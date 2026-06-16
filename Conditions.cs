using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;
using TShockAPI;
using static MonsterSpeed.MonsterSpeed;
using static MonsterSpeed.Configuration;
using static MonsterSpeed.PxUtil;

namespace MonsterSpeed;

public class Conditions
{
    [JsonProperty("查标志", Order = -23)]
    public string CheckFlag { get; set; } = "";
    [JsonProperty("BOSS血量范围", Order = -22)]
    public string NpcLift { get; set; } = "0,100";
    [JsonProperty("游戏进度条件", Order = -21)]
    public List<string> Progress { get; set; } = new();
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
    public Dictionary<int, string[]> AIPairs { get; set; } = new();
    [JsonProperty("时间范围条件", Order = -11)]
    public string Timer { get; set; } = "0,0";
    [JsonProperty("事件执行次数", Order = -10)]
    public Dictionary<int, int> ExecuteCount { get; set; } = new();
    [JsonProperty("累计播放次数", Order = -9)]
    public int PlayTotal { get; set; } = -1;
    [JsonProperty("文件播放次数", Order = -8)]
    public Dictionary<string, int> PlayCount { get; set; } = new();

    [JsonProperty("指示物条件", Order = 90)]
    public Dictionary<string, string[]> MarkerConds { get; set; } = new();

    [JsonProperty("范围内玩家检查", Order = 100)]
    public PlrCond PlrRange { get; set; } = new();
    [JsonProperty("范围内怪物检查", Order = 110)]
    public MstCond NpcRange { get; set; } = new();
    [JsonProperty("范围内弹幕检查", Order = 120)]
    public ProjCond PorjRange { get; set; } = new();

    #region 增强条件子类
    public class PlrCond
    {
        [JsonProperty("范围格数")] public string Range { get; set; } = "0,0";
        [JsonProperty("需要玩家数量")] public int MatchCnt { get; set; } = 0;
        [JsonProperty("玩家生命值")] public int HP { get; set; } = -1;
        [JsonProperty("玩家生命百分比")] public int HPRatio { get; set; } = -1;
        [JsonProperty("需要Buff列表")] public int[] Buffs { get; set; } = Array.Empty<int>();
    }

    public class MstCond
    {
        [JsonProperty("怪物标志")] public string Flag { get; set; } = "";
        [JsonProperty("怪物ID")] public int MstID { get; set; } = 0;
        [JsonProperty("范围格数")] public int Range { get; set; } = 0;
        [JsonProperty("需要怪物数量")] public int MatchCnt { get; set; } = 0;
        [JsonProperty("怪物血量百分比")] public int HPRatio { get; set; } = 0;
    }

    public class ProjCond
    {
        [JsonProperty("弹幕标志")] public string Flag { get; set; } = "";
        [JsonProperty("弹幕ID")] public int ProjID { get; set; } = 0;
        [JsonProperty("范围格数")] public int Range { get; set; } = 0;
        [JsonProperty("需要弹幕数量")] public int MatchCnt { get; set; } = 0;
        [JsonProperty("是否全局弹幕")] public bool IsGlobal { get; set; } = false;
    }
    #endregion

    #region 主入口
    /// <summary>条件检查主入口（无 StringBuilder 版本）</summary>
    public static void CondWork(NpcData data, NPC npc, Conditions Cond, ref bool allow)
    {
        var mess = new StringBuilder();
        CondWork(npc, mess, data, Cond, ref allow);
    }

    /// <summary>条件检查主入口（含 StringBuilder 日志）</summary>
    public static void CondWork(NPC npc, StringBuilder mess, NpcData? data, Conditions Cond, ref bool allow)
    {
        if (data is null || Cond is null) return;
        allow = Check(npc, data, Cond, mess);
    }
    #endregion

    #region 基础条件检查
    private static bool Check(NPC npc, NpcData data, Conditions Cond, StringBuilder mess)
    {
        bool all = true;

        NpcState? st = StateApi.GetState(npc);
        if (st == null) return false;

        // 标志条件
        if (!string.IsNullOrEmpty(Cond.CheckFlag))
        {
            bool ok = st.Flag == Cond.CheckFlag;
            if (!ok)
            {
                all = false;
                mess.Append($" 标志条件未满足: 需要 '{Cond.CheckFlag}'\n");
            }
        }

        // 指示物条件
        if (Cond.MarkerConds?.Count > 0 && !MarkManager.ChkMks(st, Cond.MarkerConds, npc))
        {
            all = false;
            mess.Append(" 指示物条件未满足\n");
        }

        // 血量条件
        float lifePct = MathHelper.Clamp(npc.life * 100f / npc.lifeMax, 0f, 100f);
        var (hpOk, hpMin, hpMax) = ParseRng(Cond.NpcLift);
        if (hpOk && !(lifePct >= hpMin && lifePct <= hpMax))
        {
            all = false;
            mess.Append($" 血量条件未满足: {lifePct:F0}% 不在 {Cond.NpcLift}%\n");
        }

        // 怪物速度条件
        if (Cond.Speed != -1)
        {
            float maxSpd = Math.Max(Math.Abs(npc.velocity.X), Math.Abs(npc.velocity.Y));
            if (maxSpd < Cond.Speed)
            {
                all = false;
                mess.Append($" 速度条件未满足: {maxSpd:F0} < {Cond.Speed}\n");
            }
        }

        // AI条件
        if (Cond.AIPairs?.Count > 0 && !CheckNpcAi(npc, mess, Cond))
            all = false;

        // ---- 时间范围 ----
        if (Cond.Timer != "0,0")
        {
            var (ok, min, max) = ParseRng(Cond.Timer);
            if (ok)
            {
                double elapsed = (DateTime.UtcNow - st.Cooldown[st.EventIndex]).TotalSeconds;
                if (!(elapsed >= min && elapsed <= max))
                { all = false; mess.Append($" 时间条件未满足: {elapsed:F1}s 不在 {Cond.Timer}\n"); }
            }
            else
                mess.Append($" 时间格式错误: {Cond.Timer}\n");
        }

        // 事件执行次数
        if (Cond.ExecuteCount.Count > 0)
        {
            foreach (var kv in Cond.ExecuteCount)
            {
                int actual = st.EventCounts?.GetValueOrDefault(kv.Key) ?? 0;
                if (!CheckStack(kv.Value, actual, $"事件[{kv.Key}]", mess))
                    all = false;
            }
        }

        // 文件累计播放
        if (Cond.PlayTotal != -1)
        {
            int total = st.EventCounts?.Values.Sum() ?? 0;
            if (!CheckStack(Cond.PlayTotal, total, "累计播放", mess))
                all = false;
        }

        // 文件播放
        if (Cond.PlayCount.Count > 0)
        {
            foreach (var kv in Cond.PlayCount)
            {
                int actual = st.PlayCounts?.GetValueOrDefault(kv.Key) ?? 0;
                if (!CheckStack(kv.Value, actual, $"文件{kv.Key}", mess)) all = false;
            }
        }

        // 范围条件（玩家、怪物、弹幕）
        if (Cond.PlrRange.MatchCnt > 0)
        {
            var (ok, min, max) = ParseRng(Cond.PlrRange.Range);
            if (ok && max > 0 && !CheckPlayer(npc, Cond.PlrRange, st, min, max, mess))
                all = false;
        }

        if (Cond.NpcRange.MatchCnt > 0 &&
            !CheckNpc(npc, Cond.NpcRange, mess))
            all = false;

        if (Cond.PorjRange.MatchCnt > 0 &&
            !CheckProj(npc, Cond.PorjRange, mess))
            all = false;

        // 目标玩家
        Player? plr = GetTarg(npc);
        if (plr == null) return false;

        // 武器
        if (Cond.WeaponName != "无" && Cond.WeaponName != GetWeapon(plr))
        {
            all = false;
            mess.Append($" 武器条件未满足: {GetWeapon(plr)} ≠ {Cond.WeaponName}\n");
            AutoTar(npc, data);
        }

        // 进度
        if (!ProgGroup(plr, Cond.Progress))
        {
            all = false;
            mess.Append(" 进度条件未满足\n");
        }

        // 数量条件
        if (!CheckStack(Cond.MonsterCount, st.SNCount, "召怪", mess)) all = false;
        if (!CheckStack(Cond.ProjectileCount, st.SPCount, "弹发", mess)) all = false;
        if (!CheckStack(Cond.DeadCount, data.DeadCount, "死亡", mess)) all = false;

        // 距离条件
        if (Cond.Range != -1)
        {
            float distTile = plr.Center.Distance(npc.Center) / 16f;
            if (distTile > Cond.Range)
            {
                all = false;
                mess.Append($" 距离条件未满足: {distTile:F1} > {Cond.Range}格\n");
                AutoTar(npc, data);
            }
        }

        // 玩家生命
        if (Cond.PlayerLife != -1)
        {
            bool LifeOk = Cond.PlayerLife > 0 ? plr.statLife >= Cond.PlayerLife : plr.statLife < Math.Abs(Cond.PlayerLife);
            if (!LifeOk)
            {
                all = false;
                mess.Append(" 生命条件未满足\n");
                AutoTar(npc, data);
            }
        }

        // 防御
        if (Cond.PlrDefense != -1 && plr.statDefense > Cond.PlrDefense)
        {
            all = false;
            mess.Append($" 防御条件未满足: {plr.statDefense} > {Cond.PlrDefense}\n");
            AutoTar(npc, data);
        }

        return all;
    }
    #endregion

    #region 范围条件检查（玩家、怪物、弹幕）
    /// <summary>检查范围内玩家条件</summary>
    private static bool CheckPlayer(NPC npc, PlrCond c, NpcState st, float rMin, float rMax, StringBuilder msg)
    {
        int cnt = 0;
        float minPx = rMin * 16f;
        float maxPx = rMax * 16f;
        if(st.Attack.Count == 0) return false;

        // 从攻击者列表里遍历
        for (int i = st.Attack.Count - 1; i >= 0; i--)
        {
            int pid = st.Attack[i];
            var plr = Main.player[pid];
            if (plr == null || !plr.active || plr.dead || plr.statLife <= 0)
            {
                st.Attack.RemoveAt(i);
                continue;
            }

            // Buff 检查
            if (c.Buffs.Length > 0 && !c.Buffs.All(b => plr.buffType.Contains(b))) continue;

            // 生命值/百分比检查
            bool hpOk = c.HP == -1 || (c.HP > 0 ? plr.statLife >= c.HP : plr.statLife < Math.Abs(c.HP));
            if (!hpOk) continue;
            if (c.HPRatio != -1)
            {
                float ratio = plr.statLife * 100f / plr.statLifeMax2;
                bool ratioOk = c.HPRatio > 0 ? ratio >= c.HPRatio : ratio < Math.Abs(c.HPRatio);
                if (!ratioOk) continue;
            }

            float distSq = npc.Center.DistanceSQ(plr.Center);
            bool inRange = rMin > 0 ? (distSq > minPx * minPx &&
                           distSq <= maxPx * maxPx) : distSq <= maxPx * maxPx;

            if (inRange)
                cnt++;
        }

        return CheckStack(c.MatchCnt, cnt, "范围内玩家", msg);
    }

    /// <summary>检查范围内怪物条件（使用 NpcMap 缓存）</summary>
    private static bool CheckNpc(NPC npc, MstCond c, StringBuilder msg)
    {
        int cnt = 0;
        float rangePx = c.Range * 16f;

        // 只检查敌对NPC 条件筛选已在NpcSpawn完成(排除友好NPC)
        var npcMap = NpcMap;
        for (int i = npcMap.Count - 1; i >= 0; i--)
        {
            int idx = npcMap[i];
            var n = Main.npc[idx];
            if (n == null || !n.active || n.whoAmI == npc.whoAmI)
            {
                npcMap.RemoveAt(i);
                continue;
            }

            // 怪物ID检查
            if (c.MstID != 0 && n.netID != c.MstID) continue;

            // 距离检查
            if (c.Range > 0 && npc.Center.DistanceSQ(n.Center) > rangePx * rangePx) continue;

            // 怪物血量百分比检查
            if (c.HPRatio != 0)
            {
                float r = n.lifeMax > 0 ? MathHelper.Clamp(n.life * 100f / n.lifeMax, 0f, 100f) : 0f;
                bool hpOk = c.HPRatio > 0 ? r >= c.HPRatio : r < Math.Abs(c.HPRatio);
                if (!hpOk) continue;
            }

            // 标志检查
            if (!string.IsNullOrEmpty(c.Flag))
            {
                if (Config.NpcDatas == null) continue;

                if (!Config.NpcDatas.Any(d => d.Type != null &&
                    d.Type.Contains(n.netID) &&
                    StateApi.GetState(n).Flag == c.Flag)) continue;
            }

            cnt++;
        }

        return CheckStack(c.MatchCnt, cnt, "范围内怪物", msg);
    }

    /// <summary>检查范围内弹幕条件（仅遍历 ProjMap 中由 NPC 发射的弹幕）</summary>
    private static bool CheckProj(NPC npc, ProjCond c, StringBuilder msg)
    {
        int cnt = 0;
        float rangePx = c.Range * 16f;

        if (ProjMap.Count == 0) return false;

        // kv.Key = 弹幕索引, kv.Value = 发射者 NPC 索引
        foreach (var kv in ProjMap)
        {
            // 如果要求仅当前 NPC 的弹幕，跳过其它 NPC 的
            if (c.IsGlobal && kv.Value != npc.whoAmI)
                continue;

            // 弹幕索引
            int idx = kv.Key;

            // 检查弹幕有效性
            var p = Main.projectile[idx];
            if (p == null || !p.active || p.owner != Main.myPlayer) continue;

            // 标志检查
            if (!string.IsNullOrEmpty(c.Flag))
            {
                var state = UpProj.GetState(idx);
                if (state == null || state.Notes != c.Flag)
                    continue;
            }

            // 弹幕类型检查
            if (c.ProjID > 0 && p.type != c.ProjID) continue;

            // 弹幕距离NPC范围检查
            if (c.Range > 0 && npc.Center.DistanceSQ(p.Center) > rangePx * rangePx) continue;
            cnt++;
        }

        return CheckStack(c.MatchCnt, cnt, "范围内弹幕", msg);
    }
    #endregion

    #region 辅助方法
    /// <summary>通用计数条件检查（need >0 需不少于，need <0 需小于绝对值）</summary>
    private static bool CheckStack(int need, int actual, string name, StringBuilder msg)
    {
        if (need == 0) return true;
        if (need > 0 && actual < need) { msg.AppendLine($" {name}不足: {need}需{actual}"); return false; }
        if (need < 0 && actual >= Math.Abs(need)) { msg.AppendLine($" {name}过多: <{Math.Abs(need)}现{actual}"); return false; }
        return true;
    }

    /// <summary>AI 条件检查</summary>
    private static bool CheckNpcAi(NPC npc, StringBuilder mess, Conditions c)
    {
        bool all = true;
        foreach (var kv in c.AIPairs)
        {
            if (kv.Key < 0 || kv.Key > 3) { all = false; mess.Append($" AI[{kv.Key}] 索引无效\n"); continue; }
            float val = npc.ai[kv.Key];
            foreach (string expr in kv.Value)
            {
                if (string.IsNullOrWhiteSpace(expr)) continue;
                if (!ParseAI(expr, val))
                { all = false; mess.Append($" AI[{kv.Key}] {expr} 不成立 (当前{val:F2})\n"); }
            }
        }
        return all;
    }

    /// <summary>解析单个 AI 表达式</summary>
    private static bool ParseAI(string expr, float val)
    {
        if (expr.StartsWith("==")) return float.TryParse(expr[2..], out float a) && val == a;
        if (expr.StartsWith("!=")) return float.TryParse(expr[2..], out float b) && val != b;
        if (expr.StartsWith(">=")) return float.TryParse(expr[2..], out float c) && val >= c;
        if (expr.StartsWith("<=")) return float.TryParse(expr[2..], out float d) && val <= d;
        if (expr.StartsWith(">")) return float.TryParse(expr[1..], out float e) && val > e;
        if (expr.StartsWith("<")) return float.TryParse(expr[1..], out float f) && val < f;
        if (expr.StartsWith("=")) return float.TryParse(expr[1..], out float g) && val == g;
        return float.TryParse(expr, out float h) && val == h;
    }

    /// <summary>获取玩家当前手持武器类型</summary>
    public static string GetWeapon(Player p)
    {
        var it = p.HeldItem;
        if (it == null || it.type == 0) return "无";
        if (it.melee && it.maxStack == 1 && it.damage > 0 && it.ammo == 0 && it.pick < 1 && it.hammer < 1 && it.axe < 1) return "近战";
        if (it.ranged && it.maxStack == 1 && it.damage > 0 && it.ammo == 0 && !it.consumable) return "远程";
        if (it.magic && it.maxStack == 1 && it.damage > 0 && it.ammo == 0) return "魔法";
        if (ItemID.Sets.SummonerWeaponThatScalesWithAttackSpeed[it.type]) return "召唤";
        if ((it.maxStack == 9999 && it.damage > 0 && it.ammo == 0 && it.ranged && it.consumable) ||
            ItemID.Sets.ItemsThatCountAsBombsForDemolitionistToSpawn[it.type]) return "投掷物";
        return "未知";
    }

    /// <summary>检查一组进度条件</summary>
    public static bool ProgGroup(Player p, List<string> conds)
    {
        foreach (string c in conds)
            if (!CheckProg(p, c)) return false;
        return true;
    }

    /// <summary>检查单个进度条件</summary>
    public static bool CheckProg(Player p, string cond)
    {
        switch (cond)
        {
            case "0": case "无": return true;
            case "1": case "克眼": case "克苏鲁之眼": return NPC.downedBoss1;
            case "2": case "史莱姆王": case "史王": return NPC.downedSlimeKing;
            case "3":
            case "世吞":
            case "黑长直":
            case "世界吞噬者":
            case "世界吞噬怪":
                return NPC.downedBoss2 && (IsDefeated(NPCID.EaterofWorldsHead) || IsDefeated(NPCID.EaterofWorldsBody) || IsDefeated(NPCID.EaterofWorldsTail));
            case "4":
            case "克脑":
            case "脑子":
            case "克苏鲁之脑":
                return NPC.downedBoss2 && IsDefeated(NPCID.BrainofCthulhu);
            case "5": case "邪恶boss2": case "世吞或克脑": return NPC.downedBoss2;
            case "6": case "巨鹿": case "鹿角怪": return NPC.downedDeerclops;
            case "7": case "蜂王": return NPC.downedQueenBee;
            case "8": case "骷髅王前": return !NPC.downedBoss3;
            case "9": case "吴克": case "骷髅王": case "骷髅王后": return NPC.downedBoss3;
            case "10": case "肉前": return !Main.hardMode;
            case "11": case "困难模式": case "肉山": case "肉后": case "血肉墙": return Main.hardMode;
            case "12": case "毁灭者": case "铁长直": return NPC.downedMechBoss1;
            case "13": case "双子眼": case "双子魔眼": return NPC.downedMechBoss2;
            case "14": case "铁吴克": case "机械吴克": case "机械骷髅王": return NPC.downedMechBoss3;
            case "15": case "世纪之花": case "花后": case "世花": return NPC.downedPlantBoss;
            case "16": case "石后": case "石巨人": return NPC.downedGolemBoss;
            case "17": case "史后": case "史莱姆皇后": return NPC.downedQueenSlime;
            case "18": case "光之女皇": case "光女": return NPC.downedEmpressOfLight;
            case "19": case "猪鲨": case "猪龙鱼公爵": return NPC.downedFishron;
            case "20": case "拜月": case "拜月教": case "教徒": case "拜月教邪教徒": return NPC.downedAncientCultist;
            case "21": case "月总": case "月亮领主": return NPC.downedMoonlord;
            case "22": case "哀木": return NPC.downedHalloweenTree;
            case "23": case "南瓜王": return NPC.downedHalloweenKing;
            case "24": case "常绿尖叫怪": return NPC.downedChristmasTree;
            case "25": case "冰雪女王": return NPC.downedChristmasIceQueen;
            case "26": case "圣诞坦克": return NPC.downedChristmasSantank;
            case "27": case "火星飞碟": return NPC.downedMartians;
            case "28": case "小丑": return NPC.downedClown;
            case "29": case "日耀柱": return NPC.downedTowerSolar;
            case "30": case "星旋柱": return NPC.downedTowerVortex;
            case "31": case "星云柱": return NPC.downedTowerNebula;
            case "32": case "星尘柱": return NPC.downedTowerStardust;
            case "33": case "一王后": case "任意机械boss": return NPC.downedMechBossAny;
            case "34": case "三王后": return NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3;
            case "35": case "一柱后": return NPC.downedTowerNebula || NPC.downedTowerSolar || NPC.downedTowerStardust || NPC.downedTowerVortex;
            case "36": case "四柱后": return NPC.downedTowerNebula && NPC.downedTowerSolar && NPC.downedTowerStardust && NPC.downedTowerVortex;
            case "37": case "哥布林入侵": return NPC.downedGoblins;
            case "38": case "海盗入侵": return NPC.downedPirates;
            case "39": case "霜月": return NPC.downedFrost;
            case "40": case "血月": return Main.bloodMoon;
            case "41": case "雨天": return Main.raining;
            case "42": case "白天": return Main.dayTime;
            case "43": case "晚上": return !Main.dayTime;
            case "44": case "大风天": return Main.IsItAHappyWindyDay;
            case "45": case "万圣节": return Main.halloween;
            case "46": case "圣诞节": return Main.xMas;
            case "47": case "派对": return BirthdayParty.PartyIsUp;
            case "48": case "旧日一": case "黑暗法师": case "撒旦一": return DD2Event._downedDarkMageT1;
            case "49": case "旧日二": case "巨魔": case "食人魔": case "撒旦二": return DD2Event._downedOgreT2;
            case "50": case "旧日三": case "贝蒂斯": case "双足翼龙": case "撒旦三": return DD2Event._spawnedBetsyT3;
            case "51": case "2020": case "醉酒": case "醉酒种子": case "醉酒世界": return Main.drunkWorld;
            case "52": case "2021": case "十周年": case "十周年种子": return Main.tenthAnniversaryWorld;
            case "53": case "ftw": case "真实世界": case "真实世界种子": return Main.getGoodWorld;
            case "54": case "ntb": case "蜜蜂世界": case "蜜蜂世界种子": return Main.notTheBeesWorld;
            case "55": case "dst": case "饥荒": case "永恒领域": return Main.dontStarveWorld;
            case "56": case "remix": case "颠倒": case "颠倒世界": case "颠倒种子": return Main.remixWorld;
            case "57": case "noTrap": case "陷阱种子": case "陷阱世界": return Main.noTrapsWorld;
            case "58": case "天顶": case "天顶种子": case "缝合种子": case "天顶世界": case "缝合世界": return Main.zenithWorld;
            case "59": case "森林": return p.ShoppingZone_Forest;
            case "60": case "丛林": return p.ZoneJungle;
            case "61": case "沙漠": return p.ZoneDesert;
            case "62": case "雪原": return p.ZoneSnow;
            case "63": case "洞穴": return p.ZoneRockLayerHeight;
            case "64": case "海洋": return p.ZoneBeach;
            case "65": case "地表": return (p.position.Y / 16) <= Main.worldSurface;
            case "66": case "太空": return (p.position.Y / 16) <= (Main.worldSurface * 0.35);
            case "67": case "地狱": return (p.position.Y / 16) >= Main.UnderworldLayer;
            case "68": case "神圣": return p.ZoneHallow;
            case "69": case "蘑菇": return p.ZoneGlowshroom;
            case "70": case "腐化": case "腐化地": case "腐化环境": return p.ZoneCorrupt;
            case "71": case "猩红": case "猩红地": case "猩红环境": return p.ZoneCrimson;
            case "72": case "邪恶": case "邪恶环境": return p.ZoneCrimson || p.ZoneCorrupt;
            case "73": case "地牢": return p.ZoneDungeon;
            case "74": case "墓地": return p.ZoneGraveyard;
            case "75": case "蜂巢": return p.ZoneHive;
            case "76": case "神庙": return p.ZoneLihzhardTemple;
            case "77": case "沙尘暴": return p.ZoneSandstorm;
            case "78": case "天空": return p.ZoneSkyHeight;
            case "79": case "满月": return Main.moonPhase == 0;
            case "80": case "亏凸月": return Main.moonPhase == 1;
            case "81": case "下弦月": return Main.moonPhase == 2;
            case "82": case "残月": return Main.moonPhase == 3;
            case "83": case "新月": return Main.moonPhase == 4;
            case "84": case "娥眉月": return Main.moonPhase == 5;
            case "85": case "上弦月": return Main.moonPhase == 6;
            case "86": case "盈凸月": return Main.moonPhase == 7;
            default: TShock.Log.ConsoleWarn($"{LogName} 未知条件: {cond}"); return false;
        }
    }

    private static bool IsDefeated(int type)
    {
        var entry = Main.BestiaryDB.FindEntryByNPCID(type);
        return entry.UIInfoProvider.GetEntryUICollectionInfo().UnlockState == Terraria.GameContent.Bestiary.BestiaryEntryUnlockState.CanShowDropsWithDropRates_4;
    }
    #endregion
}