using Terraria;

namespace MonsterSpeed;

#region NPC状态管理（统一）
public class NpcState
{
    // 基础状态
    public int Index { get; set; } = 0;
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
    public DateTime LastTextTime { get; set; } = DateTime.UtcNow;

    // 计数状态
    public int SPCount { get; set; } = 0;  // 弹幕计数
    public int SNCount { get; set; } = 0;  // 怪物计数
    public Dictionary<int, int> EventCounts { get; set; } = new();
    public Dictionary<int, int> PlayCounts { get; set; } = new();
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
}

public static class StateUtil
{
    private static readonly Dictionary<int, NpcState> NpcStates = new();

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
            // 同时清理弹幕更新计时器
            MyProjectile.UpdateTimer.Remove(npc.whoAmI);
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


