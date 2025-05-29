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

    [JsonProperty("触发条件", Order = -51)]
    public List<Conditions> Condition { get; set; }

    [JsonProperty("修改防御", Order = -10)]
    public int Defense { get; set; } = 0;
    [JsonProperty("回血间隔", Order = -9)]
    public int AutoHealInterval { get; set; } = 10;
    [JsonProperty("百分比回血", Order = -8)]
    public int AutoHeal { get; set; } = 1;
    [JsonProperty("白光AI", Order = -7)]
    public bool HallowBoss { get; set; } = false;
    [JsonProperty("猪鲨AI", Order = -6)]
    public bool DukeFishron { get; set; } = false;
    [JsonProperty("鹿角怪AI", Order = -5)]
    public bool Deerclops { get; set; } = false;
    [JsonProperty("鹦鹉螺AI", Order = -4)]
    public bool BloodNautilus { get; set; } = false;
    [JsonProperty("保持头顶", Order = -3)]
    public bool AlwaysTop { get; set; } = false;
    [JsonProperty("发射物品", Order = -2)]
    public HashSet<int> ShootItemList { get; set; } = new HashSet<int>();
    [JsonProperty("怪物AI", Order = 1)]
    public Dictionary<int, float> AIPairs { get; set; } = new Dictionary<int, float>();
    [JsonProperty("生成怪物", Order = 2)]
    public List<SpawnNpcData> SpawnNPC { get; set; } = new List<SpawnNpcData>();
    [JsonProperty("生成弹幕", Order = 3)]
    public List<ProjData> SendProj { get; set; } = new List<ProjData>();
}

internal class TimerEvents
{
    #region 时间事件
    public static void TimerEvent(NPC npc, StringBuilder mess, NpcData? data, Vector2 dict, float range)
    {
        if (data == null || data.TimerEvent == null || data.TimerEvent.Count <= 0) return;

        var (CD_Count, CD_Timer) = GetOrAdd(npc.FullName);

        var life = (int)(npc.life / (float)npc.lifeMax * 100);

        //时间事件冷却倒计时（悬浮文本）
        TextExtended(npc, data, CD_Timer);

        //更新计数器和时间 跳转下一个事件
        var Event = data.TimerEvent[CD_Count];
        if ((DateTime.UtcNow - CD_Timer).TotalSeconds >= data.CoolTimer)
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
                // AI赋值
                AIPairs(Event.AIPairs, npc);

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

                // 自动回血
                if (Event.AutoHeal > 0)
                {
                    AutoHeal(npc, Event);
                }

                // 监控
                if (Event.AIPairs.Count > 0)
                {
                    var AiInfo = AIPairsInfo(Event.AIPairs);
                    mess.Append($" ai赋值:[c/A2E4DB:{AiInfo}]\n");
                }

                TR_AI(Event, npc);
            }
        }

        mess.Append($" 顺序:[c/A2E4DB:{CD_Count + 1}/{data.TimerEvent.Count}] 血量:[c/A2E4DB:{life}%]" +
        $" 召怪:[c/A2E4DB:{MyMonster.SNCount}] 弹发:[c/A2E4DB:{MyProjectile.SPCount}]\n");
    }

    private static void TR_AI(TimerData Event, NPC npc)
    {
        Player plr = Main.player[npc.target];

        //猪鲨AI
        if (Event.DukeFishron)
        {
            npc.AI_069_DukeFishron();
        }

        //鹦鹉螺AI
        if (Event.BloodNautilus)
        {
            npc.AI_117_BloodNautilus();
        }

        //白光AI
        if (Event.HallowBoss)
        {
            npc.AI_120_HallowBoss();
        }

        //始终保持保持玩家头顶
        if (Event.AlwaysTop)
        {
            npc.AI_120_HallowBoss_DashTo(plr.position);
        }

        //鹿角怪AI
        if (Event.Deerclops)
        {
            npc.AI_123_Deerclops();
        }

        //持续发射物品(指定物品ID)
        if (Event.ShootItemList != null)
        {
            foreach (var item in Event.ShootItemList)
            {
                npc.AI_87_BigMimic_ShootItem(item);
            }
        }
    }
    #endregion

    #region 自动回血
    public static Dictionary<string, DateTime> HealTimes = new Dictionary<string, DateTime>(); // 跟踪每个NPC上次回血的时间
    public static void AutoHeal(NPC npc, TimerData Event)
    {
        if (!HealTimes.ContainsKey(npc.FullName))
        {
            HealTimes[npc.FullName] = DateTime.UtcNow.AddSeconds(-1); // 初始化为1秒前，确保第一次调用时立即回血
        }

        if ((DateTime.UtcNow - HealTimes[npc.FullName]).TotalMilliseconds >= Event.AutoHealInterval * 1000) // 回血间隔
        {
            // 将AutoHeal视为百分比并计算相应的生命值恢复量
            var num = (int)(npc.lifeMax * (Event.AutoHeal / 100.0f));
            npc.life = (int)Math.Min(npc.lifeMax, npc.life + num);
            HealTimes[npc.FullName] = DateTime.UtcNow;
        }
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

    #region 怪物AI赋值
    public static void AIPairs(Dictionary<int, float> Pairs, NPC npc)
    {
        if (Pairs == null || Pairs.Count == 0) return;

        foreach (var Pair in Pairs)
        {
            var i = Pair.Key;

            if (i >= 0 && i < npc.ai.Length)
            {
                npc.ai[i] = Pair.Value;
            }
        }
    }
    #endregion

    #region 输出正在赋值的AI信息
    public static string AIPairsInfo(Dictionary<int, float> Pairs)
    {
        if (Pairs == null || Pairs.Count == 0) return "无";
        var info = new StringBuilder();
        foreach (var Pair in Pairs)
        {
            info.Append($"ai{Pair.Key}_{Pair.Value:F0} ");
        }

        return info.ToString();
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
            var CoolTimer = TimeSpan.FromSeconds(data.CoolTimer) - (DateTime.UtcNow - CD_Timer);

            TSPlayer.All.SendData(PacketTypes.CreateCombatTextExtended, $"Time {CoolTimer.TotalSeconds:F2}",
                                 (int)Color.LightGoldenrodYellow.PackedValue, npc.position.X, npc.position.Y - 3, 0f, 0);

            TextTime[npc.FullName] = DateTime.UtcNow;
        }
    }
    #endregion

}
