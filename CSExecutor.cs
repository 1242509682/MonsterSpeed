using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using AutoCompile;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;
using static MonsterSpeed.Configuration;
using static MonsterSpeed.MonsterSpeed;

namespace MonsterSpeed;

#region 脚本配置类
public class ScriptConfig
{
    [JsonProperty("启动服务器自动编译")]
    public bool Int { get; set; } = true;
    [JsonProperty("启用编译")]
    public bool Enabled { get; set; } = true;
    [JsonProperty("超时时间")]
    public int Timeout { get; set; } = 5000;
    [JsonProperty("脚本目录")]
    public string ScriptDir { get; set; } = "C#脚本";
    [JsonProperty("引用列表")]
    public List<string> Usings { get; set; } = new List<string>();
}
#endregion

public static class CSExecutor
{
    private static readonly ConcurrentDictionary<string, Task> Running;
    static CSExecutor() => Running = new ConcurrentDictionary<string, Task>();
    private static ScriptExec Script;
    public static readonly string ScriptDir = Path.Combine(TShock.SavePath, "怪物加速", Config.ScriptCfg.ScriptDir);

    #region 初始化方法
    public static void Init()
    {
        if (!Directory.Exists(ScriptDir))
        {
            Directory.CreateDirectory(ScriptDir);
            Extract(ScriptDir);
        }

        // 收集必要的程序集引用
        var ExtraAsse = new List<string>();

        // 添加脚本程序集目录中的 DLL
        var AsseDir = Path.Combine(TShock.SavePath, "怪物加速", "脚本程序集");
        if (Directory.Exists(AsseDir))
        {
            foreach (var dll in Directory.GetFiles(AsseDir, "*.dll"))
            {
                try
                {
                    var name = AssemblyName.GetAssemblyName(dll);
                    if (name != null)
                    {
                        ExtraAsse.Add(dll);
                    }
                }
                catch { }
            }
        }
        else
        {
            Directory.CreateDirectory(AsseDir);
        }

        // 使用 AutoCompile 的执行器，传入 ScriptGlobals 类型
        Script = new ScriptExec(typeof(ScriptGlobals), ExtraAsse);
        TShock.Log.ConsoleInfo($"[怪物加速] 脚本执行器初始化完成");
    }
    #endregion

    #region 复制自己程序集
    public static void CopyMosDll()
    {
        var AsseDir = Path.Combine(TShock.SavePath, "自动编译", "程序集");

        // 如果目录不存在，直接返回（等待自动编译插件创建）
        if (!Directory.Exists(AsseDir))
        {
            return;
        }

        // 查找插件DLL（允许改名）
        var spDir = Path.Combine(typeof(TShock).Assembly.Location, "ServerPlugins");
        var AsmName = Assembly.GetExecutingAssembly().GetName().Name;

        // 1. 首先检查目标目录是否已存在相同程序集
        var exis = Directory.GetFiles(AsseDir, "*.dll");
        foreach (var dllPath in exis)
        {
            try
            {
                var asmName = AssemblyName.GetAssemblyName(dllPath);
                if (asmName.Name == AsmName)
                {
                    // 已存在相同程序集，无需复制
                    return;
                }
            }
            catch { }
        }

        // 2. 在ServerPlugins中查找当前插件DLL
        string pluginPath = null;
        var allDlls = Directory.GetFiles(spDir, "*.dll");
        foreach (var dllPath in allDlls)
        {
            try
            {
                var asmName = AssemblyName.GetAssemblyName(dllPath);
                if (asmName.Name == AsmName)
                {
                    pluginPath = dllPath;
                    break;
                }
            }
            catch { }
        }

        // 3. 如果找到插件且程序集目录中没有它，则复制
        if (pluginPath != null)
        {
            var pluginName = Path.GetFileName(pluginPath);
            var dst = Path.Combine(AsseDir, pluginName);
            try
            {
                File.Copy(pluginPath, dst, true);
                TShock.Log.ConsoleInfo($"[怪物加速] 已复制本插件到 {AsseDir} 作为脚本引用");
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"[怪物加速] 复制插件失败: {ex.Message}");
            }
        }
    }
    #endregion

    #region 批量编译脚本
    public static CompResult BatchCompile()
    {
        if (Script == null)
        {
            return CompResult.Fail("脚本执行器未初始化");
        }

        Script.ClearCache(); // 先清理缓存

        var result = Script.BatchCompile(
            ScriptDir,
            Config.ScriptCfg.Usings,
            Config.ScriptCfg?.Enabled ?? true
        );
        return result;
    }
    #endregion

    #region 选择执行方法
    public static void SelExec(string name, NPC npc, NpcData data, NpcState state, StringBuilder msg, bool async = false)
    {
        if (Script == null)
        {
            TShock.Log.ConsoleError("脚本执行器未初始化");
            return;
        }

        if (async)
        {
            AsyncExec(name, npc, data, state, msg);
        }
        else
        {
            var globals = new ScriptGlobals(npc, state, data, msg);
            var timeout = Config.ScriptCfg?.Timeout ?? 5000;
            var result = Script.SyncRun(name, globals, timeout);
            if (!result.Ok)
            {
                TShock.Log.ConsoleError($"执行失败: {result.Msg} 请使用指令进行编译:/reload");
            }
        }
    }
    #endregion

    #region 异步执行方法
    private static void AsyncExec(string name, NPC npc, NpcData data, NpcState state, StringBuilder msg)
    {
        var key = $"{npc.whoAmI}_{name}";

        if (!Running.TryAdd(key, Task.CompletedTask))
        {
            TShock.Log.ConsoleError($"脚本执行中:{name}");
            return;
        }

        var task = Task.Run(async () =>
        {
            try
            {
                try
                {
                    var globals = new ScriptGlobals(npc, state, data, msg);
                    var timeout = Config.ScriptCfg?.Timeout ?? 5000;
                    var result = await Script.AsyncRun(name, globals, timeout);
                    if (!result.Ok)
                    {
                        TShock.Log.ConsoleError($"执行失败: {result.Msg} 请使用指令进行编译:/reload");
                    }
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError($"脚本异常: {ex.Message}");
                }
            }
            finally
            {
                Running.TryRemove(key, out _);
            }
        });

        Running[key] = task;
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
    internal static CompResult Reload()
    {
        try
        {
            TShock.Log.ConsoleInfo("[怪物加速] 开始重载脚本执行器...");

            // 修复：分步完全释放
            if (Script != null)
            {
                Script.Dispose();
                Script = null;  // 重要：切断静态引用
            }

            // 修复：清空并发字典防止内存泄漏
            Running?.Clear();

            // 修复：强制GC收集所有代
            for (int i = 0; i < 3; i++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                Thread.Sleep(20);  // 短暂延迟确保完全回收
            }

            long before = GC.GetTotalMemory(true);
            Init();
            long after = GC.GetTotalMemory(true);

            TShock.Log.ConsoleInfo($"[怪物加速] 重载后内存变化: {(after - before) / 1024 / 1024}MB");

            if (Config.ScriptCfg?.Enabled == true)
            {
                var result = BatchCompile();

                // 修复：编译后立即清理临时对象
                Compiler.ClearMetaRefs();

                // 仅清理第2代，避免过度GC
                GC.Collect(2, GCCollectionMode.Default);

                return result;
            }

            return CompResult.Success("重载完成");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[怪物加速] 重载异常: {ex.Message}");
            return CompResult.Fail($"重载异常: {ex.Message}");
        }
    }
    #endregion
}