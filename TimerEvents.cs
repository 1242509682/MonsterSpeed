using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;
using static MonsterSpeed.Configuration;
using static MonsterSpeed.FilePlayManager;
using static MonsterSpeed.MoveMod;

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
    public Conditions Condition { get; set; } = new Conditions() { NpcLift = "0,100" };

    [JsonProperty("修改防御", Order = -20)]
    public int Defense { get; set; } = 0;

    [JsonProperty("行动模式", Order = 1)]
    public MoveModeData MoveData { get; set; }

    [JsonProperty("AI赋值", Order = 50)]
    public AIModes AIMode { get; set; }
    [JsonProperty("生成怪物", Order = 51)]
    public List<SpawnNpcData> SpawnNPC { get; set; } = new List<SpawnNpcData>();
    [JsonProperty("生成弹幕", Order = 52)]
    public List<SpawnProjData> SendProj { get; set; } = new List<SpawnProjData>();
    [JsonProperty("发射物品", Order = 53)]
    public HashSet<int> ShootItemList { get; set; } = new HashSet<int>();
}

// 状态管理类
public class TimerState
{
    public int Index { get; set; } = 0; // 当前执行的事件索引
    public FilePlayState FileState { get; set; } = new FilePlayState(); // 文件播放状态管理
    public PauseState PauseState { get; set; } = new PauseState(); // 暂停状态管理
    public DateTime UpdateTimer { get; set; } = DateTime.UtcNow; // 事件更新时间戳
    public DateTime LastTextTime { get; set; } = DateTime.UtcNow; // 上次显示文本时间
    public MoveModeState MoveState { get; set; } = new MoveModeState(); // 移动模式状态
    public Dictionary<int, int> EventStopCounts { get; set; } = new Dictionary<int, int>(); // 事件执行次数统计
    public Dictionary<int, int> PlayCounts { get; set; } = new Dictionary<int, int>(); // 文件播放次数统计
}

// 暂停状态类
public class PauseState
{
    public bool Paused { get; set; } = false; // 是否处于暂停状态
    public DateTime StateTime { get; set; } = DateTime.MinValue; // 状态开始时间
    public double Duration { get; set; } = 0; // 状态持续时间
}

internal class TimerEvents
{
    #region 时间事件
    public static void TimerEvent(NPC npc, StringBuilder mess, NpcData data, ref bool handled)
    {
        if (data?.TimerEvent == null || data.TimerEvent.Count <= 0) return;

        var state = GetState(npc);
        var Event = data.TimerEvent[state!.Index];

        // 文件播放器处理
        if (state.FileState.Playing)
        {
            HandleFilePlay(npc, mess, data, state, ref handled);
            return;
        }

        // 暂停检查 - 如果是强制播放，跳过暂停
        if (Event.PauseTime > 0 && !Event.NoCond)
        {
            PauseMode(npc, mess, data, state, Event);
            // 如果在暂停状态，阻止除NextEvent外的其他逻辑
            if (state.PauseState.Paused)
            {
                // 但仍然检查事件冷却，允许NextEvent切换
                if ((DateTime.UtcNow - state.UpdateTimer).TotalSeconds >= data.ActiveTime)
                {
                    // 强制播放文件检查（即使在暂停状态下也允许）
                    if (Event.FilePlayList != null && Event.FilePlayList.Count > 0 && Event.PlayCount != 0 && Event.NoCond)
                    {
                        StartFilePlay(npc.FullName, Event.FilePlayList, Event.PlayCount, Event.NoCond, Event.ByFile, state);
                    }
                    else
                    {
                        // 正常切换到下一个事件
                        NextEvent(data, Event.NextAddTimer, npc, state);
                    }
                }
                return;
            }
        }

        // 显示冷却文本
        ShowCoolText(npc, data, state);

        // 事件冷却检查
        if ((DateTime.UtcNow - state.UpdateTimer) >= TimeSpan.FromSeconds(data.ActiveTime))
        {
            // 启动文件播放器
            if (Event.FilePlayList != null && Event.FilePlayList.Count > 0 && Event.PlayCount != 0)
            {
                // 检查条件是否满足
                bool all = true;
                bool loop = false;
                if (!Event.NoCond) // 只有非强制播放时才检查条件
                {
                    Conditions.Condition(npc, mess, data, Event, ref all, ref loop);
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
            Conditions.Condition(npc, mess, data, Event, ref all, ref loop);

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
                StartEvent(data, npc, Event, mess, ref handled);
            }
        }

        if (!state.PauseState.Paused)
        {
            AppendStatusInfo(npc, mess, data, state, Event);
        }
    }
    #endregion

    #region 非暂停模式下状态信息显示
    public static void AppendStatusInfo(NPC npc, StringBuilder mess, NpcData data, TimerState state, TimerData Event)
    {
        int spCount = MyProjectile.GetState(npc)?.SPCount ?? 0;
        int snCount = MyMonster.GetState(npc)?.SNCount ?? 0;
        var life = (int)(npc.life / (float)npc.lifeMax * 100);

        mess.Append($" 顺序:[c/A2E4DB:{state.Index + 1}/{data.TimerEvent.Count}] 血量:[c/A2E4DB:{life}%] 召怪:[c/A2E4DB:{snCount}] 弹发:[c/A2E4DB:{spCount}]\n");
    }
    #endregion

    #region 暂停模式
    public static void PauseMode(NPC npc, StringBuilder mess, NpcData data, TimerState state, TimerData Event)
    {
        PauseState pause = state.PauseState;

        // 初始化暂停状态
        if (!pause.Paused && pause.StateTime == DateTime.MinValue)
        {
            pause.Paused = true;
            pause.StateTime = DateTime.UtcNow;
            pause.Duration = Event.PauseTime;
        }

        // 计算经过的时间（秒）
        double elapsedSeconds = (DateTime.UtcNow - pause.StateTime).TotalSeconds;

        if (pause.Paused)
        {
            // 暂停状态：检查是否应该切换到释放状态
            if (elapsedSeconds >= pause.Duration)
            {
                // 暂停时间结束，切换到释放状态
                pause.Paused = false;
                pause.StateTime = DateTime.UtcNow;

                // 如果释放时间为0，则使用与暂停相同的时间（1:1循环）
                // 如果释放时间>0，使用设置的释放时间
                pause.Duration = Event.ReleaseTime > 0 ? Event.ReleaseTime : Event.PauseTime;

                // 状态切换后立即返回，让后续逻辑有机会执行
                return;
            }
            else
            {
                // 仍在暂停中，显示信息并返回
                ShowCoolText(npc, data, state);
                var remain = pause.Duration - elapsedSeconds;
                var life = (int)(npc.life / (float)npc.lifeMax * 100);
                mess.Append($" 顺序:[c/A2E4DB:{state.Index + 1}/{data.TimerEvent.Count}] 血量:[c/A2E4DB:{life}%] 暂停剩余:[c/A2E4DB:{remain:F1}秒]\n");

                // 暂停期间只允许强制播放文件，其他事件逻辑被阻止
                // 但允许NextEvent切换（在TimerEvent中处理）
                return;
            }
        }
        else
        {
            // 释放状态：检查是否应该切换到暂停状态
            if (elapsedSeconds >= pause.Duration)
            {
                // 释放时间结束，切换回暂停状态
                pause.Paused = true;
                pause.StateTime = DateTime.UtcNow;
                pause.Duration = Event.PauseTime;

                // 显示状态切换信息
                ShowCoolText(npc, data, state);
                var life = (int)(npc.life / (float)npc.lifeMax * 100);
                mess.Append($" 顺序:[c/A2E4DB:{state.Index + 1}/{data.TimerEvent.Count}] 血量:[c/A2E4DB:{life}%] 释放结束，进入暂停\n");
                return;
            }
            // 释放状态下不返回，允许执行后续事件逻辑
        }
    }
    #endregion

    #region 让计数器自动前进到下一个事件
    public static void NextEvent(NpcData data, int timer, NPC npc, TimerState state)
    {
        // 更新当前事件的执行次数
        UpdateEventExecuteCount(state, state.Index);
        state.PauseState = new PauseState();
        state.MoveState = new MoveModeState();
        state.FileState = new FilePlayState();
        state.LastTextTime = DateTime.UtcNow;
        state.Index = (state.Index + 1) % data.TimerEvent.Count;
        var addTime = timer >= 0 ? timer : 0;

        state.UpdateTimer = DateTime.UtcNow.AddSeconds(addTime);
    }
    #endregion

    #region 更新事件执行次数
    private static void UpdateEventExecuteCount(TimerState state, int Index)
    {
        if (state.EventStopCounts == null)
        {
            state.EventStopCounts = new Dictionary<int, int>();
        }

        if (state.EventStopCounts.ContainsKey(Index))
        {
            state.EventStopCounts[Index]++;
        }
        else
        {
            state.EventStopCounts[Index] = 1;
        }
    }
    #endregion

    #region 执行事件逻辑
    public static void StartEvent(NpcData data, NPC npc, TimerData Event, StringBuilder mess, ref bool handled)
    {
        // 移动模式处理（新增）
        HandleMoveMode(npc, Event, mess, ref handled);

        if (Event.AIMode != null)
            AISystem.AIPairs(npc, Event.AIMode, npc.FullName, ref handled);

        if (Event.SpawnNPC != null && Event.SpawnNPC.Count > 0)
            MyMonster.SpawnMonsters(Event.SpawnNPC, npc);

        if (Event.SendProj != null && Event.SendProj.Count > 0)
            MyProjectile.SpawnProjectile(Event.SendProj, npc);

        if (Event.ShootItemList != null)
        {
            foreach (var item in Event.ShootItemList)
                npc.AI_87_BigMimic_ShootItem(item);
        }

        if (Event.AIMode?.BossAI != null)
        {
            foreach (var bossAI in Event.AIMode.BossAI)
                AISystem.TR_AI(bossAI, npc, ref handled);
        }

        npc.defense = Event.Defense > 0 ? Event.Defense : npc.defDefense;
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
            var time = GetActiveTime(fs, data);
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
    // 获取或创建NPC的状态
    public static TimerState? GetState(NPC npc)
    {
        if (npc == null || !npc.active)
            return new TimerState();

        if (!TimerStates.ContainsKey(npc.whoAmI))
        {
            var state = new TimerState();

            // 初始化移动状态
            state.MoveState.DashStartPosition = npc.Center;

            TimerStates[npc.whoAmI] = state;
        }

        return TimerStates[npc.whoAmI];
    }

    // 清理NPC的状态
    public static void ClearStates(NPC npc)
    {
        if (npc != null)
        {
            if (TimerStates.ContainsKey(npc.whoAmI))
            {
                TimerStates.Remove(npc.whoAmI);
            }
        }
    }

    // 清理所有状态（用于重置）
    public static void ClearAllStates()
    {
        TimerStates.Clear();
    }
    #endregion
}