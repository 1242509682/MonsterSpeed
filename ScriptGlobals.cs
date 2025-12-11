using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using TShockAPI;
using static MonsterSpeed.Configuration;

namespace MonsterSpeed;

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
    public void Log(string txt) => TShock.Log.ConsoleDebug($"[怪物加速] {txt}");
    public void Warn(string txt) => TShock.Log.ConsoleWarn($"[怪物加速] {txt}");
    public void Err(string txt) => TShock.Log.ConsoleError($"[怪物加速] {txt}");
    // 跨NPC操作
    public List<int> FindByFlag(string flag) => StateApi.FindByFlag(flag);
    public void SetByFlag(string flag, string key, int val) => StateApi.SetByFlag(flag, key, val);
    #endregion
}
#endregion
