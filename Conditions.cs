using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;
using TShockAPI;
using static MonsterSpeed.Configuration;
using static MonsterSpeed.UpProj;

namespace MonsterSpeed;

// 统一的条件数据结构
public class Conditions
{
    // 基础条件
    [JsonProperty("查标志", Order = -23)]
    public string CheckFlag { get; set; } = "";
    [JsonProperty("BOSS血量范围", Order = -22)]
    public string NpcLift { get; set; } = "0,100";
    [JsonProperty("游戏进度条件", Order = -21)]
    public List<string> Progress { get; set; } = new List<string>();
    [JsonProperty("目标玩家生命值", Order = -20)]
    public int PlayerLife { get; set; } = -1;
    [JsonProperty("目标玩家防御力", Order = -19)]
    public int PlrDefense { get; set; } = -1;
    [JsonProperty("目标玩家武器类型", Order = -18)]
    public string WeaponName { get; set; } = "无";
    [JsonProperty("召唤怪物次数", Order = -17)]
    public int MonsterCount { get; set; } = 0;
    [JsonProperty("发射弹幕次数", Order = -16)]
    public int ProjectileCount { get; set; } = 0;
    [JsonProperty("怪物死亡次数", Order = -15)]
    public int DeadCount { get; set; } = 0;
    [JsonProperty("与目标玩家距离", Order = -14)]
    public float Range { get; set; } = -1;
    [JsonProperty("怪物移动速度", Order = -13)]
    public float Speed { get; set; } = -1;
    [JsonProperty("AI状态条件", Order = -12)]
    public Dictionary<int, string[]> AIPairs { get; set; } = new Dictionary<int, string[]>();
    [JsonProperty("时间范围条件", Order = -11)]
    public string Timer { get; set; } = "0,0";
    [JsonProperty("事件执行次数", Order = -10)]
    public Dictionary<int, int> ExecuteCount { get; set; } = new Dictionary<int, int>();
    [JsonProperty("累计播放次数", Order = -9)]
    public int TotalPlayCount { get; set; } = -1;
    [JsonProperty("文件播放次数", Order = -8)]
    public Dictionary<string, int> FilePlayCount { get; set; } = new Dictionary<string, int>();

    [JsonProperty("指示物条件", Order = 90)]
    public Dictionary<string, string[]> MarkerConds { get; set; } = new Dictionary<string, string[]>();

    [JsonProperty("范围内玩家检查", Order = 100)]
    public RangePlayerCondition RangePlayers { get; set; } = new RangePlayerCondition();
    [JsonProperty("范围内怪物检查", Order = 110)]
    public RangeMonsterCondition RangeMonsters { get; set; } = new RangeMonsterCondition();
    [JsonProperty("范围内弹幕检查", Order = 120)]
    public RangeProjectileCondition RangeProjectiles { get; set; } = new RangeProjectileCondition();

    #region 增强条件子类
    public class RangePlayerCondition
    {
        [JsonProperty("范围格数")]
        public string Range { get; set; } = "0,0";
        [JsonProperty("需要玩家数量")]
        public int MatchCnt { get; set; } = 0;
        [JsonProperty("玩家生命值")]
        public int HP { get; set; } = -1;
        [JsonProperty("玩家生命百分比")]
        public int HPRatio { get; set; } = -1;
        [JsonProperty("需要Buff列表")]
        public int[] Buffs { get; set; } = new int[0];
    }

    public class RangeMonsterCondition
    {
        [JsonProperty("怪物标志")]
        public string Flag { get; set; } = "";
        [JsonProperty("怪物ID")]
        public int MstID { get; set; } = 0;
        [JsonProperty("范围格数")]
        public int Range { get; set; } = 0;
        [JsonProperty("需要怪物数量")]
        public int MatchCnt { get; set; } = 0;
        [JsonProperty("怪物血量百分比")]
        public int HPRatio { get; set; } = 0;
    }

    public class RangeProjectileCondition
    {
        [JsonProperty("弹幕标志")]
        public string Flag { get; set; } = "";
        [JsonProperty("弹幕ID")]
        public int ProjID { get; set; } = 0;
        [JsonProperty("范围格数")]
        public int Range { get; set; } = 0;
        [JsonProperty("需要弹幕数量")]
        public int MatchCnt { get; set; } = 0;
        [JsonProperty("是否全局弹幕")]
        public bool IsGlobal { get; set; } = false;
    }
    #endregion

    #region 触发条件主入口
    internal static void Condition(NpcData data, NPC npc, Conditions Cond, ref bool allow)
    {
        var mess = new StringBuilder();
        Condition(npc, mess, data, Cond, ref allow);
    }

    internal static void Condition(NPC npc, StringBuilder mess, NpcData? data, Conditions Cond, ref bool allow)
    {
        if (data is null) return;
        if (Cond is null) return;

        bool flag = true;

        // 基础条件检查
        flag &= CheckConditions(npc, data, Cond, mess);
        
        // 增强条件检查
        flag &= CheckConditions2(npc, Cond, mess);

        allow = flag;
    }
    #endregion

    #region 基础条件检查
    private static bool CheckConditions(NPC npc, NpcData data, Conditions Cond, StringBuilder mess)
    {
        bool allMet = true;

        // 新增：标志条件检查
        if (!string.IsNullOrEmpty(Cond.CheckFlag))
        {
            bool flagMet = data.Flag == Cond.CheckFlag;
            if (!flagMet)
            {
                allMet = false;
                mess.Append($" 标志条件未满足: 需要标志 '{Cond.CheckFlag}'，当前标志 '{data.Flag}'\n");
            }
        }

        // 新增：指示物条件检查
        if (Cond.MarkerConds != null && Cond.MarkerConds.Count > 0)
        {
            if (!MarkerUtil.CheckMarkers(StateApi.GetState(npc), Cond.MarkerConds, npc))
            {
                allMet = false;
                mess.Append(" 指示物条件未满足\n");
            }
        }

        // 生命条件
        var life = (int)PxUtil.GetLifeRatio(npc);
        var LC = LifeCondition(life, Cond);
        if (!LC && Cond.NpcLift != "0,0")
        {
            allMet = false;
            mess.Append($" 血量条件未满足: 血量 {life}% 不在范围 {Cond.NpcLift}%\n");
        }

        // 武器条件
        Player plr = PxUtil.GetValidTarget(npc);
        if (plr is null) 
        {
            return false;
        }
        
        var WC = Cond.WeaponName == GetPlayerWeapon(plr);
        if (Cond.WeaponName != "无" && !WC)
        {
            allMet = false;
            mess.Append($" 武器条件未满足: 玩家武器 {GetPlayerWeapon(plr)} 不是 {Cond.WeaponName}\n");
            MonsterSpeed.AutoTar(npc, data);
        }

        // 进度条件
        var PC = CheckGroup(plr, Cond.Progress);
        if (!PC)
        {
            allMet = false;
            mess.Append($" 进度条件未满足: 当前进度不符合 {string.Join(",", Cond.Progress)}\n");
        }

        var state = StateApi.GetState(npc);
        
        // 数量条件检查
        if (!CheckCountConditions(npc, state, data, Cond, mess))
        {
            allMet = false;
        }

        // 距离条件 - 只检查与目标玩家的距离
        if (Cond.Range != -1 && !PxUtil.InRangeTiles(plr.Center, npc.Center, Cond.Range))
        {
            allMet = false;
            float currentRange = PxUtil.ToTiles(Vector2.Distance(plr.Center, npc.Center));
            mess.Append($" 距离条件未满足: 玩家距离 {currentRange:F1} < {Cond.Range} 格\n");
            MonsterSpeed.AutoTar(npc, data);
        }

        // 速度条件
        var absX = Math.Abs(npc.velocity.X);
        var absY = Math.Abs(npc.velocity.Y);
        var SP = absX >= Cond.Speed || absY >= Cond.Speed;
        if (Cond.Speed != -1 && !SP)
        {
            allMet = false;
            mess.Append($" 速度条件未满足: x{npc.velocity.X:F0} y{npc.velocity.Y:F0} 速度 < {Cond.Speed}\n");
        }

        // 玩家生命条件 - 只检查目标玩家
        if (Cond.PlayerLife != -1 && !PxUtil.CheckHPCondition(plr, Cond.PlayerLife, -1))
        {
            allMet = false;
            mess.Append($" 生命条件未满足: 玩家生命 {plr.statLife} > {Cond.PlayerLife} \n");
            MonsterSpeed.AutoTar(npc, data);
        }

        // 玩家防御条件
        var DE = plr.statDefense <= Cond.PlrDefense;
        if (Cond.PlrDefense != -1 && !DE)
        {
            allMet = false;
            mess.Append($" 防御条件未满足: 玩家防御 {plr.statDefense} > {Cond.PlrDefense} \n");
            MonsterSpeed.AutoTar(npc, data);
        }

        // AI条件
        if (Cond.AIPairs != null && Cond.AIPairs.Count > 0)
        {
            if (!AICondition(npc, mess, Cond))
            {
                allMet = false;
            }
        }

        // 时间范围条件
        var TC = TimerCondition(npc, data, Cond, mess);
        if (!TC && Cond.Timer != "0,0")
        {
            allMet = false;
            mess.Append($" 时间条件未满足: 当前时间不在 {Cond.Timer} 秒范围内\n");
        }

        // 执行次数条件
        var EC = ExecuteCountCondition(npc, Cond, mess);
        if (!EC && Cond.ExecuteCount.Count > 0)
        {
            allMet = false;
        }

        // 累计播放次数条件
        var TPC = TotalPlayCountCondition(npc, Cond, mess);
        if (!TPC && Cond.TotalPlayCount != -1)
        {
            allMet = false;
        }

        // 指定文件播放次数条件
        var FPC = FilePlayCountCondition(npc, Cond, mess);
        if (!FPC && Cond.FilePlayCount.Count > 0)
        {
            allMet = false;
        }

        return allMet;
    }
    #endregion

    #region 增强条件检查
    private static bool CheckConditions2(NPC npc, Conditions Cond, StringBuilder mess)
    {
        bool allMet = true;

        var Result = PxUtil.ParseRange(Cond.RangePlayers.Range, s => int.Parse(s.Trim()));

        // 玩家增强：范围内玩家计数
        if (Cond.RangePlayers.MatchCnt != 0 && Result.max > 0)
        {
            allMet &= CheckPlrRangeCondition(npc, Cond.RangePlayers, mess);
        }

        // 怪物增强：范围内怪物计数
        if (Cond.RangeMonsters.MatchCnt != 0)
        {
            allMet &= CheckMstRangeCondition(npc, Cond.RangeMonsters, mess);
        }

        // 弹幕增强：范围内弹幕计数
        if (Cond.RangeProjectiles.MatchCnt != 0)
        {
            allMet &= CheckProjRangeCondition(npc, Cond.RangeProjectiles, mess);
        }

        return allMet;
    }

    #region 玩家范围计数（增强功能）
    private static bool CheckPlrRangeCondition(NPC npc, RangePlayerCondition cond, StringBuilder mess)
    {
        if (cond.MatchCnt == 0) return true;

        // 解析范围字符串
        var Result = PxUtil.ParseRange(cond.Range, s => int.Parse(s.Trim()));
        if (!Result.success)
        {
            mess.Append($" 玩家范围条件格式错误: {cond.Range}\n");
            return false;
        }

        int rangeMin = Result.min;
        int rangeMax = Result.max;

        if (rangeMax <= 0) return true;

        int cnt = 0;
        float rangeToPx = PxUtil.ToPx(rangeMax);
        float rangeFromPx = PxUtil.ToPx(rangeMin);

        foreach (TSPlayer plr in TShock.Players)
        {
            if (!PxUtil.IsValidPlr(plr))
                continue;

            // 检查Buff条件
            if (cond.Buffs.Length > 0 && !cond.Buffs.All(b => plr.TPlayer.buffType.Contains(b)))
                continue;

            // 检查生命条件（增强功能特有的）
            if (!PxUtil.CheckHPCondition(plr, cond.HP, cond.HPRatio))
                continue;

            var distSq = Vector2.DistanceSquared(npc.Center, plr.TPlayer.Center);
            bool inRange = rangeMin > 0 ?
                distSq > rangeFromPx * rangeFromPx && distSq <= rangeToPx * rangeToPx :
                distSq <= rangeToPx * rangeToPx;

            if (inRange) cnt++;
        }

        return PxUtil.CheckCountCondition(cond.MatchCnt, cnt, "范围内玩家", mess);
    }
    #endregion

    #region 怪物范围计数（增强功能）
    private static bool CheckMstRangeCondition(NPC npc, RangeMonsterCondition cond, StringBuilder mess)
    {
        if (cond.MatchCnt == 0) return true;

        int cnt = CountRangeMsts(npc, cond);
        return PxUtil.CheckCountCondition(cond.MatchCnt, cnt, "范围内怪物", mess);
    }

    private static int CountRangeMsts(NPC npc, RangeMonsterCondition cond)
    {
        int cnt = 0;
        float rangePx = PxUtil.ToPx(cond.Range);

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            var mst = Main.npc[i];
            if (!PxUtil.IsValidMst(mst, npc)) continue;
            if (cond.MstID != 0 && mst.netID != cond.MstID) continue;
            if (cond.Range > 0 && !PxUtil.InRange(npc.Center, mst.Center, rangePx)) continue;
            if (!CheckMstHP(mst, cond.HPRatio)) continue;
            if (!CheckMstFlag(mst, cond.Flag)) continue;

            cnt++;
        }
        return cnt;
    }

    private static bool CheckMstHP(NPC mst, int hpRatio)
    {
        if (hpRatio == 0) return true;

        float currentRatio = PxUtil.GetLifeRatio(mst);
        return hpRatio > 0 ? currentRatio >= hpRatio : currentRatio < Math.Abs(hpRatio);
    }

    private static bool CheckMstFlag(NPC mst, string flag)
    {
        // 如果flag为空，直接返回true（表示不需要检查标志）
        if (string.IsNullOrEmpty(flag))
            return true;

        // 在NpcDatas列表中查找匹配的配置
        if (MonsterSpeed.Config?.NpcDatas != null)
        {
            // 查找包含当前怪物ID且标志匹配的配置
            var matchedData = MonsterSpeed.Config.NpcDatas.FirstOrDefault(data =>
                data.Type != null &&
                data.Type.Contains(mst.netID) &&
                data.Flag == flag);

            return matchedData != null;
        }

        return false;
    }
    #endregion

    #region 弹幕范围计数（增强功能）
    private static bool CheckProjRangeCondition(NPC npc, RangeProjectileCondition cond, StringBuilder mess)
    {
        if (cond.MatchCnt == 0) return true;

        int cnt = CountRangeProjs(npc, cond);
        return PxUtil.CheckCountCondition(cond.MatchCnt, cnt, "范围内弹幕", mess);
    }

    private static int CountRangeProjs(NPC npc, RangeProjectileCondition cond)
    {
        int cnt = 0;
        float rangePx = PxUtil.ToPx(cond.Range);

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            var proj = Main.projectile[i];
            if (!IsValidProj(proj, npc, cond)) continue;
            if (cond.ProjID != 0 && proj.type != cond.ProjID) continue;
            if (cond.Range > 0 && !PxUtil.InRange(npc.Center, proj.Center, rangePx)) continue;

            cnt++;
        }
        return cnt;
    }

    private static bool IsValidProj(Projectile proj, NPC npc, RangeProjectileCondition cond)
    {
        if (!PxUtil.IsValidProj(proj))
            return false;

        if (cond.IsGlobal && UpdateState[proj.whoAmI]?.whoAmI != npc.whoAmI)
            return false;

        return string.IsNullOrEmpty(cond.Flag) || UpdateState[proj.whoAmI]?.Notes == cond.Flag;
    }
    #endregion

    #endregion

    #region 共用辅助方法
    private static bool CheckCountConditions(NPC npc, NpcState state, NpcData data, Conditions Cond, StringBuilder mess)
    {
        bool allMet = true;

        // 召怪条件
        int snCount = state?.SNCount ?? 0;
        if (!PxUtil.CheckCountCondition(Cond.MonsterCount, snCount, "召怪", mess))
        {
            allMet = false;
        }

        // 弹发条件
        int spCount = state?.SPCount ?? 0;
        if (!PxUtil.CheckCountCondition(Cond.ProjectileCount, spCount, "弹发", mess))
        {
            allMet = false;
        }

        // 死亡次数条件
        if (!PxUtil.CheckCountCondition(Cond.DeadCount, data.DeadCount, "死亡", mess))
        {
            allMet = false;
        }

        return allMet;
    }

    private static bool LifeCondition(int life, Conditions? cycle)
    {
        if (cycle == null) return false;
        
        var result = PxUtil.ParseRange(cycle.NpcLift, s => int.Parse(s.Trim()));
        if (!result.success) return false;

        return life >= result.min && life <= result.max;
    }

    public static string GetPlayerWeapon(Player plr)
    {
        var Held = plr.HeldItem;
        if (Held == null || Held.type == 0) return "无";

        if (Held.melee && Held.maxStack == 1 && Held.damage > 0 && Held.ammo == 0 &&
            Held.pick < 1 && Held.hammer < 1 && Held.axe < 1) return "近战";

        if (Held.ranged && Held.maxStack == 1 &&
            Held.damage > 0 && Held.ammo == 0 && !Held.consumable) return "远程";

        if (Held.magic && Held.maxStack == 1 &&
            Held.damage > 0 && Held.ammo == 0) return "魔法";

        if (ItemID.Sets.SummonerWeaponThatScalesWithAttackSpeed[Held.type]) return "召唤";

        if (Held.maxStack == 9999 && Held.damage > 0 &&
            Held.ammo == 0 && Held.ranged && Held.consumable ||
            ItemID.Sets.ItemsThatCountAsBombsForDemolitionistToSpawn[Held.type]) return "投掷物";

        return "未知";
    }

    private static bool AICondition(NPC npc, StringBuilder mess, Conditions Condition)
    {
        bool allMet = true;

        foreach (var ai in Condition.AIPairs)
        {
            if (ai.Key < 0 || ai.Key > 3)
            {
                allMet = false;
                mess.Append($" AI条件格式错误: 键必须是 0~3，当前键为 '{ai.Key}'\n");
                continue;
            }

            string[] expressions = ai.Value;
            if (expressions == null || expressions.Length == 0)
            {
                allMet = false;
                mess.Append($" AI条件格式错误: 值为空或未定义 (键: AI[{ai.Key}])\n");
                continue;
            }

            float npcAIValue = npc.ai[ai.Key];

            foreach (string Raw in expressions)
            {
                string? expr = Raw?.Trim();
                if (string.IsNullOrWhiteSpace(expr))
                {
                    allMet = false;
                    mess.Append($" AI条件格式错误: 表达式为空 (键: AI[{ai.Key}])\n");
                    continue;
                }

                bool Met = ParseAIExpression(expr, npcAIValue);
                
                if (!Met)
                {
                    allMet = false;
                    mess.Append($" AI条件未满足: AI[{ai.Key}] {expr} 不成立 (当前值: {npcAIValue:F2})\n");
                }
            }
        }

        return allMet;
    }

    private static bool ParseAIExpression(string expr, float npcAIValue)
    {
        if (expr.StartsWith("=="))
        {
            return float.TryParse(expr.Substring(2), out float eqVal) && npcAIValue == eqVal;
        }
        else if (expr.StartsWith("!="))
        {
            return float.TryParse(expr.Substring(2), out float neqVal) && npcAIValue != neqVal;
        }
        else if (expr.StartsWith(">="))
        {
            return float.TryParse(expr.Substring(2), out float geVal) && npcAIValue >= geVal;
        }
        else if (expr.StartsWith("<="))
        {
            return float.TryParse(expr.Substring(2), out float leVal) && npcAIValue <= leVal;
        }
        else if (expr.StartsWith(">"))
        {
            return float.TryParse(expr.Substring(1), out float gtVal) && npcAIValue > gtVal;
        }
        else if (expr.StartsWith("<"))
        {
            return float.TryParse(expr.Substring(1), out float ltVal) && npcAIValue < ltVal;
        }
        else if (expr.StartsWith("="))
        {
            return float.TryParse(expr.Substring(1), out float eqVal) && npcAIValue == eqVal;
        }
        else
        {
            return float.TryParse(expr, out float exactVal) && npcAIValue == exactVal;
        }
    }

    private static bool TimerCondition(NPC npc, NpcData data, Conditions Condition, StringBuilder mess)
    {
        if (Condition.Timer == "0,0") return true;
    
        var result = PxUtil.ParseRange(Condition.Timer, s => double.Parse(s.Trim()));
        if (!result.success)
        {
            mess.Append($" 时间条件格式错误: {Condition.Timer}\n");
            return false;
        }
    
        var state = StateApi.GetState(npc);
        if (state == null) return false;
    
        double elapsed = (DateTime.UtcNow - state.CooldownTime[state.EventIndex]).TotalSeconds;
        bool inRange = elapsed >= result.min && elapsed <= result.max;
    
        return inRange;
    }

    private static bool ExecuteCountCondition(NPC npc, Conditions Condition, StringBuilder mess)
    {
        if (Condition.ExecuteCount.Count == 0) return true;

        var state = StateApi.GetState(npc);
        if (state == null) return false;

        bool allMet = true;

        foreach (var kvp in Condition.ExecuteCount)
        {
            int Index = kvp.Key;
            int NeedCount = kvp.Value;

            int NewCount = GetEventExecuteCount(state, Index);

            if (!PxUtil.CheckCountCondition(NeedCount, NewCount, $"事件[{Index}]执行", mess))
            {
                allMet = false;
            }
        }

        return allMet;
    }

    private static int GetEventExecuteCount(NpcState state, int eventIndex)
    {
        if (state.EventCounts != null && state.EventCounts.ContainsKey(eventIndex))
        {
            return state.EventCounts[eventIndex];
        }
        return 0;
    }

    private static bool TotalPlayCountCondition(NPC npc, Conditions Condition, StringBuilder mess)
    {
        if (Condition.TotalPlayCount == -1) return true;

        int Total = GetTotalPlayCount(npc);
        return PxUtil.CheckCountCondition(Condition.TotalPlayCount, Total, "累计播放", mess);
    } 

    private static int GetTotalPlayCount(NPC npc)
    {
        var state = StateApi.GetState(npc);
        if (state == null) return 0;

        int total = 0;
        if (state.EventCounts != null)
        {
            foreach (var count in state.EventCounts.Values)
            {
                total += count;
            }
        }
        return total;
    }

    private static bool FilePlayCountCondition(NPC npc, Conditions Condition, StringBuilder mess)
    {
        if (Condition.FilePlayCount.Count == 0) return true;

        var state = StateApi.GetState(npc);
        if (state == null) return false;

        bool allMet = true;

        foreach (var kvp in Condition.FilePlayCount)
        {
            string fileName = kvp.Key;
            int NeedCount = kvp.Value;

            int NewCount = GetFilePlayCount(state, fileName);

            if (!PxUtil.CheckCountCondition(NeedCount, NewCount, $"文件{fileName}播放", mess))
            {
                allMet = false;
            }
        }

        return allMet;
    }

    private static int GetFilePlayCount(NpcState state, string fileName)
    {
        if (state.PlayCounts != null && state.PlayCounts.ContainsKey(fileName))
        {
            return state.PlayCounts[fileName];
        }
        return 0;
    }
    #endregion

    #region 进度条件
    // 检查条件组中的所有条件是否都满足
    public static bool CheckGroup(Player p, List<string> conds)
    {
        foreach (var c in conds)
        {
            if (!CheckCond(p, c))
                return false;
        }
        return true;
    }

    // 检查单个条件是否满足 - 直接匹配中文
    public static bool CheckCond(Player p, string cond)
    {
        switch (cond)
        {
            case "0":
            case "无":
                return true;
            case "1":
            case "克眼":
            case "克苏鲁之眼":
                return NPC.downedBoss1;
            case "2":
            case "史莱姆王":
            case "史王":
                return NPC.downedSlimeKing;
            case "3":
            case "世吞":
            case "黑长直":
            case "世界吞噬者":
            case "世界吞噬怪":
                return NPC.downedBoss2 &&
                       (IsDefeated(NPCID.EaterofWorldsHead) ||
                        IsDefeated(NPCID.EaterofWorldsBody) ||
                        IsDefeated(NPCID.EaterofWorldsTail));
            case "4":
            case "克脑":
            case "脑子":
            case "克苏鲁之脑":
                return NPC.downedBoss2 && IsDefeated(NPCID.BrainofCthulhu);
            case "5":
            case "邪恶boss2":
            case "世吞或克脑":
            case "击败世吞克脑任意一个":
                return NPC.downedBoss2;
            case "6":
            case "巨鹿":
            case "鹿角怪":
                return NPC.downedDeerclops;
            case "7":
            case "蜂王":
                return NPC.downedQueenBee;
            case "8":
            case "骷髅王前":
                return !NPC.downedBoss3;
            case "9":
            case "吴克":
            case "骷髅王":
            case "骷髅王后":
                return NPC.downedBoss3;
            case "10":
            case "肉前":
                return !Main.hardMode;
            case "11":
            case "困难模式":
            case "肉山":
            case "肉后":
            case "血肉墙":
                return Main.hardMode;
            case "12":
            case "毁灭者":
            case "铁长直":
                return NPC.downedMechBoss1;
            case "13":
            case "双子眼":
            case "双子魔眼":
                return NPC.downedMechBoss2;
            case "14":
            case "铁吴克":
            case "机械吴克":
            case "机械骷髅王":
                return NPC.downedMechBoss3;
            case "15":
            case "世纪之花":
            case "花后":
            case "世花":
                return NPC.downedPlantBoss;
            case "16":
            case "石后":
            case "石巨人":
                return NPC.downedGolemBoss;
            case "17":
            case "史后":
            case "史莱姆皇后":
                return NPC.downedQueenSlime;
            case "18":
            case "光之女皇":
            case "光女":
                return NPC.downedEmpressOfLight;
            case "19":
            case "猪鲨":
            case "猪龙鱼公爵":
                return NPC.downedFishron;
            case "20":
            case "拜月":
            case "拜月教":
            case "教徒":
            case "拜月教邪教徒":
                return NPC.downedAncientCultist;
            case "21":
            case "月总":
            case "月亮领主":
                return NPC.downedMoonlord;
            case "22":
            case "哀木":
                return NPC.downedHalloweenTree;
            case "23":
            case "南瓜王":
                return NPC.downedHalloweenKing;
            case "24":
            case "常绿尖叫怪":
                return NPC.downedChristmasTree;
            case "25":
            case "冰雪女王":
                return NPC.downedChristmasIceQueen;
            case "26":
            case "圣诞坦克":
                return NPC.downedChristmasSantank;
            case "27":
            case "火星飞碟":
                return NPC.downedMartians;
            case "28":
            case "小丑":
                return NPC.downedClown;
            case "29":
            case "日耀柱":
                return NPC.downedTowerSolar;
            case "30":
            case "星旋柱":
                return NPC.downedTowerVortex;
            case "31":
            case "星云柱":
                return NPC.downedTowerNebula;
            case "32":
            case "星尘柱":
                return NPC.downedTowerStardust;
            case "33":
            case "一王后":
            case "任意机械boss":
                return NPC.downedMechBossAny;
            case "34":
            case "三王后":
                return NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3;
            case "35":
            case "一柱后":
                return NPC.downedTowerNebula || NPC.downedTowerSolar || NPC.downedTowerStardust || NPC.downedTowerVortex;
            case "36":
            case "四柱后":
                return NPC.downedTowerNebula && NPC.downedTowerSolar && NPC.downedTowerStardust && NPC.downedTowerVortex;
            case "37":
            case "哥布林入侵":
                return NPC.downedGoblins;
            case "38":
            case "海盗入侵":
                return NPC.downedPirates;
            case "39":
            case "霜月":
                return NPC.downedFrost;
            case "40":
            case "血月":
                return Main.bloodMoon;
            case "41":
            case "雨天":
                return Main.raining;
            case "42":
            case "白天":
                return Main.dayTime;
            case "43":
            case "晚上":
                return !Main.dayTime;
            case "44":
            case "大风天":
                return Main.IsItAHappyWindyDay;
            case "45":
            case "万圣节":
                return Main.halloween;
            case "46":
            case "圣诞节":
                return Main.xMas;
            case "47":
            case "派对":
                return BirthdayParty.PartyIsUp;
            case "48":
            case "旧日一":
            case "黑暗法师":
            case "撒旦一":
                return DD2Event._downedDarkMageT1;
            case "49":
            case "旧日二":
            case "巨魔":
            case "食人魔":
            case "撒旦二":
                return DD2Event._downedOgreT2;
            case "50":
            case "旧日三":
            case "贝蒂斯":
            case "双足翼龙":
            case "撒旦三":
                return DD2Event._spawnedBetsyT3;
            case "51":
            case "2020":
            case "醉酒":
            case "醉酒种子":
            case "醉酒世界":
                return Main.drunkWorld;
            case "52":
            case "2021":
            case "十周年":
            case "十周年种子":
                return Main.tenthAnniversaryWorld;
            case "53":
            case "ftw":
            case "真实世界":
            case "真实世界种子":
                return Main.getGoodWorld;
            case "54":
            case "ntb":
            case "蜜蜂世界":
            case "蜜蜂世界种子":
                return Main.notTheBeesWorld;
            case "55":
            case "dst":
            case "饥荒":
            case "永恒领域":
                return Main.dontStarveWorld;
            case "56":
            case "remix":
            case "颠倒":
            case "颠倒世界":
            case "颠倒种子":
                return Main.remixWorld;
            case "57":
            case "noTrap":
            case "陷阱种子":
            case "陷阱世界":
                return Main.noTrapsWorld;
            case "58":
            case "天顶":
            case "天顶种子":
            case "缝合种子":
            case "天顶世界":
            case "缝合世界":
                return Main.zenithWorld;
            case "59":
            case "森林":
                return p.ShoppingZone_Forest;
            case "60":
            case "丛林":
                return p.ZoneJungle;
            case "61":
            case "沙漠":
                return p.ZoneDesert;
            case "62":
            case "雪原":
                return p.ZoneSnow;
            case "63":
            case "洞穴":
                return p.ZoneRockLayerHeight;
            case "64":
            case "海洋":
                return p.ZoneBeach;
            case "65":
            case "地表":
                return (p.position.Y / 16) <= Main.worldSurface;
            case "66":
            case "太空":
                return (p.position.Y / 16) <= (Main.worldSurface * 0.35);
            case "67":
            case "地狱":
                return (p.position.Y / 16) >= Main.UnderworldLayer;
            case "68":
            case "神圣":
                return p.ZoneHallow;
            case "69":
            case "蘑菇":
                return p.ZoneGlowshroom;
            case "70":
            case "腐化":
            case "腐化地":
            case "腐化环境":
                return p.ZoneCorrupt;
            case "71":
            case "猩红":
            case "猩红地":
            case "猩红环境":
                return p.ZoneCrimson;
            case "72":
            case "邪恶":
            case "邪恶环境":
                return p.ZoneCrimson || p.ZoneCorrupt;
            case "73":
            case "地牢":
                return p.ZoneDungeon;
            case "74":
            case "墓地":
                return p.ZoneGraveyard;
            case "75":
            case "蜂巢":
                return p.ZoneHive;
            case "76":
            case "神庙":
                return p.ZoneLihzhardTemple;
            case "77":
            case "沙尘暴":
                return p.ZoneSandstorm;
            case "78":
            case "天空":
                return p.ZoneSkyHeight;
            case "79":
            case "满月":
                return Main.moonPhase == 0;
            case "80":
            case "亏凸月":
                return Main.moonPhase == 1;
            case "81":
            case "下弦月":
                return Main.moonPhase == 2;
            case "82":
            case "残月":
                return Main.moonPhase == 3;
            case "83":
            case "新月":
                return Main.moonPhase == 4;
            case "84":
            case "娥眉月":
                return Main.moonPhase == 5;
            case "85":
            case "上弦月":
                return Main.moonPhase == 6;
            case "86":
            case "盈凸月":
                return Main.moonPhase == 7;
            default:
                TShock.Log.ConsoleWarn($"[怪物加速] 未知条件: {cond}");
                return false;
        }
    }


    // 是否解锁怪物图鉴以达到解锁物品掉落的程度（用于独立判断克脑、世吞）
    private static bool IsDefeated(int type)
    {
        var unlockState = Main.BestiaryDB.FindEntryByNPCID(type).UIInfoProvider.GetEntryUICollectionInfo().UnlockState;
        return unlockState == Terraria.GameContent.Bestiary.BestiaryEntryUnlockState.CanShowDropsWithDropRates_4;
    }
    #endregion
}
