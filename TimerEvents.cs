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

    [JsonProperty("播放次数", Order = -102)]
    public int PlayCount { get; set; } = 0;

    [JsonProperty("文件播放器", Order = -101)]
    public List<int> FilePlayList { get; set; } = new List<int>();

    [JsonProperty("下组事件延长秒数", Order = -100)]
    public int Timer { get; set; } = 0;

    [JsonProperty("暂停间隔", Order = -99)]
    public double PauseTime { get; set; }
    [JsonProperty("释放间隔", Order = -98)]
    public double ReleaseTime { get; set; }

    [JsonProperty("触发条件", Order = -50)]
    public List<Conditions> Condition { get; set; }
    [JsonProperty("修改防御", Order = -8)]
    public int Defense { get; set; } = 0;

    [JsonProperty("AI赋值", Order = 0)]
    public AIModes AIMode { get; set; } = new AIModes();

    [JsonProperty("生成怪物", Order = 3)]
    public List<SpawnNpcData> SpawnNPC { get; set; } = new List<SpawnNpcData>();
    [JsonProperty("生成弹幕", Order = 4)]
    public List<ProjData> SendProj { get; set; } = new List<ProjData>();
    [JsonProperty("发射物品", Order = 5)]
    public HashSet<int> ShootItemList { get; set; } = new HashSet<int>();
}

// 暂停状态类
public class PauseState
{
    public bool InPause { get; set; } = false;
    public DateTime PauseTime { get; set; } = DateTime.UtcNow;
    public double StateDuration { get; set; } = 0;
}

internal class TimerEvents
{
    #region 时间事件
    public static void TimerEvent(NPC npc, StringBuilder mess, NpcData? data, Vector2 dict, float range)
    {
        if (data == null || data.TimerEvent == null || data.TimerEvent.Count <= 0) return;

        var (CD_Count, CD_Timer) = GetIndex_SetTime(npc.FullName);

        var life = (int)(npc.life / (float)npc.lifeMax * 100);

        // 检查文件播放器状态
        if (FilePlayStates.ContainsKey(npc.FullName) && FilePlayStates[npc.FullName].IsPlaying)
        {
            HandleFilePlay(npc, mess, data, life);
            return;
        }

        //更新计数器和时间 跳转下一个事件
        var Event = data.TimerEvent[CD_Count];

        // 检查暂停模式
        if (ShouldPause(npc.FullName, Event))
        {
            // 在暂停期间，只显示冷却文本，不执行事件逻辑
            TextExtended(npc, data, CD_Timer);
            var remaining = GetReTime(npc.FullName, Event);
            mess.Append($" 顺序:[c/A2E4DB:{CD_Count + 1}/{data.TimerEvent.Count}] 血量:[c/A2E4DB:{life}%] [暂停剩余:{remaining:F0}ms]\n");
            return;
        }

        //时间事件冷却倒计时（悬浮文本）
        TextExtended(npc, data, CD_Timer);

        if ((DateTime.UtcNow - CD_Timer).TotalSeconds >= data.ActiveTime)
        {
            // 检查是否需要启动文件播放器
            if (Event.FilePlayList != null && Event.FilePlayList.Count > 0 && Event.PlayCount != 0)
            {
                StartFilePlay(npc.FullName, Event.FilePlayList, Event.PlayCount);
                return;
            }
            else
            {
                NextEvent(ref CD_Count, ref CD_Timer, data, Event.Timer, npc.FullName);
            }
        }

        // 时间事件
        if (Event != null)
        {
            var all = true; //达成所有条件标识
            var loop = false;

            //触发条件
            Conditions.Condition(npc, mess, data, range, life, Event, ref all, ref loop);

            //循环执行
            if (data.Loop && loop)
            {
                NextEvent(ref CD_Count, ref CD_Timer, data, Event.Timer, npc.FullName);
            }

            // 满足所有条件
            if (all)
            {
                ExecuteEvent(npc, Event);
            }
        }

        mess.Append($" 顺序:[c/A2E4DB:{CD_Count + 1}/{data.TimerEvent.Count}] 血量:[c/A2E4DB:{life}%]" +
        $" 召怪:[c/A2E4DB:{MyMonster.SNCount}] 弹发:[c/A2E4DB:{MyProjectile.SPCount}]\n");
    }
    #endregion

    #region 执行事件逻辑（提取为独立方法）
    public static void ExecuteEvent(NPC npc, TimerData Event)
    {
        // 修改AI模式
        if (Event.AIMode != null)
        {
            AISystem.AIPairs(npc, Event.AIMode, npc.FullName);
        }

        // 召唤怪物
        if (Event.SpawnNPC != null && Event.SpawnNPC.Count > 0)
        {
            MyMonster.SpawnMonsters(Event.SpawnNPC, npc);
        }

        // 生成弹幕
        if (Event.SendProj != null && Event.SendProj.Count > 0)
        {
            MyProjectile.SpawnProjectile(Event.SendProj, npc);
        }

        //持续发射物品(指定物品ID)
        if (Event.ShootItemList != null)
        {
            foreach (var item in Event.ShootItemList)
            {
                npc.AI_87_BigMimic_ShootItem(item);
            }
        }

        // BossAi
        if (Event.AIMode != null && Event.AIMode.BossAI != null)
        {
            foreach (var bossAI in Event.AIMode.BossAI)
            {
                if (bossAI != null)
                {
                    AISystem.TR_AI(bossAI, npc);
                }
            }
        }

        // 修改防御
        if (Event.Defense > 0)
        {
            if (npc.defense != Event.Defense)
            {
                npc.defense = Event.Defense;
            }
        }
        else //否则恢复默认
        {
            npc.defense = npc.defDefense;
        }
    }
    #endregion

    #region 冷却计数与更新冷却时间方法
    public static readonly Dictionary<string, (int CDCount, DateTime UpdateTimer)> CoolTrack = new();
    public static (int CDCount, DateTime UpdateTimer) GetIndex_SetTime(string key)
    {
        return CoolTrack.TryGetValue(key, out var value) ? value : (CoolTrack[key] = (0, DateTime.UtcNow));
    }
    public static void UpdateTrack(string key, int cdCount, DateTime updateTimer)
    {
        CoolTrack[key] = (cdCount, updateTimer);
    }
    #endregion

    #region 让计数器自动前进到下一个事件
    public static void NextEvent(ref int CD_Count, ref DateTime CD_Timer, NpcData data, int Timer, string npcName)
    {
        if (data.TimerEvent != null && data.TimerEvent.Count > 0)
        {
            // 重置暂停状态
            if (PauseStates.ContainsKey(npcName))
            {
                PauseStates[npcName] = new PauseState();
            }

            // 更新计数器以指向下一个事件
            CD_Count = (CD_Count + 1) % data.TimerEvent.Count;

            // 如果当前周期有指定的延长秒数，则使用它来更新冷却时间；否则使用默认值0
            var Add = Timer >= 0 ? Timer : 0;

            // 获取当前UTC时间并加上指定的秒数作为新的冷却结束时间
            CD_Timer = DateTime.UtcNow.AddSeconds(Add);

            // 更新时间
            UpdateTrack(npcName, CD_Count, CD_Timer);
        }
    }
    #endregion

    #region 时间事件冷却倒计时方法（悬浮文本）
    private static Dictionary<string, DateTime> TextTime = new Dictionary<string, DateTime>();// 跟踪每个NPC上次冷却记录时间
    private static void TextExtended(NPC npc, NpcData? data, DateTime CD_Timer)
    {
        if (data == null) return;

        if (!TextTime.ContainsKey(npc.FullName))
        {
            TextTime[npc.FullName] = DateTime.UtcNow;
        }

        if ((DateTime.UtcNow - TextTime[npc.FullName]).TotalMilliseconds >= data.TextInterval)
        {
            var ActionTimer = TimeSpan.FromSeconds(data.ActiveTime) - (DateTime.UtcNow - CD_Timer);

            // 获取当前事件序号
            var (CD_Count, _) = GetIndex_SetTime(npc.FullName);
            int EventIndex = CD_Count + 1;
            int TotalEvents = data.TimerEvent?.Count ?? 1;

            // 检查是否处于暂停状态
            bool pass = PauseStates.ContainsKey(npc.FullName) && PauseStates[npc.FullName].InPause;
            var Status = pass ? "[pass] " : "";

            // 检查是否处于文件播放状态
            bool filePlaying = FilePlayStates.ContainsKey(npc.FullName) && FilePlayStates[npc.FullName].IsPlaying;
            if (filePlaying)
            {
                var fileState = FilePlayStates[npc.FullName];
                Status = $"[文件播放] 文件:{fileState.CurrentFileIndex + 1}/{fileState.FileSequence.Count} ";
            }

            // 构建包含事件序号的文本
            string Text = $"{Status}Time {ActionTimer.TotalSeconds:F2} [{EventIndex}/{TotalEvents}]";

            var color = new Color();

            // 是否启用倒计时渐变色
            if (!data.TextGradient)
            {
                color = pass ? Color.Red : Color.LightGoldenrodYellow;
                TSPlayer.All.SendData(PacketTypes.CreateCombatTextExtended, Text,
                    (int)color.PackedValue, npc.position.X, npc.position.Y - 3, 0f, 0);
            }
            else
            {
                // 将文本拆分成多个部分，每个部分使用不同的颜色
                for (int i = 0; i < Text.Length; i++)
                {
                    var start = new Color(166, 213, 234);
                    var end = new Color(245, 247, 175);
                    float ratio = (float)i / (Text.Length - 1);
                    color = Color.Lerp(start, end, ratio);

                    // 发送单个字符（会产生多个悬浮文本）
                    TSPlayer.All.SendData(PacketTypes.CreateCombatTextExtended, Text[i].ToString(),
                                         (int)color.PackedValue,
                                         npc.position.X + (i * 16), // 水平偏移
                                         npc.position.Y - 3, 0f, 0);
                }
            }

            TextTime[npc.FullName] = DateTime.UtcNow;
        }
    }
    #endregion

    #region 改进暂停控制（支持自定义释放时间）
    private static Dictionary<string, PauseState> PauseStates = new Dictionary<string, PauseState>();
    private static bool ShouldPause(string npcName, TimerData Event)
    {
        // 如果暂停时间为0，不进行暂停
        if (Event.PauseTime <= 0) return false;

        // 计算释放时间，如果未设置则使用暂停时间（1:1）
        double ReTime = Event.ReleaseTime > 0 ? Event.ReleaseTime : Event.PauseTime;

        // 初始化或获取状态
        if (!PauseStates.ContainsKey(npcName))
        {
            PauseStates[npcName] = new PauseState
            {
                InPause = true, // 默认从暂停状态开始
                PauseTime = DateTime.UtcNow,
                StateDuration = Event.PauseTime
            };
        }

        var state = PauseStates[npcName];
        var elapsed = (DateTime.UtcNow - state.PauseTime).TotalMilliseconds;
        var Duration = state.InPause ? Event.PauseTime : ReTime;

        // 检查是否应该切换状态
        if (elapsed >= Duration)
        {
            // 切换状态并重置计时器
            state.InPause = !state.InPause;
            state.PauseTime = DateTime.UtcNow;
            state.StateDuration = state.InPause ? Event.PauseTime : ReTime;
        }

        return state.InPause;
    }
    #endregion

    #region 获取剩余暂停时间
    private static double GetReTime(string npcName, TimerData Event)
    {
        if (!PauseStates.ContainsKey(npcName)) return 0;

        var state = PauseStates[npcName];
        var elapsed = (DateTime.UtcNow - state.PauseTime).TotalMilliseconds;
        return state.StateDuration - elapsed;
    }
    #endregion

}
