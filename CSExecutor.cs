using System.Reflection;
using System.Text;
using AutoCompile;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using static MonsterSpeed.Configuration;
using static MonsterSpeed.MonsterSpeed;

namespace MonsterSpeed;

#region 脚本配置类
public class ScriptConfig
{
    [JsonProperty("启动服务器自动编译")]
    public bool AutoComp = true;
    [JsonProperty("引用列表")]
    public List<string> Usings { get; set; } = new List<string>();
}
#endregion

#region 脚本执行器（简化）
public static class CSExecutor
{
    private static bool isInit = false; // 初始化标识
    private static readonly Assembly Asm = typeof(MonsterSpeed).Assembly;  // 获取当前插件的程序集传参给自动编译插件进行注册(避免命名空间引用污染)
    public static readonly string Dir = Path.Combine(TShock.SavePath, "怪物加速", "C#脚本"); // 给编译器指定一个脚本目录

    #region 注册脚本执行器
    public static bool Register()
    {
        try
        {
            if (!isInit)
            {
                // 创建脚本存放目录
                if (!Directory.Exists(Dir))
                {
                    Directory.CreateDirectory(Dir);
                    CopyCSX(); // 释放内嵌好的脚本
                }

                isInit = true;
            }

            var exec = ScriptMag.Register(Asm, Dir, typeof(ScriptGlobals),
                                          Config.ScriptCfg.AutoComp, 
                                          Config.ScriptCfg.Usings);
            return exec != null;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"{LogName} 注册执行器失败: {ex.Message}");
            return false;
        }
    }
    #endregion

    #region 重载脚本(只对修改过的脚本重新编译)
    internal static CompResult Reload()
    {
        try
        {
            if (!isInit) return CompResult.Fail("脚本执行器未初始化，请先注册");

            if (!ServerApi.Plugins.Any(p => p.Plugin.Name == "自动编译插件")) 
                return CompResult.Fail("AutoCompile插件未加载,请重启");

            TShock.Log.ConsoleInfo($"{LogName} 开始重载脚本...");

            var result = ScriptMag.Reload(Asm);
            if (result == null) return CompResult.Fail("执行器未注册");
            return result;
        }
        catch (Exception ex)
        {
            return CompResult.Fail($"重载异常: {ex.Message}");
        }
    }
    #endregion

    #region 选择执行
    // 提供给TimerEvents.StartEvent方法使用
    public static void SelExec(string csxName, NPC npc, NpcData data, NpcState state, StringBuilder msg)
    {
        if (npc == null || !npc.active || string.IsNullOrEmpty(csxName))
            return;
        
        var exec = ScriptMag.GetExec(Asm);
        if (exec == null)
        {
            TShock.Log.ConsoleError($"{LogName} 脚本执行器未初始化");
            return;
        }

        // 编写脚本用的全局变量类,把需要传的参数写进去,方便编写时直接调用。
        var gtype = new ScriptGlobals(npc, state, data, msg);

        // 同步执行
        var result = exec.SyncRun(csxName, gtype);
        if (!result.Ok)
        {
            // 恢复原详细提示
            TShock.Log.ConsoleError($"{LogName} 执行失败: {result.Msg} 请使用指令进行编译:/reload");
        }
    }
    #endregion

    #region 提取内嵌脚本
    private static void CopyCSX()
    {
        try
        {
            var asm = Asm;
            var asmName = asm.GetName().Name;

            foreach (string res in asm.GetManifestResourceNames())
            {
                if (!res.StartsWith($"{asmName}.示例脚本."))
                    continue;

                var fName = res.Substring(asmName.Length + "示例脚本.".Length + 1);
                var tPath = Path.Combine(Dir, fName);

                if (File.Exists(tPath)) continue;

                var dir = Path.GetDirectoryName(tPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                using (var stream = asm.GetManifestResourceStream(res))
                using (var fs = new FileStream(tPath, FileMode.Create))
                {
                    stream?.CopyTo(fs);
                }
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"{LogName} 提取资源失败: {ex.Message}");
        }
    }
    #endregion

    #region 清理所有执行器
    public static void Clear()
    {
        ScriptMag.ClearPlugin(Asm);
    }
    #endregion
}
#endregion

#region 脚本全局变量包装类 编写可直接调用
public class ScriptGlobals
{
    public NPC Npc { get; set; }
    public NpcState State { get; set; }
    public StringBuilder Msg { get; set; }
    public NpcData Data { get; set; }
    internal ScriptGlobals(NPC npc, NpcState state, NpcData data, StringBuilder msg)
    {
        Npc = npc;
        State = state;
        Data = data;
        Msg = msg;
    }

    #region 简化方法包装（直接调用StateApi）
    // 1. 指示物操作
    public void SetMkr(string key, int val) => State.Set(key, val);
    public int GetMkr(string key, int def = 0) => State.Get(key, def);
    public bool HasMkr(string key) => State.Has(key);
    public void DelMkr(string key) => State.Remove(key);
    // 2. 标志操作
    public void SetFlg(string flag) => StateApi.SetFlag(Npc, flag);
    public string GetFlg() => StateApi.GetFlag(Npc);
    // 3. 事件控制
    public void NextEvt() => TimerEvents.NextEvent(Data, Npc, State);
    public void ShowTxt(double rem) => TimerEvents.ShowCoolText(Npc, Data, State, rem);
    public void SetEvtIdx(int idx) => State.EventIndex = idx;
    public int GetEvtIdx() => State.EventIndex;
    // 4. 弹幕控制
    public void SetProjIdx(int idx) => State.SendProjIdx = idx;
    public int GetProjIdx() => State.SendProjIdx;
    public void ResetProj() => State.SendCnt.Clear();
    // 9. 执行文件状态
    public FilePlayState GetPlySt(string key) => State.IndieStates.TryGetValue(key, out var s) ? s : new FilePlayState();
    public void SetPlySt(string key, FilePlayState st) => State.IndieStates[key] = st;
    // 10. 快捷功能
    public void HealNpc(int val) => Npc.life = Math.Min(Npc.lifeMax, Npc.life + val);
    public void SetDef(int val) => Npc.defense = val;
    public void SetSpd(int val) => Npc.velocity = Npc.velocity.SafeNormalize(Vector2.Zero) * val;
    // 11. 玩家目标
    public Player GetTar() => Npc.target >= 0 && Npc.target < Main.maxPlayers ? Main.player[Npc.target] : new Player();
    public void SetTar(int idx) => Npc.target = idx;
    // 12. 弹幕生成（简化版）
    public void SpawnProj(int type, int dmg, Vector2 vel, int life = 120)
    {
        if (type <= 0) return;
        int idx = Projectile.NewProjectile(
            Npc.GetSpawnSourceForNPCFromNPCAI(),
            Npc.Center.X, Npc.Center.Y,
            vel.X, vel.Y,
            type, dmg, 5f, Main.myPlayer
        );
        if (idx >= 0 && idx < Main.maxProjectiles && life > 0)
            Main.projectile[idx].timeLeft = life;
    }
    // 13. 怪物生成（简化版）
    public void SpawnNpc(int type, Vector2 pos, int cnt = 1)
    {
        if (type <= 0 || type == 113 || type == 488) return;
        for (int i = 0; i < cnt; i++)
        {
            int idx = NPC.NewNPC(
                Npc.GetSpawnSourceForNPCFromNPCAI(),
                (int)pos.X, (int)pos.Y,
                type
            );
            if (idx >= 0 && idx < Main.maxNPCs)
                Main.npc[idx].netUpdate = true;
        }
    }
    // 14. 日志输出
    public void Log(string txt) => TShock.Log.ConsoleDebug($"{LogName} {txt}");
    public void Warn(string txt) => TShock.Log.ConsoleWarn($"{LogName}{txt}");
    public void Err(string txt) => TShock.Log.ConsoleError($"{LogName} {txt}");
    // 跨NPC操作
    public List<int> FindByFlag(string flag) => StateApi.FindByFlag(flag);
    public void SetByFlag(string flag, string key, int val) => StateApi.SetByFlag(flag, key, val);
    #endregion
}
#endregion
