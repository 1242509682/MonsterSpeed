using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;
using static MonsterSpeed.Configuration;
using static MonsterSpeed.FilePlay;

namespace MonsterSpeed;

//时间事件数据结构
public class TimerData
{
    [JsonProperty("文件播放器", Order = -103)]
    public List<int> FilePlayList { get; set; } = new List<int>();
    [JsonProperty("播放次数", Order = -102)]
    public int PlayCount { get; set; } = 0;
    [JsonProperty("强制播放", Order = -101)]
    public bool NoCond { get; set; } = false;
    [JsonProperty("限次播放", Order = -100)]
    public bool ByFile { get; set; } = false;
    [JsonProperty("暂停间隔", Order = -99)]
    public double PauseTime { get; set; }
    [JsonProperty("释放间隔", Order = -98)]
    public double ReleaseTime { get; set; }

    [JsonProperty("下组事件延长秒数", Order = -97)]
    public int NextAddTimer { get; set; } = 0;

    [JsonProperty("触发条件", Order = -50)]
    public Conditions Condition { get; set; } = new Conditions(){ NpcLift = "0,100" };

    [JsonProperty("修改防御", Order = -8)]
    public int Defense { get; set; } = 0;

    [JsonProperty("AI赋值", Order = 0)]
    public AIModes AIMode { get; set; }

    [JsonProperty("生成怪物", Order = 3)]
    public List<SpawnNpcData> SpawnNPC { get; set; } = new List<SpawnNpcData>();
    [JsonProperty("生成弹幕", Order = 4)]
    public List<ProjData> SendProj { get; set; } = new List<ProjData>();
    [JsonProperty("发射物品", Order = 5)]
    public HashSet<int> ShootItemList { get; set; } = new HashSet<int>();
}

// 状态管理类
public class TimerState
{
    public FilePlayState FileState { get; set; } = new FilePlayState(); // 文件播放状态管理
    public PauseState PauseState { get; set; } = new PauseState(); // 暂停状态管理
    public int Index { get; set; } = 0; // 当前执行的事件索引
    public DateTime UpdateTimer { get; set; } = DateTime.UtcNow; // 事件更新时间戳
    public DateTime LastTextTime { get; set; } = DateTime.UtcNow; // 上次显示文本时间
}

// 暂停状态类
public class PauseState
{
    public bool Paused { get; set; } = false; // 是否处于暂停状态
    public DateTime StateTime { get; set; } = DateTime.UtcNow; // 状态开始时间
    public double Duration { get; set; } = 0; // 状态持续时间
}

internal class TimerEvents
{
    #region 时间事件
    public static void TimerEvent(NPC npc, StringBuilder mess, NpcData data, Vector2 dict, float range, ref bool handled)
    {
        if (data?.TimerEvent == null || data.TimerEvent.Count <= 0) return;

        var state = GetState(npc);
        var life = (int)(npc.life / (float)npc.lifeMax * 100);
        var Event = data.TimerEvent[state!.Index];

        // 文件播放器处理
        if (state.FileState.Playing)
        {
            HandleFilePlay(npc, mess, data, life, state, ref handled);
            return;
        }

        // 暂停检查 - 如果是强制播放，跳过暂停
        if (Event.PauseTime > 0 && !Event.NoCond)
        {
            PauseMode(npc, mess, data, state, life, Event);
            return;
        }

        // 显示冷却文本
        ShowCoolText(npc, data, state);

        // 事件冷却检查
        if ((DateTime.UtcNow - state.UpdateTimer).TotalSeconds >= data.ActiveTime)
        {
            // 启动文件播放器
            if (Event.FilePlayList != null && Event.FilePlayList.Count > 0 && Event.PlayCount != 0)
            {
                // 检查条件是否满足
                bool all = true;
                bool loop = false;
                if (!Event.NoCond) // 只有非强制播放时才检查条件
                {
                    Conditions.Condition(npc, mess, data, range, life, Event, ref all, ref loop);
                }

                // 强制播放 或 条件满足时启动文件播放器
                if (Event.NoCond || all)
                {
                    StartFilePlay(npc.FullName, Event.FilePlayList, Event.PlayCount, Event.NoCond, Event.ByFile, state);
                    return;
                }
                else
                {
                    NextEvent(data, Event.NextAddTimer, npc, state);
                }
            }
            else
            {
                NextEvent(data, Event.NextAddTimer, npc, state);
            }
        }

        // 事件条件检查
        if (Event != null)
        {
            var all = true;
            var loop = false;
            Conditions.Condition(npc, mess, data, range, life, Event, ref all, ref loop);

            if (data.Loop && loop)
            {
                // 检查是否有文件播放器，如果有则根据强制播放决定是否跳过
                if (Event.FilePlayList != null && Event.FilePlayList.Count > 0 && Event.PlayCount != 0 && Event.NoCond)
                {
                    StartFilePlay(npc.FullName, Event.FilePlayList, Event.PlayCount, Event.NoCond, Event.ByFile, state);
                }
                else
                {
                    // 没有文件播放器，正常跳过
                    NextEvent(data, Event.NextAddTimer, npc, state);
                    return;
                }
            }

            if (all)
            {
                StartEvent(npc, Event, ref handled);
            }
        }

        if (!state.PauseState.Paused)
        {
            int spCount = MyProjectile.GetState(npc)?.SPCount ?? 0;
            int snCount = MyMonster.GetState(npc)?.SNCount ?? 0;
            mess.Append($" 顺序:[c/A2E4DB:{state.Index + 1}/{data.TimerEvent.Count}] 血量:[c/A2E4DB:{life}%] 召怪:[c/A2E4DB:{snCount}] 弹发:[c/A2E4DB:{spCount}]\n");
        }
    }
    #endregion
    
    #region 暂停模式
    public static void PauseMode(NPC npc, StringBuilder mess, NpcData data, TimerState state, int life, TimerData Event)
    {
        PauseState pause = state.PauseState;
        if (!pause.Paused)
        {
            pause.Paused = true;
            pause.StateTime = DateTime.UtcNow;
            pause.Duration = Event.PauseTime;
        }

        double elapsed = (DateTime.UtcNow - pause.StateTime).TotalMilliseconds;
        double reTime = Event.ReleaseTime > 0 ? Event.ReleaseTime : Event.PauseTime;

        if (elapsed >= (pause.Paused ? Event.PauseTime : reTime))
        {
            pause.Paused = !pause.Paused;
            pause.StateTime = DateTime.UtcNow;
            pause.Duration = pause.Paused ? Event.PauseTime : reTime;
        }

        if (pause.Paused)
        {
            ShowCoolText(npc, data, state);
            var remain = pause.Duration - (DateTime.UtcNow - pause.StateTime).TotalMilliseconds;
            mess.Append($" 顺序:[c/A2E4DB:{state.Index + 1}/{data.TimerEvent.Count}] 血量:[c/A2E4DB:{life}%] 暂停剩余:[c/A2E4DB:{remain:F0}毫秒]\n");
            return;
        }
    }
    #endregion

    #region 让计数器自动前进到下一个事件
    public static void NextEvent(NpcData data, int timer, NPC npc, TimerState state)
    {
        state.PauseState = new PauseState();
        state.Index = (state.Index + 1) % data.TimerEvent.Count;
        var addTime = timer >= 0 ? timer : 0;

        state.UpdateTimer = DateTime.UtcNow.AddSeconds(addTime);
    }
    #endregion

    #region 执行事件逻辑
    public static void StartEvent(NPC npc, TimerData evt, ref bool handled)
    {
        if (evt.AIMode != null)
            AISystem.AIPairs(npc, evt.AIMode, npc.FullName, ref handled);

        if (evt.SpawnNPC != null && evt.SpawnNPC.Count > 0)
            MyMonster.SpawnMonsters(evt.SpawnNPC, npc);

        if (evt.SendProj != null && evt.SendProj.Count > 0)
            MyProjectile.SpawnProjectile(evt.SendProj, npc);

        if (evt.ShootItemList != null)
        {
            foreach (var item in evt.ShootItemList)
                npc.AI_87_BigMimic_ShootItem(item);
        }

        if (evt.AIMode?.BossAI != null)
        {
            foreach (var bossAI in evt.AIMode.BossAI)
                AISystem.TR_AI(bossAI, npc, ref handled);
        }

        npc.defense = evt.Defense > 0 ? evt.Defense : npc.defDefense;
    }
    #endregion

    #region 时间事件冷却倒计时方法（悬浮文本）
    public static void ShowCoolText(NPC npc, NpcData data, TimerState state)
    {
        if ((DateTime.UtcNow - state.LastTextTime).TotalMilliseconds < data.TextInterval)
            return;

        TimeSpan ActionTime;
        string text;
        Color color;

        if (state.PauseState.Paused)
        {
            // 暂停状态下使用主事件计时器
            ActionTime = TimeSpan.FromSeconds(data.ActiveTime) - (DateTime.UtcNow - state.UpdateTimer);
            text = $"Pass {ActionTime.TotalSeconds:F2}";
            color = Color.Red;
        }
        else if (state.FileState.Playing)
        {
            // 文件播放模式
            var fs = state.FileState;
            var time = data.ActiveTime + fs.MoreActiveTime;
            ActionTime = TimeSpan.FromSeconds(time) - (DateTime.UtcNow - fs.EventTimer);
            text = $"File {ActionTime.TotalSeconds:F2} [{fs.FileIndex + 1}/{fs.FileSeq.Count}]";
            color = Color.Blue;
        }
        else
        {
            // 正常主事件模式
            ActionTime = TimeSpan.FromSeconds(data.ActiveTime) - (DateTime.UtcNow - state.UpdateTimer);
            text = $"Time {ActionTime.TotalSeconds:F2} [{state.Index + 1}/{data.TimerEvent?.Count ?? 1}]";
            color = Color.LightGoldenrodYellow;
        }

        if (!data.TextGradient)
        {
            TSPlayer.All.SendData(PacketTypes.CreateCombatTextExtended, text,
                (int)color.PackedValue, npc.position.X, npc.position.Y - 3, 0f, 0);
        }
        else
        {
            for (int i = 0; i < text.Length; i++)
            {
                var start = new Color(166, 213, 234);
                var end = new Color(245, 247, 175);
                float ratio = (float)i / (text.Length - 1);
                var gradColor = Color.Lerp(start, end, ratio);

                TSPlayer.All.SendData(PacketTypes.CreateCombatTextExtended, text[i].ToString(),
                                     (int)gradColor.PackedValue,
                                     npc.position.X + (i * data.TextRange),
                                     npc.position.Y - 3, 0f, 0);
            }
        }

        state.LastTextTime = DateTime.UtcNow;
    }
    #endregion

    #region 状态管理
    public static readonly Dictionary<int, TimerState> TimerStates = new();

    /// <summary>
    /// 获取或创建NPC的状态
    /// </summary>
    /// <param name="npc">NPC实例</param>
    /// <returns>状态对象</returns>
    public static TimerState? GetState(NPC npc)
    {
        if (npc == null || !npc.active)
            return new TimerState();

        if (!TimerStates.ContainsKey(npc.whoAmI))
            TimerStates[npc.whoAmI] = new TimerState();
        return TimerStates[npc.whoAmI];
    }

    /// <summary>
    /// 清理NPC的状态
    /// </summary>
    /// <param name="npc">NPC实例</param>
    public static void ClearStates(NPC npc)
    {
        if (npc != null && TimerStates.ContainsKey(npc.whoAmI))
        {
            TimerStates.Remove(npc.whoAmI);
        }
    }

    /// <summary>
    /// 清理所有状态（用于重置）
    /// </summary>
    public static void ClearAllStates()
    {
        TimerStates.Clear();
    }
    #endregion
}