using System.Text;
using Microsoft.Xna.Framework;
using MonsterSpeed.Progress;
using Newtonsoft.Json;
using Terraria;
using Terraria.ID;
using TShockAPI;
using static MonsterSpeed.Configuration;

namespace MonsterSpeed;

//触发条件数据结构
public class Conditions
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
    [JsonProperty("AI条件", Order = -12)]
    public Dictionary<int, string[]> AIPairs { get; set; } = new Dictionary<int, string[]>();

    #region 触发条件
    internal static void Condition(NPC npc, StringBuilder mess, NpcData? data, TimerData Event, ref bool all, ref bool loop)
    {
        if (data is null) return;
        var Condition = Event.Condition;
        if (Condition != null)
        {
            // 生命条件
            var life = (int)(npc.life / (float)npc.lifeMax * 100);
            var LC = LifeCondition(life, Condition);
            if (!LC && Condition.NpcLift != "0,100")
            {
                all = false;
                loop = true;
                mess.Append($" 血量条件未满足: 血量 {life}% < {Condition.NpcLift} \n");
            }

            // 武器条件
            Player plr = Main.player[npc.target];
            if (plr is null || !plr.active || plr.dead) return;
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
            int snCount = MyMonster.GetState(npc)?.SNCount ?? 0;
            var MC = snCount >= Condition.MonsterCount;
            if (Condition.MonsterCount != -1 && !MC)
            {
                all = false;
                loop = true;
                mess.Append($" 召怪条件未满足: 当前召怪次数 {snCount} < {Condition.MonsterCount}\n");
            }

            // 弹发条件
            int spCount = MyProjectile.GetState(npc)?.SPCount ?? 0;
            var PrC = spCount >= Condition.ProjectileCount;
            if (Condition.ProjectileCount != -1 && !PrC)
            {
                all = false;
                loop = true;
                mess.Append($" 弹发条件未满足: 当前生成弹幕次数 {spCount} < {Condition.ProjectileCount}\n");
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
            var range = Vector2.Distance(Main.player[npc.target].Center, npc.Center);
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

            // AI条件
            if (Condition.AIPairs != null && Condition.AIPairs.Count > 0)
            {
                AICondition(npc, mess, ref all, ref loop, Condition);
            }

        }
    }
    #endregion

    #region 生命条件
    private static bool LifeCondition(int life, Conditions? cycle)
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

    #region AI条件解析（支持：= != > < >= <=）写法
    private static void AICondition(NPC npc, StringBuilder mess, ref bool all, ref bool loop, Conditions Condition)
    {
        foreach (var ai in Condition.AIPairs)
        {
            // 检查 TR_AI 键是否合法（只能是 0,1,2,3）
            if (ai.Key < 0 || ai.Key > 3)
            {
                all = false;
                loop = true;
                mess.Append($" AI条件格式错误: 键必须是 0~3，当前键为 '{ai.Key}'\n");
                continue;
            }

            string[] expressions = ai.Value;

            if (expressions == null || expressions.Length == 0)
            {
                all = false;
                loop = true;
                mess.Append($" AI条件格式错误: 值为空或未定义 (键: AI[{ai.Key}])\n");
                continue;
            }

            float npcAIValue = npc.ai[ai.Key];

            foreach (string Raw in expressions)
            {
                string? expr = Raw?.Trim();

                if (string.IsNullOrWhiteSpace(expr))
                {
                    all = false;
                    loop = true;
                    mess.Append($" AI条件格式错误: 表达式为空 (键: AI[{ai.Key}])\n");
                    continue;
                }

                bool Met = false;

                switch (expr.Substring(0, 1))
                {
                    case "=":
                        if (expr.Length > 1 && expr[1] == '=')
                        {
                            // == 等于
                            if (float.TryParse(expr.Substring(2), out float eqVal))
                            {
                                Met = npcAIValue == eqVal;
                            }
                            else
                            {
                                all = false;
                                loop = true;
                                mess.Append($" AI条件格式错误: 无效的等值判断 '{expr}' (键: AI[{ai.Key}])\n");
                            }
                        }
                        else
                        {
                            // = 等于
                            if (float.TryParse(expr.Substring(1), out float eqVal))
                            {
                                Met = npcAIValue == eqVal;
                            }
                            else
                            {
                                all = false;
                                loop = true;
                                mess.Append($" AI条件格式错误: 无效的等值判断 '{expr}' (键: AI[{ai.Key}])\n");
                            }
                        }
                        break;

                    case "!":
                        if (expr.Length > 1 && expr[1] == '=')
                        {
                            // != 不等于
                            if (float.TryParse(expr.Substring(2), out float neqVal))
                            {
                                Met = npcAIValue != neqVal;
                            }
                            else
                            {
                                all = false;
                                loop = true;
                                mess.Append($" AI条件格式错误: 无效的不等值判断 '{expr}' (键: AI[{ai.Key}])\n");
                            }
                        }
                        else
                        {
                            all = false;
                            loop = true;
                            mess.Append($" AI条件格式错误: 无法识别的操作符 '{expr}' (键: AI[{ai.Key}])\n");
                        }
                        break;

                    case ">":
                        if (expr.Length > 1 && expr[1] == '=')
                        {
                            // >= 大于等于
                            if (float.TryParse(expr.Substring(2), out float geVal))
                            {
                                Met = npcAIValue >= geVal;
                            }
                            else
                            {
                                all = false;
                                loop = true;
                                mess.Append($" AI条件格式错误: 无效的大于等于判断 '{expr}' (键: AI[{ai.Key}])\n");
                            }
                        }
                        else
                        {
                            // > 大于
                            if (float.TryParse(expr.Substring(1), out float gtVal))
                            {
                                Met = npcAIValue > gtVal;
                            }
                            else
                            {
                                all = false;
                                loop = true;
                                mess.Append($" AI条件格式错误: 无效的大于判断 '{expr}' (键: AI[{ai.Key}])\n");
                            }
                        }
                        break;

                    case "<":
                        if (expr.Length > 1 && expr[1] == '=')
                        {
                            // <= 小于等于
                            if (float.TryParse(expr.Substring(2), out float leVal))
                            {
                                Met = npcAIValue <= leVal;
                            }
                            else
                            {
                                all = false;
                                loop = true;
                                mess.Append($" AI条件格式错误: 无效的小于等于判断 '{expr}' (键: AI[{ai.Key}])\n");
                            }
                        }
                        else
                        {
                            // < 小于
                            if (float.TryParse(expr.Substring(1), out float ltVal))
                            {
                                Met = npcAIValue < ltVal;
                            }
                            else
                            {
                                all = false;
                                loop = true;
                                mess.Append($" AI条件格式错误: 无效的小于判断 '{expr}' (键: AI[{ai.Key}])\n");
                            }
                        }
                        break;

                    default:
                        // 默认尝试解析为数字进行等于判断
                        if (float.TryParse(expr, out float exactVal))
                        {
                            Met = npcAIValue == exactVal;
                        }
                        else
                        {
                            all = false;
                            loop = true;
                            mess.Append($" AI条件格式错误: 无法识别的操作符或无效的值 '{expr}' (键: AI[{ai.Key}])\n");
                        }
                        break;
                }

                if (!Met)
                {
                    all = false;
                    loop = true;
                    mess.Append($" AI条件未满足: AI[{ai.Key}] {expr} 不成立 (当前值: {npcAIValue:F2})\n");
                }
            }
        }
    }
    #endregion
}
