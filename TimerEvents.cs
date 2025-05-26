using System.Text;
using Microsoft.Xna.Framework;
using MonsterSpeed.Progress;
using Newtonsoft.Json;
using Terraria;
using Terraria.ID;
using TShockAPI;
using static MonsterSpeed.Configuration;

namespace MonsterSpeed;

//时间事件数据结构
public class TimerData
{
    [JsonProperty("下组事件延长秒数", Order = -100)]
    public int Timer { get; set; } = 0;

    [JsonProperty("触发条件", Order = -51)]
    public List<ConditionData> Condition { get; set; }

    [JsonProperty("修改防御", Order = -10)]
    public int Defense { get; set; } = 0;
    [JsonProperty("回血间隔", Order = -9)]
    public int AutoHealInterval { get; set; } = 10;
    [JsonProperty("百分比回血", Order = -8)]
    public int AutoHeal { get; set; } = 1;


    [JsonProperty("怪物AI", Order = 1)]
    public Dictionary<int, float> AIPairs { get; set; } = new Dictionary<int, float>();
    [JsonProperty("生成怪物", Order = 2)]
    public List<SpawnNpcData> SpawnNPC { get; set; } = new List<SpawnNpcData>();
    [JsonProperty("生成弹幕", Order = 3)]
    public List<ProjData> SendProj { get; set; } = new List<ProjData>();
}

//触发条件数据结构
public class ConditionData
{
    [JsonProperty("血量范围", Order = -22)]
    public string NpcLift { get; set; } = "0,100";
    [JsonProperty("进度限制", Order = -21)]
    public ProgressType Progress { get; set; } = (ProgressType)(-1);
    [JsonProperty("玩家生命", Order = -20)]
    public int PlayerLife { get; set; } = -1;
    [JsonProperty("玩家防御", Order = -19)]
    public int PlrDefense { get; set; } = -1;
    [JsonProperty("玩家武器", Order = -18)]
    public string WeaponName { get; set; } = "无";
    [JsonProperty("召怪次数", Order = -17)]
    public int MonsterCount { get; set; } = -1;
    [JsonProperty("弹发次数", Order = -16)]
    public int ProjectileCount { get; set; } = -1;
    [JsonProperty("死亡次数", Order = -15)]
    public int DeadCount { get; set; } = -1;
    [JsonProperty("距离条件", Order = -14)]
    public float Range { get; set; } = -1;
    [JsonProperty("速度条件", Order = -13)]
    public float Speed { get; set; } = -1;
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
            Condition(npc, mess, data, range, life, Event, ref all, ref loop);

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
            }
        }

        mess.Append($" 顺序:[c/A2E4DB:{CD_Count + 1}/{data.TimerEvent.Count}] 血量:[c/A2E4DB:{life}%]" +
        $" 召怪:[c/A2E4DB:{MyMonster.SNCount}] 弹发:[c/A2E4DB:{MyProjectile.SPCount}]\n");
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

    #region 触发条件
    private static void Condition(NPC npc, StringBuilder mess, NpcData? data, float range, int life, TimerData Event, ref bool all, ref bool loop)
    {
        if (data is null) return;
        if (Event.Condition != null && Event.Condition.Count > 0)
        {
            foreach (var Condition in Event.Condition)
            {
                // 生命条件
                var LC = LifeCondition(life, Condition);
                if (!LC && Condition.NpcLift != "0,100")
                {
                    all = false;
                    loop = true;
                    mess.Append($" 血量条件未满足: 血量 {life}% < {Condition.NpcLift} \n");
                }

                // 武器条件
                var plr = TShock.Players.FirstOrDefault(p => p != null && p.Active && p.IsLoggedIn)!.TPlayer;
                var WC = Condition.WeaponName == GetPlayerWeapon(plr);
                if (Condition.WeaponName != "无" && !WC)
                {
                    all = false;
                    loop = true;
                    mess.Append($" 武器条件未满足: 玩家武器 {GetPlayerWeapon(plr)} 不是 {Condition.WeaponName}\n");
                    MonsterSpeed.AutoTar(npc, data); //自动转换仇恨目标
                }

                // 进度条件
                var PC = ProgressChecker.IsProgress(Condition.Progress);
                if (Condition.Progress != (ProgressType)(-1) && !PC)
                {
                    all = false;
                    loop = true;
                    mess.Append($" 进度条件未满足: 当前进度不符合 {Condition.Progress.ToString()}\n");
                }

                // 召怪条件
                var MC = MyMonster.SNCount >= Condition.MonsterCount;
                if (Condition.MonsterCount != -1 && !MC)
                {
                    all = false;
                    loop = true;
                    mess.Append($" 召怪条件未满足: 当前召怪次数 {MyMonster.SNCount} < {Condition.MonsterCount}\n");
                }

                // 弹发条件
                var PrC = MyProjectile.SPCount >= Condition.ProjectileCount;
                if (Condition.ProjectileCount != -1 && !MC)
                {
                    all = false;
                    loop = true;
                    mess.Append($" 弹发条件未满足: 当前生成弹幕次数 {MyProjectile.SPCount} < {Condition.ProjectileCount}\n");
                }

                // 死亡次数条件
                var DC = data.DeadCount >= Condition.DeadCount;
                if (Condition.DeadCount != -1 && !DC)
                {
                    all = false;
                    loop = true;
                    mess.Append($" 死次条件未满足: 当前死亡次数 {data.DeadCount} < {Condition.DeadCount}\n");
                }

                // 距离条件
                var RC = range >= Condition.Range * 16;
                if (Condition.Range != -1 && !RC)
                {
                    all = false;
                    loop = true;
                    mess.Append($" 距离条件未满足: 玩家距离 {range} < {Condition.Range} 格\n");
                    MonsterSpeed.AutoTar(npc, data); //自动转换仇恨目标
                }

                //速度条件
                var absX = Math.Abs(npc.velocity.X);
                var absY = Math.Abs(npc.velocity.Y);
                var SP = absX >= Condition.Speed || absY >= Condition.Speed;
                if (Condition.Speed != -1 && !SP)
                {
                    all = false;
                    loop = true;
                    mess.Append($" 速度条件未满足: x{npc.velocity.X:F0} y{npc.velocity.Y:F0} 速度 < {Condition.Speed}\n");
                }

                // 玩家生命条件
                var PL = plr.statLife <= Condition.PlayerLife;
                if (Condition.PlayerLife != -1 && !PL)
                {
                    all = false;
                    loop = true;
                    mess.Append($" 生命条件未满足: 玩家生命 {plr.statLife} > {Condition.PlayerLife} \n");
                    MonsterSpeed.AutoTar(npc, data); //自动转换仇恨目标
                }

                // 玩家防御条件
                var DE = plr.statDefense <= Condition.PlrDefense;
                if (Condition.PlrDefense != -1 && !DE)
                {
                    all = false;
                    loop = true;
                    mess.Append($" 防御条件未满足: 玩家防御 {plr.statDefense} > {Condition.PlrDefense} \n");
                    MonsterSpeed.AutoTar(npc, data); //自动转换仇恨目标
                }
            }
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

    #region 生命条件
    private static bool LifeCondition(int life, ConditionData? cycle)
    {
        var flag = true;
        if (cycle == null) return false;
        var result = CheckLife(cycle.NpcLift);
        if (result.success && result.min != -1 && result.max != -1)
        {
            if (life < result.min || life > result.max)
            {
                flag = false;
            }
        }
        if (result.min == -1 || result.max == -1)
        {
            flag = false;
        }

        return flag;
    }
    #endregion

    #region 解析生命条件的方法
    private static (bool success, int min, int max) CheckLife(string condition)
    {
        var parts = condition.Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out int min) || !int.TryParse(parts[1].Trim(), out int max))
        {
            // 解析失败，返回错误标志
            return (false, -1, -1);
        }

        // 确保min <= max
        return (true, Math.Min(min, max), Math.Max(min, max));
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

    #region 获取玩家当前武器类型的逻辑
    public static string GetPlayerWeapon(Player plr)
    {
        var Held = plr.HeldItem;
        if (Held == null || Held.type == 0) return "无";

        // 检查近战武器
        if (Held.melee && Held.maxStack == 1 && Held.damage > 0 && Held.ammo == 0 &&
            Held.pick < 1 && Held.hammer < 1 && Held.axe < 1) return "近战";

        // 检查远程武器
        if (Held.ranged && Held.maxStack == 1 &&
            Held.damage > 0 && Held.ammo == 0 && !Held.consumable) return "远程";

        // 检查魔法武器
        if (Held.magic && Held.maxStack == 1 &&
            Held.damage > 0 && Held.ammo == 0) return "魔法";

        // 检查召唤鞭子
        if (ItemID.Sets.SummonerWeaponThatScalesWithAttackSpeed[Held.type]) return "召唤";

        // 检查悠悠球
        if (ItemID.Sets.Yoyo[Held.type]) return "悠悠球";

        // 检查投掷物
        if (Held.maxStack == 9999 && Held.damage > 0 &&
            Held.ammo == 0 && Held.ranged && Held.consumable ||
            ItemID.Sets.ItemsThatCountAsBombsForDemolitionistToSpawn[Held.type]) return "投掷物";

        return "未知"; // 默认未知
    }
    #endregion
}
