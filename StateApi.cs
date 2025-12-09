using System.Collections.Concurrent;
using Terraria;

namespace MonsterSpeed;

#region NPC状态管理
public class NpcState
{
    public string Flag { get; set; } = string.Empty; // 标志
    public Dictionary<int, bool> Script { get; set; } = new(); // 脚本执行状态
    public ConcurrentDictionary<string, int> Marker { get; set; } = new(); // c#脚本里的指示物数据
    public Dictionary<string, int> Markers { get; set; } = new();  // 配置里指示物数据
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
    public int SendProjIdx { get; set; } = 0; // 发射弹幕组的索引号
    public Dictionary<int, int> SendCnt { get; set; } = new(); // 本组弹幕发射计数
    public Dictionary<int, float> SendCD { get; set; } = new();  // 本组弹幕发射间隔
    // 其他模式状态
    public AIState AIState { get; set; } = new AIState();  // AI赋值状态
    public MoveModeState MoveState { get; set; } = new();  // 行动模式状态
    public Dictionary<string, FilePlayState> IndieStates { get; set; } = new();  // 执行文件播放状态

    #region 构造函数
    public NpcState() { }
    public NpcState(NPC npc)
    {
        if (npc != null)
        {
            MoveState = new MoveModeState { DashStartPosition = npc.Center };
        }
    }
    #endregion

    #region 指示物操作方法
    public int Get(string key, int def = 0) => Marker.GetValueOrDefault(key, def);
    public void Set(string key, int val) => Marker[key] = val;
    public void Add(string key, int val) => Marker.AddOrUpdate(key, val, (k, o) => o + val);
    public bool Has(string key) => Marker.ContainsKey(key);
    public bool Remove(string key) => Marker.TryRemove(key, out _);
    public void Clear() => Marker.Clear();
    public Dictionary<string, int> GetAll() => new Dictionary<string, int>(Marker);
    public bool Check(string key, string op, int val) => op switch
    {
        "==" => Get(key) == val,
        "!=" => Get(key) != val,
        ">" => Get(key) > val,
        "<" => Get(key) < val,
        ">=" => Get(key) >= val,
        "<=" => Get(key) <= val,
        _ => false
    };
    #endregion
}
#endregion

public static class StateApi
{
    // 怪物状态，键为怪物索引,值为状态类
    public static readonly ConcurrentDictionary<int, NpcState> NpcStates = new();  // 怪物状态，键为怪物索引,值为状态类

    #region NPC状态核心管理方法
    // 获取与创建NPC状态
    public static NpcState GetState(NPC npc) => NpcStates.GetOrAdd(npc.whoAmI, _ => new NpcState());
    // 清理指定NPC状态
    public static void ClearState(NPC npc) => NpcStates.TryRemove(npc.whoAmI, out _);
    // 清理所有NPC状态
    public static void ClearAll() => NpcStates.Clear();
    #endregion

    #region 标志系统
    public static string GetFlag(NPC npc) => GetState(npc).Flag;
    public static void SetFlag(NPC npc, string flag) => GetState(npc).Flag = flag;
    #endregion

    #region 跨NPC修改方法
    // 1. 按标志找NPC
    public static List<int> FindByFlag(string flag) =>
        NpcStates.Where(kv => kv.Value.Flag == flag).Select(kv => kv.Key).ToList();

    // 2. 按指示物找NPC
    public static List<int> FindByMarker(string key, int val) =>
        NpcStates.Where(kv => kv.Value.Marker[key] == val).Select(kv => kv.Key).ToList();

    public static void BatchByFlag(string flag, Action<NPC> action)
    {
        foreach (var kv in NpcStates)
            if (kv.Value.Flag == flag && Main.npc[kv.Key].active)
                action(Main.npc[kv.Key]);
    }

    public static void SetByFlag(string flag, string key, int val) => BatchByFlag(flag, npc => GetState(npc).Set(key, val));
    public static void AddByFlag(string flag, string key, int val) => BatchByFlag(flag, npc => GetState(npc).Add(key, val));
    #endregion
}
