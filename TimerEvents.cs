using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;
using static MonsterSpeed.Configuration;

namespace MonsterSpeed;

//时间事件数据结构
public class TimerData
{
    [JsonProperty("下组事件延长秒数", Order = -100)]
    public int Timer { get; set; } = 0;
    [JsonProperty("暂停间隔", Order = -99)]
    public double PauseTime { get; set; }
    [JsonProperty("触发条件", Order = -50)]
    public List<Conditions> Condition { get; set; }
    [JsonProperty("修改防御", Order = -8)]
    public int Defense { get; set; } = 0;

    [JsonProperty("原版AI", Order = 0)]
    public List<BossAI> BossAI { get; set; } = new List<BossAI>();

    [JsonProperty("AI赋值", Order = 2)]
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
}

internal class TimerEvents
{
    #region 时间事件
    public static void TimerEvent(NPC npc, StringBuilder mess, NpcData? data, Vector2 dict, float range)
    {
        if (data == null || data.TimerEvent == null || data.TimerEvent.Count <= 0) return;

        var (CD_Count, CD_Timer) = GetOrAdd(npc.FullName);

        var life = (int)(npc.life / (float)npc.lifeMax * 100);

        //更新计数器和时间 跳转下一个事件
        var Event = data.TimerEvent[CD_Count];

        // 检查暂停模式
        if (ShouldPause(npc.FullName, Event))
        {
            // 直接计算剩余暂停时间
            var state = PauseStates[npc.FullName];
            var elapsed = (DateTime.UtcNow - state.PauseTime).TotalMilliseconds;
            var remaining = Event.PauseTime - elapsed;

            // 在暂停期间，只显示冷却文本，不执行事件逻辑
            TextExtended(npc, data, CD_Timer);
            mess.Append($" 顺序:[c/A2E4DB:{CD_Count + 1}/{data.TimerEvent.Count}] 血量:[c/A2E4DB:{life}%] [暂停剩余:{remaining:F0}ms]\n");
            return;
        }

        //时间事件冷却倒计时（悬浮文本）
        TextExtended(npc, data, CD_Timer);

        if ((DateTime.UtcNow - CD_Timer).TotalSeconds >= data.ActiveTime)
        {
            NextEvent(ref CD_Count, ref CD_Timer, data, Event.Timer, npc.FullName);
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
                // 修改AI模式
                if (Event.AIMode != null)
                {
                    // AI赋值（修改为支持模式控制）
                    AISystem.AIPairs(npc, Event.AIMode, npc.FullName);

                    // AI赋值监控
                    if (Event.AIMode.Enabled)
                    {
                        var AiInfo = AISystem.GetAiInfo(Event.AIMode, npc.FullName);
                        mess.Append($" ai赋值:[c/A2E4DB:{AiInfo}]\n");
                    }
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
                if (Event.BossAI != null)
                {
                    foreach (var bossAI in Event.BossAI)
                    {
                        if (bossAI != null)
                        {
                            TR_AI(bossAI, npc);
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
        }

        mess.Append($" 顺序:[c/A2E4DB:{CD_Count + 1}/{data.TimerEvent.Count}] 血量:[c/A2E4DB:{life}%]" +
        $" 召怪:[c/A2E4DB:{MyMonster.SNCount}] 弹发:[c/A2E4DB:{MyProjectile.SPCount}]\n");
    }
    #endregion

    #region 冷却计数与更新冷却时间方法
    public static readonly Dictionary<string, (int CDCount, DateTime UpdateTimer)> CoolTrack = new();
    public static (int CDCount, DateTime UpdateTimer) GetOrAdd(string key)
    {
        return CoolTrack.TryGetValue(key, out var value) ? value : (CoolTrack[key] = (0, DateTime.UtcNow));
    }
    public static void UpdateTrack(string key, int cdCount, DateTime updateTimer)
    {
        CoolTrack[key] = (cdCount, updateTimer);
    }
    #endregion

    #region 让计数器自动前进到下一个事件
    private static void NextEvent(ref int CD_Count, ref DateTime CD_Timer, NpcData data, int Timer, string npcName)
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

            // 检查是否处于暂停状态
            bool pass = PauseStates.ContainsKey(npc.FullName) && PauseStates[npc.FullName].InPause;
            var color = pass ? Color.Red : Color.LightGoldenrodYellow;
            var Status = pass ? "[pass] " : "";

            TSPlayer.All.SendData(PacketTypes.CreateCombatTextExtended, $"{Status}Time {ActionTimer.TotalSeconds:F2}",
                                 (int)color.PackedValue, npc.position.X, npc.position.Y - 3, 0f, 0);

            TextTime[npc.FullName] = DateTime.UtcNow;
        }
    }
    #endregion

    #region 泰拉瑞亚 Boss AI
    private static void TR_AI(BossAI bossAI, NPC npc)
    {
        Player plr = Main.player[npc.target];

        //始终保持保持玩家头顶
        if (bossAI.AlwaysTop && !plr.dead && plr.active)
        {
            npc.AI_120_HallowBoss_DashTo(plr.position);
        }

        //猪鲨AI
        if (bossAI.DukeFishron)
        {
            npc.AI_069_DukeFishron();
        }

        //鹦鹉螺AI
        if (bossAI.BloodNautilus)
        {
            npc.AI_117_BloodNautilus();
        }

        //白光AI
        if (bossAI.HallowBoss)
        {
            npc.AI_120_HallowBoss();
        }

        //鹿角怪AI
        if (bossAI.Deerclops)
        {
            npc.AI_123_Deerclops();
        }
    }
    #endregion

    #region 简化暂停控制
    private static Dictionary<string, PauseState> PauseStates = new Dictionary<string, PauseState>();
    private static bool ShouldPause(string npcName, TimerData Event)
    {
        // 如果暂停时间为0，不进行暂停
        if (Event.PauseTime <= 0) return false;

        // 初始化或检查事件是否切换
        if (!PauseStates.ContainsKey(npcName))
        {
            PauseStates[npcName] = new PauseState
            {
                InPause = true, // 默认从暂停状态开始
                PauseTime = DateTime.UtcNow
            };
        }

        var state = PauseStates[npcName];
        var PauseTimer = (DateTime.UtcNow - state.PauseTime).TotalMilliseconds;

        // 检查是否应该切换状态
        if (PauseTimer >= Event.PauseTime)
        {
            // 切换状态
            state.InPause = !state.InPause;
            state.PauseTime = DateTime.UtcNow;
        }

        return state.InPause;
    }
    #endregion

}
