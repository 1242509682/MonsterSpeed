using Terraria;
using TShockAPI;

namespace MonsterSpeed;

#region NPC状态管理
public class NpcState
{
    // 时间事件基础状态
    public int EventIndex { get; set; } = 0; // 时间事件索引
    public Dictionary<int, int> EventCounts { get; set; } = new(); // 时间事件执行计数
    public Dictionary<int,DateTime> CooldownTime { get; set; } = new Dictionary<int, DateTime>();   // 时间事件冷却时间
    public DateTime LastTextTime { get; set; } = DateTime.UtcNow;   // 时间事件冷却倒计时（悬浮文本）

    // 怪物全局计数状态
    public int SPCount { get; set; } = 0;    // 总计发射弹幕组次数
    public int SNCount { get; set; } = 0;    // 总计召唤随从组次数
    public int Struck { get; set; } = 0;     // 受击计数
    public int KillPlay { get; set; } = 0;   // 击杀玩家计数
    public int ActiveTime { get; set; } = 0; // 存活秒数计数


    public Dictionary<string, int> PlayCounts { get; set; } = new(); // 执行文件播放计数
    public Dictionary<int, float> SpawnTimer = new Dictionary<int, float>(); //用于追踪该NPC生成随从NPC冷却时间

    // 弹幕状态
    public int SendProjIndex { get; set; } = 0;                      // 发射弹幕组的索引号
    public Dictionary<int, int> SendStack { get; set; } = new(); // 本组弹幕发射计数
    public Dictionary<int, float> SendCD { get; set; } = new();  // 本组弹幕发射间隔

    // AI赋值状态
    public AIState AIState { get; set; } = new AIState();

    // 指示物系统状态
    public Dictionary<string, int> Markers { get; set; } = new();

    // 行动模式状态
    public MoveModeState MoveState { get; set; } = new();

    // 执行文件播放状态
    public Dictionary<string, FilePlayState> IndieStates { get; set; } = new();
}
#endregion

public static class StateUtil
{
    // 怪物状态，键为怪物索引,值为状态类
    public static readonly Dictionary<int, NpcState> NpcStates = new();

    #region 获取NPC状态
    public static NpcState GetState(Entity npc)
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
    public static void ClearState(Entity npc)
    {
        if (npc != null)
        {
            NpcStates.Remove(npc.whoAmI);
            UpdateProjectile.UpdateTimer.Remove(npc.whoAmI);  // 同时清理弹幕更新计时器
        }
    }
    #endregion

    #region 清理所有状态
    public static void ClearAllStates()
    {
        NpcStates.Clear();
        UpdateProjectile.UpdateTimer.Clear();
    }
    #endregion
}
