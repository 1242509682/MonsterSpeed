using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;
using static MonsterSpeed.MonsterSpeed;

namespace MonsterSpeed;

#region 脚本配置类
public class ScriptConfig
{
    [JsonProperty("启用缓存")]
    public bool UseCache { get; set; } = true;
    [JsonProperty("超时时间")]
    public int Timeout { get; set; } = 5000;
    [JsonProperty("脚本目录")]
    public string ScriptDir { get; set; } = "C#脚本";
    [JsonProperty("引用列表")]
    public List<string> Usings { get; set; } = new List<string>();
    [JsonProperty("系统程序集引用")]
    public List<string> SystemAsse { get; set; } = new List<string>();
}
#endregion

#region 脚本全局变量包装类
public class ScriptGlobals
{
    public NPC Npc { get; set; }
    public NpcState State { get; set; }
    public StringBuilder Msg { get; set; }

    internal ScriptGlobals(NPC npc, NpcState state, StringBuilder msg)
    {
        Npc = npc;
        State = state;
        Msg = msg;
    }
}
#endregion

#region 脚本执行结果
public class ScriptResult
{
    public bool Ok { get; }
    public string Msg { get; }
    public object Data { get; }

    private ScriptResult(bool ok, string msg, object data = null)
    {
        Ok = ok;
        Msg = msg;
        Data = data;
    }

    public static ScriptResult Success(string msg = "完成", object data = null) => new ScriptResult(true, msg, data);
    public static ScriptResult Fail(string msg) => new ScriptResult(false, msg);
}
#endregion

public class CSExecutor : IDisposable
{
    #region 构造函数
    internal CSExecutor()
    {
        lockObj = new object();
        cache = new Dictionary<string, ScriptRunner<object>>(StringComparer.OrdinalIgnoreCase);
        defOpt = CreateDefOpt();   // 创建编译选项
        TShock.Log.ConsoleInfo($"[怪物加速] 脚本执行器初始化完成");
    }
    #endregion

    private ScriptOptions defOpt;
    private readonly Dictionary<string, ScriptRunner<object>> cache;
    private readonly object lockObj;
    private bool isDisposed = false;
    private List<MetadataReference> metaRefs = new();

    #region 创建编译选项 - 核心方法
    private ScriptOptions CreateDefOpt()
    {
        try
        {
            // 清理旧的元数据引用
            ClearMeta();

            // 收集所有必要的程序集路径
            HashSet<string>? refs = new HashSet<string>();
            // 1. 添加TS的引用
            AddTShockReferences(refs);
            // 2. 添加.NET系统程序集
            AddSystemAss(refs);

            // 转换为绝对路径并确保文件存在
            List<string> validRefs = new List<string>();
            foreach (var r in refs)
            {
                try
                {
                    if (File.Exists(r))
                    {
                        var fullPath = Path.GetFullPath(r);
                        validRefs.Add(fullPath);
                        TShock.Log.ConsoleDebug($"[怪物加速] 添加引用: {Path.GetFileName(fullPath)}");
                    }
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleWarn($"[怪物加速] 跳过无效引用 {r}: {ex.Message}");
                }
            }

            // 创建元数据引用并保存
            metaRefs = validRefs.Select(r => MetadataReference.CreateFromFile(r)).ToList<MetadataReference>();
            TShock.Log.ConsoleInfo($"[怪物加速] 共收集到 {metaRefs.Count} 个程序集引用");

            // 默认的命名空间导入
            string[] imports = new[]
            {
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "System.Text",
                "System.Threading.Tasks",
                "Terraria",
                "Microsoft.Xna.Framework",
                "TShockAPI",
                "MonsterSpeed"
            };

            var opts = ScriptOptions.Default
                .WithReferences(metaRefs)
                .WithImports(imports)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithAllowUnsafe(true);

            return opts;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[脚本]创建编译选项失败: {ex.Message}");
            return null;
        }
    }
    #endregion

    #region 清理元数据引用
    private void ClearMeta()
    {
        try
        {
            // 释放旧的元数据引用
            if (metaRefs != null)
            {
                metaRefs.Clear();
                metaRefs = null;
            }

            // 清理编译选项
            defOpt = null;
            cache.Clear();

            // 强制垃圾回收释放非托管资源
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
        }
        catch { }
    }
    #endregion

    #region 系统程序集引用
    private void AddSystemAss(HashSet<string> refs)
    {
        try
        {
            int added = 0;

            // 获取.NET运行时的系统程序集目录
            var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location);

            if (!string.IsNullOrEmpty(runtime))
            {
                var Asse = Config.ScriptCfg.SystemAsse;

                foreach (var ass in Asse)
                {
                    var file = Path.Combine(runtime, ass);

                    if (File.Exists(file) && !refs.Contains(file))
                    {
                        refs.Add(file);
                        added++;
                    }
                    else
                    {
                        TShock.Log.ConsoleError($"[怪物加速] 文件不存在 {file} ");
                    }
                }

                if (added > 0)
                    TShock.Log.ConsoleInfo($"[怪物加速] 添加了 {added} 个系统程序集");
            }
        }
        catch { }
    }
    #endregion

    #region 添加TS程序集引用
    private static void AddTShockReferences(HashSet<string> refs)
    {
        try
        {
            var count = 0;
            var dir = Path.Combine(Configuration.Paths, "脚本程序集");

            // 0.《脚本程序集》不存在 创建文件夹
            // 扫描ServerPlugins文件夹把MonsterSpeed.dll同名的程序集复制进去方便第一步时加载(避免手贱改名,引用不到)
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);

                // 查找插件DLL（允许改名）
                var pluginsDir = Path.Combine(typeof(TShock).Assembly.Location, "ServerPlugins");
                var AsmName = Assembly.GetExecutingAssembly().GetName().Name;

                // 查找可能的DLL文件
                string pluginPath = string.Empty;

                // 扫描所有DLL
                var allDlls = Directory.GetFiles(pluginsDir, "*.dll");
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

                if (pluginPath != null)
                {
                    var pluginName = Path.GetFileName(pluginPath);
                    var dst = Path.Combine(dir, pluginName);
                    File.Copy(pluginPath, dst, true);
                    TShock.Log.ConsoleInfo($"[怪物加速] 已复制插件DLL: {pluginName}");
                }
            }

            // 1. 《脚本程序集》目录存在 读取里面所有程序集
            if (Directory.Exists(dir))
            {
                var dllFiles = Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories);
                foreach (var dllPath in dllFiles)
                {
                    // 确保文件存在且不是重复的
                    if (File.Exists(dllPath) && !refs.Contains(dllPath))
                    {
                        // 跳过可能损坏或无法加载的DLL
                        if (Tool.IsValidDll(dllPath))
                        {
                            refs.Add(dllPath);
                            count++;
                        }
                        else
                        {
                            TShock.Log.ConsoleWarn($"[怪物加速] 跳过无效的程序集: {Path.GetFileName(dllPath)}");
                        }
                    }
                }
            }

            if (count > 0)
                TShock.Log.ConsoleInfo($"[怪物加速] 从‘程序集’添加了 {count} 个引用");

            // 2.添加TShockAPI.dll
            var PluginsDir = Path.Combine(typeof(TShock).Assembly.Location, "ServerPlugins");
            var path2 = Path.Combine(PluginsDir, "TShockAPI.dll");
            if (File.Exists(path2) && !refs.Contains(path2))
            {
                refs.Add(path2);
            }

            // 3.添加TS运行核心文件（从bin目录）
            var OT = new[]
            {
                "OTAPI.dll",
                "OTAPI.Runtime.dll",
                "HttpServer.dll",
                "ModFramework.dll",
                "TerrariaServer.dll"
            };

            foreach (var f in OT)
            {
                var binDir = Path.Combine(typeof(TShock).Assembly.Location, "bin");
                var path3 = Path.Combine(binDir, f);
                if (File.Exists(path3) && !refs.Contains(path3))
                {
                    refs.Add(path3);
                }
            }

        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[怪物加速] 扫描目录失败: {ex.Message}");
        }
    }
    #endregion

    #region 执行脚本方法（带缓存）
    internal async Task<ScriptResult> Run(string name, NPC npc, NpcState state, StringBuilder msg)
    {
        try
        {
            // 安全检查
            if (npc is null)
                return ScriptResult.Fail("NPC对象为空");

            if (state is null)
            {
                state = StateApi.GetState(npc);

                if (state == null)
                {
                    return ScriptResult.Fail("无法获取NPC状态");
                }
            }

            if (msg is null)
            {
                msg = new StringBuilder();
            }

            // 检查脚本文件
            var scriptDir = Path.Combine(TShock.SavePath, "怪物加速", Config.ScriptCfg.ScriptDir);
            var scriptPath = Path.Combine(scriptDir, name.EndsWith(".csx") ? name : name + ".csx");

            if (!File.Exists(scriptPath))
                return ScriptResult.Fail($"脚本文件不存在: {name}");

            var code = await File.ReadAllTextAsync(scriptPath, Encoding.UTF8);

            // 检查缓存
            ScriptRunner<object> runner = null;

            lock (lockObj)
            {
                cache.TryGetValue(name, out runner);
            }

            // 执行脚本
            if (runner != null)
            {
                var globals = new ScriptGlobals(npc, state, msg);
                if (globals is null)
                {
                    return ScriptResult.Fail("无法创建脚本全局变量");
                }

                var timeout = Config.ScriptCfg?.Timeout ?? 5000;

                var task = runner(globals);

                if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
                {
                    var result = await task;
                    TShock.Log.ConsoleDebug($"[脚本]执行完成: {name}");
                    return ScriptResult.Success(result?.ToString() ?? "完成");
                }
                else
                {
                    TShock.Log.ConsoleWarn($"[脚本]执行超时: {name} ({timeout}ms)");
                    return ScriptResult.Fail($"脚本执行超时 ({timeout}ms)");
                }
            }

            return ScriptResult.Fail("无法创建或获取脚本运行器");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[脚本]运行 {name} 时发生异常: \n{ex.Message}\n{ex.StackTrace}");
            return ScriptResult.Fail($"执行异常: \n{ex.Message}");
        }
    }
    #endregion

    #region 为代码添加默认 using
    private static string AddUsings(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return code;

        // 从配置中获取默认 using 指令
        var defList = Config.ScriptCfg.Usings;
        // 从配置获取并格式化
        var fmtUsgs = Tool.FmtUsings(defList);

        if (string.IsNullOrEmpty(fmtUsgs))
            return code;

        // 检查代码中是否已经有这些 using（避免重复）
        var existing = Tool.GetExistUsings(code);

        // 过滤掉已经存在的 using
        var ToAdd = Tool.FilterUsings(fmtUsgs, existing);

        if (string.IsNullOrEmpty(ToAdd))
            return code;

        // 总是添加到文件最开头
        return ToAdd + code;
    }
    #endregion

    #region 重新加载程序集引用
    public ScriptResult Reload()
    {
        try
        {
            lock (lockObj)
            {
                TShock.Log.ConsoleInfo($"[怪物加速] 开始重新加载程序集引用...");

                // 重新创建编译选项
                defOpt = CreateDefOpt();

                if (defOpt == null)
                {
                    return ScriptResult.Fail("创建编译选项失败");
                }

                TShock.Log.ConsoleInfo($"[怪物加速] 程序集引用重新加载完成");
                return ScriptResult.Success("程序集引用重新加载成功");
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[怪物加速] 重新加载程序集引用失败: {ex.Message}");
            return ScriptResult.Fail($"重新加载程序集引用失败: {ex.Message}");
        }
    }
    #endregion

    #region 预编译脚本方法
    internal ScriptResult PreCompile(string name)
    {
        try
        {
            // 检查脚本文件
            var scDir = Path.Combine(TShock.SavePath, "怪物加速", Config.ScriptCfg.ScriptDir);
            var sctPath = Path.Combine(scDir, name.EndsWith(".csx") ? name : name + ".csx");

            if (!File.Exists(sctPath))
                return ScriptResult.Fail($"脚本文件不存在: {name}");

            var code = File.ReadAllText(sctPath, Encoding.UTF8);

            lock (lockObj)
            {
                // 如果缓存中已有，先移除旧的
                ClearCache(name);

                // 预处理代码：确保有正确的using指令
                code = AddUsings(code);

                // 创建脚本
                var script = CSharpScript.Create<object>(
                    code,
                    options: defOpt,
                    globalsType: typeof(ScriptGlobals));

                // 检查编译错误
                var comp = script.GetCompilation();
                var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

                if (errors.Any())
                {
                    var errList = string.Join("\n", errors.Select(e => e.ToString()));
                    return ScriptResult.Fail($"编译错误:\n{errList}");
                }

                // 创建委托并缓存
                var runner = script.CreateDelegate();
                cache[name] = runner;
                return ScriptResult.Success("预编译成功");
            }
        }
        catch (CompilationErrorException ex)
        {
            return ScriptResult.Fail($"编译失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ScriptResult.Fail($"预编译异常: {ex.Message}");
        }
    }
    #endregion

    #region 清理缓存
    public void ClearCache(string name = null)
    {
        lock (lockObj)
        {
            if (name == null)
            {
                cache.Clear();
                TShock.Log.ConsoleInfo("[脚本]已清除所有脚本缓存");
            }
            else
            {
                if (cache.Remove(name))
                {
                    TShock.Log.ConsoleDebug($"[脚本]已清除脚本缓存: {name}");
                }
            }
        }
    }
    #endregion

    #region IDisposable 接口（用来Reload时释放执行器的）
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!isDisposed)
        {
            if (disposing)
            {
                lock (lockObj)
                {
                    // 清理托管资源
                    cache.Clear();
                    ClearMeta();
                    defOpt = null;
                }
            }

            isDisposed = true;
        }
    }

    ~CSExecutor()
    {
        Dispose(false);
    }
    #endregion
}