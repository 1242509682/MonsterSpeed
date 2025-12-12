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
    [JsonProperty("超时时间")]
    public int Timeout { get; set; } = 5000;
    [JsonProperty("异步执行")]
    public bool AsyncExec { get; set; } = false;
    [JsonProperty("引用列表")]
    public List<string> Usings { get; set; } = new List<string>();
}
#endregion

#region 脚本执行器
public static class CSExecutor
{
    public static ScriptExec Script;
    public static readonly string Dir = Path.Combine(TShock.SavePath, "怪物加速", "C#脚本");
    private static readonly ConcurrentDictionary<string, Task> Running; // 异步任务执行状态
    static CSExecutor() => Running = new ConcurrentDictionary<string, Task>();

    #region 初始化方法
    public static void Init()
    {
        try
        {
            if (!Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
                Extract(Dir);
            }

            // 使用 AutoCompile 的执行器，传入 ScriptGlobals 类型
            Script = new ScriptExec(typeof(ScriptGlobals));
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[怪物加速] 脚本执行器异常: {ex.Message}");
        }
    }
    #endregion

    #region 重载执行器方法
    internal static CompResult Reload()
    {
        try
        {
            TShock.Log.ConsoleInfo("[怪物加速] 开始重载脚本执行器...");

            // 不清除脚本缓存，只重新编译需要更新的
            if (Script == null)
            {
                Init(); // 如果执行器为null，重新初始化
            }

            // 重新批量编译（会自动跳过未修改的）
            if (Script != null)
            {
                Script.BatchCompile(Dir, Config.ScriptCfg.Usings);
                return CompResult.Success("重载完成，已重新编译修改过的脚本");
            }
            else
            {
                return CompResult.Fail("脚本执行器初始化失败");
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[怪物加速] 重载异常: {ex.Message}");
            return CompResult.Fail($"重载异常: {ex.Message}");
        }
    }
    #endregion

    #region 选择执行方法
    public static void SelExec(string name, NPC npc, NpcData data, NpcState state, StringBuilder msg)
    {
        if (Script == null)
        {
            TShock.Log.ConsoleError("脚本执行器未初始化");
            return;
        }

        var globals = new ScriptGlobals(npc, state, data, msg);

        if (!Config.ScriptCfg.AsyncExec)
        {
            // 同步执行
            var result = Script.SyncRun(name, globals);
            if (!result.Ok)
            {
                TShock.Log.ConsoleError($"执行失败: {result.Msg} 请使用指令进行编译:/reload");
            }
        }
        else
        {
            // 异步执行
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
                    var result = await Script.AsyncRun(name, globals, Config.ScriptCfg.Timeout);
                    if (!result.Ok)
                    {
                        TShock.Log.ConsoleError($"执行失败: {result.Msg} 请使用指令进行编译:/reload");
                    }
                }
                finally
                {
                    Running.TryRemove(key, out _);
                }
            });

            Running[key] = task;
        }
    }
    #endregion

    #region 复制自己程序集
    public static void CopyMosDll(string AsseDir)
    {
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
        string plugin = null;
        var allDlls = Directory.GetFiles(spDir, "*.dll");
        foreach (var dllPath in allDlls)
        {
            try
            {
                var asmName = AssemblyName.GetAssemblyName(dllPath);
                if (asmName.Name == AsmName)
                {
                    plugin = dllPath;
                    break;
                }
            }
            catch { }
        }

        // 3. 如果找到插件且程序集目录中没有它，则复制
        if (plugin != null)
        {
            var pluginName = Path.GetFileName(plugin);
            var dst = Path.Combine(AsseDir, pluginName);
            try
            {
                File.Copy(plugin, dst, true);
                TShock.Log.ConsoleInfo($"[怪物加速] 已复制本插件到 {AsseDir} 作为脚本引用");
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"[怪物加速] 复制插件失败: {ex.Message}");
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

} 
#endregion