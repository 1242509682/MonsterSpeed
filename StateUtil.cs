using Terraria;

namespace MonsterSpeed;

#region NPC状态管理（统一）
public class NpcState
{
    // 时间事件基础状态
    public int EventIndex { get; set; } = 0; // 时间事件索引
    public DateTime CooldownTime { get; set; } = DateTime.UtcNow;   // 时间事件冷却时间
    public DateTime LastTextTime { get; set; } = DateTime.UtcNow;   // 时间事件冷却倒计时（悬浮文本）

    // 计数状态
    public int SPCount { get; set; } = 0;  // 弹幕计数
    public int SNCount { get; set; } = 0;  // 怪物计数
    public int Struck { get; set; } = 0;    // 受击计数
    public int KillPlay { get; set; } = 0;   // 击杀计数
    public int ActiveTime { get; set; } = 0; // 存活时间计数
    public int ProjIndex { get; set; } = 0; // 弹幕索引号

    public Dictionary<int, int> EventCounts { get; set; } = new();
    public Dictionary<string, int> PlayCounts { get; set; } = new();
    public Dictionary<int, float> SpawnTimer = new Dictionary<int, float>(); //用于追踪该NPC生成随从NPC冷却时间

    // 弹幕状态
    public Dictionary<int, int> SendStack { get; set; } = new();
    public Dictionary<int, float> EachCDs { get; set; } = new();

    public AIState AIState { get; set; } = new AIState();

    // 指示物系统
    public Dictionary<string, int> Markers { get; set; } = new();

    // 子系统状态
    public FilePlayState FileState { get; set; } = new();
    public PauseState PauseState { get; set; } = new();
    public MoveModeState MoveState { get; set; } = new();

    // 新增：独立播放器状态（短变量名）
    public Dictionary<string, IndieState> IndieStates { get; set; } = new();
}

public static class StateUtil
{
    public static readonly Dictionary<int, NpcState> NpcStates = new();

    #region 获取NPC状态
    public static NpcState GetState(NPC npc)
    {
        if (npc == null || !npc.active)
            return new NpcState();

        if (!NpcStates.ContainsKey(npc.whoAmI))
        {
            NpcStates[npc.whoAmI] = new NpcState
            {
                MoveState = new MoveModeState 
                { 
                    DashStartPosition = npc.Center 
                }
            };
        }

        return NpcStates[npc.whoAmI];
    }
    #endregion

    #region 清理NPC状态
    public static void ClearState(NPC npc)
    {
        if (npc != null)
        {
            NpcStates.Remove(npc.whoAmI);
            MyProjectile.UpdateTimer.Remove(npc.whoAmI);  // 同时清理弹幕更新计时器
        }
    }
    #endregion

    #region 清理所有状态
    public static void ClearAllStates()
    {
        NpcStates.Clear();
        MyProjectile.UpdateTimer.Clear();
    }
    #endregion
}
#endregion


