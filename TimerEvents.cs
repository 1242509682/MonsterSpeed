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

    [JsonProperty("怪物AI", Order = 1)]
    public Dictionary<int, float> AIPairs { get; set; } = new Dictionary<int, float>();
    [JsonProperty("生成怪物", Order = 2)]
    public List<SpawnNpcData> SpawnNPC { get; set; } = new List<SpawnNpcData>();
    [JsonProperty("生成弹幕", Order = 3)]
    public List<ProjData> SendProj { get; set; } = new List<ProjData>();
}

internal class TimerEvents
{
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

    #region 时间事件
    private static Dictionary<string, DateTime> TextTime = new Dictionary<string, DateTime>(); // 跟踪每个NPC上次回血的时间
    public static void TimerEvent(NPC npc, StringBuilder mess, NpcData? data, Vector2 dict, float range)
    {
        if (data == null || data.TimerEvent == null) return;
        var (CD_Count, CD_Timer) = GetOrAdd(npc.FullName);

        //时间事件冷却倒计时（悬浮文本）
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

        //更新计数器和时间
        if ((DateTime.UtcNow - CD_Timer).TotalSeconds >= data.CoolTimer)
        {
            if (data.TimerEvent.Count > 0)
            {
                CD_Count = (CD_Count + 1) % data.TimerEvent.Count;
            }

            CD_Timer = DateTime.UtcNow;
            UpdateTrack(npc.FullName, CD_Count, CD_Timer);
        }

        // 时间事件
        var life = (int)(npc.life / (float)npc.lifeMax * 100);
        if (data.TimerEvent.Count > 0)
        {
            var cycle = data.TimerEvent.ElementAtOrDefault(CD_Count);
            if (cycle != null)
            {
                var all = true; //达成所有条件标识

                // 生命条件
                var LC = LifeCondition(life, cycle);
                if (!LC && cycle.NpcLift != "0,100")
                {
                    all = false;
                    mess.Append($" 血量条件未满足: 血量 {life}% < {cycle.NpcLift} \n");
                }

                // 武器条件
                var plr = TShock.Players.FirstOrDefault(p => p != null && p.Active && p.IsLoggedIn)!.TPlayer;
                var WC = cycle.WeaponName == GetPlayerWeapon(plr);
                if (cycle.WeaponName != "无" && !WC)
                {
                    all = false;
                    mess.Append($" 武器条件未满足: 玩家武器 {GetPlayerWeapon(plr)} 不是 {cycle.WeaponName}\n");
                    if (data.AutoTarget)
                    {
                        npc.TargetClosest(true);
                        npc.netSpam = 0;
                        npc.spriteDirection = npc.direction = Terraria.Utils.ToDirectionInt(npc.velocity.X > 0f);
                    }
                }

                // 进度条件
                var PC = ProgressChecker.IsProgress(cycle.Progress);
                if (cycle.Progress != (ProgressType)(-1) && !PC)
                {
                    all = false;
                    mess.Append($" 进度条件未满足: 当前进度不符合 {cycle.Progress.ToString()}\n");
                }

                // 召怪条件
                var MC = MyMonster.SNCount >= cycle.MonsterCount;
                if (cycle.MonsterCount != -1 && !MC)
                {
                    all = false;
                    mess.Append($" 召怪条件未满足: 当前召怪次数 {MyMonster.SNCount} < {cycle.MonsterCount}\n");
                }

                // 弹发条件
                var PrC = MyProjectile.SPCount >= cycle.ProjectileCount;
                if (cycle.ProjectileCount != -1 && !MC)
                {
                    all = false;
                    mess.Append($" 弹发条件未满足: 当前生成弹幕次数 {MyProjectile.SPCount} < {cycle.ProjectileCount}\n");
                }

                // 死亡次数条件
                var DC = data.DeadCount >= cycle.DeadCount;
                if (cycle.DeadCount != -1 && !DC)
                {
                    all = false;
                    mess.Append($" 死次条件未满足: 当前死亡次数 {data.DeadCount} < {cycle.DeadCount}\n");
                }

                // 距离条件
                var RC = range >= cycle.Range * 16;
                if(cycle.Range != -1 && !RC)
                {
                    all = false;
                    mess.Append($" 距离条件未满足: 玩家距离 {range} < {cycle.Range} 格\n");
                }

                //速度条件
                var absX = Math.Abs(npc.velocity.X);
                var absY = Math.Abs(npc.velocity.Y);
                var SP = absX >= cycle.Speed || absY >= cycle.Speed;
                if (cycle.Speed != -1 && !SP)
                {
                    all = false;
                    mess.Append($" 速度条件未满足: x{npc.velocity.X:F0} y{npc.velocity.Y:F0} 速度 < {cycle.Speed}\n");
                }

                // 玩家生命条件
                var PL = plr.statLife <= cycle.PlayerLife;
                if (cycle.PlayerLife != -1 && !PL)
                {
                    all = false;
                    mess.Append($" 生命条件未满足: 玩家生命 {plr.statLife} > {cycle.PlayerLife} \n");
                    if (data.AutoTarget)
                    {
                        npc.TargetClosest(true);
                        npc.netSpam = 0;
                        npc.spriteDirection = npc.direction = Terraria.Utils.ToDirectionInt(npc.velocity.X > 0f);
                    }
                }

                // 玩家防御条件
                var DE = plr.statDefense <= cycle.PlrDefense;
                if (cycle.PlrDefense != -1 && !DE)
                {
                    all = false;
                    mess.Append($" 防御条件未满足: 玩家防御 {plr.statDefense} > {cycle.PlrDefense} \n");
                    if (data.AutoTarget)
                    {
                        npc.TargetClosest(true);
                        npc.netSpam = 0;
                        npc.spriteDirection = npc.direction = Terraria.Utils.ToDirectionInt(npc.velocity.X > 0f);
                    }
                }

                // 满足所有条件
                if (all)
                {
                    // AI赋值
                    AIPairs(cycle.AIPairs, npc);

                    // 召唤怪物
                    if (cycle.SpawnNPC != null && cycle.SpawnNPC.Count > 0)
                    {
                        MyMonster.SpawnMonsters(cycle.SpawnNPC, npc);
                    }

                    // 生成弹幕
                    if (cycle.SendProj != null && cycle.SendProj.Count > 0)
                    {
                        MyProjectile.SpawnProjectile(cycle.SendProj, npc);
                    }

                    // 监控
                    if (cycle.AIPairs.Count > 0)
                    {
                        var AiInfo = AIPairsInfo(cycle.AIPairs);
                        mess.Append($" ai赋值:[c/A2E4DB:{AiInfo}]\n");
                    }
                }
            }
        }

        mess.Append($" 顺序:[c/A2E4DB:{CD_Count + 1}/{data.TimerEvent.Count}] 血量:[c/A2E4DB:{life}%]" +
                    $" 召怪:[c/A2E4DB:{MyMonster.SNCount}] 弹发:[c/A2E4DB:{MyProjectile.SPCount}]\n");
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
    private static bool LifeCondition(int life, TimerData? cycle)
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
