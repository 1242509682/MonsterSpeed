using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Terraria;
using TShockAPI;
using static MonsterSpeed.Configuration;
using static MonsterSpeed.MonsterSpeed;

namespace MonsterSpeed;

public static class AsyncExec
{
    private static readonly ConcurrentDictionary<string, Task> running;
    static AsyncExec() => running = new ConcurrentDictionary<string, Task>();
    public static readonly string ScriptDir = Path.Combine(TShock.SavePath, "怪物加速", Config.ScriptCfg.ScriptDir);

    #region 初始化方法
    public static void Init()
    {
        if (!Directory.Exists(ScriptDir))
        {
            Directory.CreateDirectory(ScriptDir);
            Extract(ScriptDir);
        }

        scriptExec = new CSExecutor();

        // 新增：初始化时自动编译所有脚本
        if (Config.ScriptCfg?.UseCache == true)
        {
            var result = BatchCompile();
            if (result.Ok)
            {
                TShock.Log.ConsoleInfo($"[怪物加速] 脚本初始化编译完成: {result.Msg}");
            }
            else
            {
                TShock.Log.ConsoleWarn($"[怪物加速] 脚本初始化编译有错误: {result.Msg}");
            }
        }
    }
    #endregion

    #region 内嵌脚本示例管理
    internal static void Extract(string AsmPath)
    {
        var asm = Assembly.GetExecutingAssembly();
        string assemblyName = asm.GetName().Name!;

        foreach (string res in asm.GetManifestResourceNames())
        {
            if (!res.StartsWith($"{assemblyName}.示例脚本."))
                continue;

            string fileName = res.Substring(assemblyName.Length + "示例脚本.".Length + 1);
            string tarPath = Path.Combine(AsmPath, fileName);

            if (File.Exists(tarPath)) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(tarPath)!);

            using (var stream = asm.GetManifestResourceStream(res))
            {
                if (stream == null) continue;

                using (var fs = new FileStream(tarPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096))
                {
                    stream.CopyTo(fs);
                }
            }
        }
    }
    #endregion

    #region 重载执行器方法
    internal static ScriptResult Reload()
    {
        try
        {
            // 清理旧的执行器
            scriptExec.Dispose();

            // 强制GC
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            // 创建新的执行器
            scriptExec = new CSExecutor();

            return ScriptResult.Success("程序集引用重新加载成功");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[怪物加速] 重新加载程序集引用异常: {ex.Message}");
            return ScriptResult.Fail($"重新加载程序集引用异常: {ex.Message}");
        }
    }
    #endregion

    #region 批量编译脚本
    public static ScriptResult BatchCompile()
    {
        try
        {
            if (!Directory.Exists(ScriptDir))
            {
                return ScriptResult.Fail($"脚本目录不存在: {ScriptDir}");
            }

            var Files = Directory.GetFiles(ScriptDir, "*.csx", SearchOption.AllDirectories);
            int total = Files.Length;
            int yes = 0;
            int no = 0;

            foreach (var filePath in Files)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var result = scriptExec.PreCompile(fileName);
                    if (result.Ok)
                    {
                        yes++;
                        TShock.Log.ConsoleDebug($"[怪物加速]预编译成功: {fileName}");
                    }
                    else
                    {
                        no++;
                        TShock.Log.ConsoleError($"[怪物加速]预编译失败:\n {fileName} - {result.Msg}");
                    }
                }
                catch (Exception ex)
                {
                    no++;
                    var fileName = Path.GetFileName(filePath);
                    TShock.Log.ConsoleError($"[怪物加速]预编译异常:\n {fileName} - {ex.Message}");
                }
            }

            var msg = $"批量编译完成: 总数{total}, 成功{yes}, 失败{no}";
            return ScriptResult.Success(msg);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[怪物加速] 批量编译异常: {ex.Message}");
            return ScriptResult.Fail($"批量编译异常: {ex.Message}");
        }
    }
    #endregion

    #region 执行脚本 模式选择(异步或同步)
    public static void Exec(string name, NPC npc, Configuration.NpcData data, NpcState state, StringBuilder msg, bool async = false)
    {
        if (scriptExec == null)
        {
            TShock.Log.ConsoleInfo($"脚本执行器未初始化");
            msg?.AppendLine(" 脚本执行器未初始化");
            return;
        }

        if (async)
        {
            var key = $"{npc.whoAmI}_{name}";

            // 使用TryAdd确保线程安全
            if (!running.TryAdd(key, Task.CompletedTask))
            {
                TShock.Log.ConsoleInfo($"脚本已在执行中:{name}");
                msg?.AppendLine($" 脚本执行中:{name}");
                return;
            }

            var task = Task.Run(async () =>
            {
                try
                {
                    await scriptExec.Run(name, npc, data, state, msg);
                }
                finally
                {
                    running.TryRemove(key, out _);
                }
            });

            // 更新为实际任务
            running[key] = task;
        }
        else
        {
            var task = scriptExec.Run(name, npc, data, state, msg);
            task.GetAwaiter().GetResult();
            if (!task.Result.Ok)
            {
                TShock.Log.ConsoleError($"脚本执行失败:{name} 错误:{task.Result.Msg}");
                msg?.AppendLine($" 执行失败:{task.Result.Msg}");
            }
        }
    }
    #endregion
}