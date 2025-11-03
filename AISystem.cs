using System.Text;
using Newtonsoft.Json;
using Terraria;

namespace MonsterSpeed;

// 新增：AI模式配置类
public class AIModes
{
    [JsonProperty("启用模式", Order = -2)]
    public bool Enabled { get; set; } = false;

    [JsonProperty("固定AI", Order = -1)]
    public Dictionary<int, float> FixedAI { get; set; } = new Dictionary<int, float>();
    [JsonProperty("步进AI", Order = 0)]
    public Dictionary<int, AISetting> StepAI { get; set; } = new Dictionary<int, AISetting>();
    [JsonProperty("固定LocalAI", Order = 0)]
    public Dictionary<int, float> FixedLocalAI { get; set; } = new Dictionary<int, float>();
    [JsonProperty("步进LocalAI", Order = 2)]
    public Dictionary<int, AISetting> StepLocalAI { get; set; } = new Dictionary<int, AISetting>();
    [JsonProperty("原版AI", Order = 2)]
    public List<BossAI> BossAI { get; set; } = new List<BossAI>();
}

// 步进AI的配置
public class AISetting
{
    [JsonProperty("模式")]
    public int Type { get; set; } = 2;
    [JsonProperty("步长")]
    public float Step { get; set; } = 1.0f;
    [JsonProperty("最小")]
    public float MinValue { get; set; } = -10.0f;
    [JsonProperty("最大")]
    public float MaxValue { get; set; } = 10.0f;
    [JsonProperty("循环")]
    public bool Loop { get; set; } = true;
}

// 新增：AI模式状态类
public class AIState
{
    public Dictionary<int, int> Directions { get; set; } = new Dictionary<int, int>();
    public Dictionary<int, float> Values { get; set; } = new Dictionary<int, float>();
    public Dictionary<int, int> LocalDirections { get; set; } = new Dictionary<int, int>();
    public Dictionary<int, float> LocalValues { get; set; } = new Dictionary<int, float>();
}

// 原版 BOSS AI
public class BossAI
{
    [JsonProperty("保持头顶", Order = -1)]
    public bool AlwaysTop { get; set; } = false;
    [JsonProperty("白光AI", Order = 0)]
    public bool HallowBoss { get; set; } = false;
    [JsonProperty("猪鲨AI", Order = 1)]
    public bool DukeFishron { get; set; } = false;
    [JsonProperty("鹿角怪AI", Order = 2)]
    public bool Deerclops { get; set; } = false;
    [JsonProperty("鹦鹉螺AI", Order = 3)]
    public bool BloodNautilus { get; set; } = false;
}

internal class AISystem
{
    #region 应用步进AI模式控制
    // 新增：存储每个NPC的AI模式状态
    private static Dictionary<string, AIState> AIPattern = new Dictionary<string, AIState>();
    public static void AIPairs(NPC npc, AIModes aiMode, string npcName, ref bool handled)
    {
        if (!aiMode.Enabled) return;
        // 初始化或获取模式状态
        if (!AIPattern.TryGetValue(npcName, out var state))
        {
            state = new AIState
            {
                Values = new Dictionary<int, float>(),
                Directions = new Dictionary<int, int>(),
                LocalValues = new Dictionary<int, float>(),
                LocalDirections = new Dictionary<int, int>()
            };
            AIPattern[npcName] = state;
        }

        bool flag = false;

        // 处理固定AI
        if (aiMode.FixedAI != null && aiMode.FixedAI.Count > 0)
        {
            FixedAI(npc, aiMode.FixedAI, state);
            flag = true;
        }

        // 处理固定localAI
        if (aiMode.FixedLocalAI != null && aiMode.FixedLocalAI.Count > 0)
        {
            FixedLocalAI(npc, aiMode.FixedLocalAI, state);
            flag = true;
        }

        // 处理步进AI
        if (aiMode.StepAI != null && aiMode.StepAI.Count > 0)
        {
            StepAI(npc, aiMode.StepAI, state, npcName, false);
            flag = true;
        }

        // 处理步进localAI
        if (aiMode.StepLocalAI != null && aiMode.StepLocalAI.Count > 0)
        {
            StepAI(npc, aiMode.StepLocalAI, state, npcName, true);
            flag = true;
        }

        handled = !flag;
    }
    #endregion

    #region 固定AI
    private static void FixedAI(NPC npc, Dictionary<int, float> fixedAI, AIState state)
    {
        foreach (var kvp in fixedAI)
        {
            int key = kvp.Key;
            float val = kvp.Value;
            if (key >= 0 && key < npc.ai.Length)
            {
                npc.ai[key] = val;
                state.Values[key] = val;
            }
        }
    }
    #endregion

    #region 固定localAI
    private static void FixedLocalAI(NPC npc, Dictionary<int, float> fixedLocalAI, AIState state)
    {
        foreach (var kvp in fixedLocalAI)
        {
            int key = kvp.Key;
            float val = kvp.Value;
            if (key >= 0 && key < npc.localAI.Length)
            {
                npc.localAI[key] = val;
                state.LocalValues[key] = val;
            }
        }
    }
    #endregion

    #region 步进AI
    private static Random random = new Random(); // 静态Random实例
    private static void StepAI(NPC npc, Dictionary<int, AISetting> stepAI, AIState state, string npcName, bool isLocalAI)
    {
        foreach (var kvp in stepAI)
        {
            int Index = kvp.Key; // 键就是AI索引
            AISetting setting = kvp.Value;

            // 检查索引范围
            var target = isLocalAI ? npc.localAI : npc.ai;
            if (Index < 0 || Index >= target.Length) continue;

            // 初始化当前值
            var val = isLocalAI ? state.LocalValues : state.Values;
            var Dict = isLocalAI ? state.LocalDirections : state.Directions;
            if (!val.ContainsKey(Index))
            {
                val[Index] = target[Index];
            }

            // 初始化方向（用于往复模式）
            if (!Dict.ContainsKey(Index) && setting.Type == 2)
            {
                Dict[Index] = 1;
            }

            float Value = state.Values[Index];
            float newVal = Value;

            // 根据模式类型计算新值
            switch (setting.Type)
            {
                case 0: // 递增模式
                    newVal = Value + setting.Step;
                    if (newVal > setting.MaxValue)
                    {
                        newVal = setting.Loop ? setting.MinValue : setting.MaxValue;
                    }
                    break;

                case 1: // 递减模式
                    newVal = Value - setting.Step;
                    if (newVal < setting.MinValue)
                    {
                        newVal = setting.Loop ? setting.MaxValue : setting.MinValue;
                    }
                    break;

                case 2: // 往复模式
                    newVal = Value + (setting.Step * Dict[Index]);
                    if (newVal >= setting.MaxValue || newVal <= setting.MinValue)
                    {
                        // 使用 Math.Clamp 确保值在范围内
                        newVal = Math.Clamp(newVal, setting.MinValue, setting.MaxValue);
                        // 反转方向
                        Dict[Index] *= -1;
                    }
                    break;

                case 3: //  随机模式
                    newVal = setting.Loop && random.NextDouble() < 0.1 ?
                             setting.MinValue : (float)(random.NextDouble() * (setting.MaxValue - setting.MinValue) + setting.MinValue);
                    break;
            }

            // 对于非随机模式，限制（随机模式已经限制在范围内，所以不需要额外限制）
            if (setting.Type != 3)
            {
                newVal = Math.Max(setting.MinValue, Math.Min(setting.MaxValue, newVal));
            }

            // 更新AI值
            target[Index] = newVal;
            val[Index] = newVal;
        }
    }
    #endregion

    #region 输出正在赋值的AI信息
    private static string GetModeName(int type)
    {
        return type switch
        {
            0 => "递增",
            1 => "递减",
            2 => "往复",
            3 => "随机",
            _ => "未知"
        };
    }

    public static string GetAiInfo(AIModes aiMode, string npcName)
    {
        var info = new StringBuilder();
        if (AIPattern.TryGetValue(npcName, out var state))
        {
            // 显示固定AI
            if (aiMode.FixedAI != null && aiMode.FixedAI.Count > 0)
            {
                info.Append("\n [固定] ");
                foreach (var kvp in aiMode.FixedAI)
                {
                    info.Append($" [ai{kvp.Key}] {kvp.Value:F1} ");
                }
            }

            // 显示固定localAI
            if (aiMode.FixedLocalAI != null && aiMode.FixedLocalAI.Count > 0)
            {
                info.Append("\n [固定] ");
                foreach (var kvp in aiMode.FixedLocalAI)
                {
                    info.Append($" [Lai{kvp.Key}] {kvp.Value:F1} ");
                }
            }

            // 显示步进AI
            if (aiMode.StepAI != null && aiMode.StepAI.Count > 0)
            {
                info.Append("\n [步进] ");
                foreach (var kvp in aiMode.StepAI)
                {
                    var aiIndex = kvp.Key;
                    var setting = kvp.Value;
                    if (state.Values.ContainsKey(aiIndex))
                    {
                        string modeName = GetModeName(setting.Type);
                        info.Append($" [ai{aiIndex}] {state.Values[aiIndex]:F1}({modeName}) ");
                    }
                }
            }

            // 显示步进localAI
            if (aiMode.StepLocalAI != null && aiMode.StepLocalAI.Count > 0)
            {
                info.Append("\n [步进] ");
                foreach (var kvp in aiMode.StepLocalAI)
                {
                    var aiIndex = kvp.Key;
                    var setting = kvp.Value;
                    if (state.LocalValues.ContainsKey(aiIndex))
                    {
                        string modeName = GetModeName(setting.Type);
                        info.Append($" [Lai{aiIndex}] {state.LocalValues[aiIndex]:F1}({modeName}) ");
                    }
                }
            }
        }

        // 对结果应用渐变色效果
        string result = info.ToString().Trim();
        if (!string.IsNullOrEmpty(result))
        {
            return Tool.TextGradient(result);
        }

        return result;
    }
    #endregion

    #region 泰拉瑞亚 Boss AI
    public static void TR_AI(BossAI bossAI, NPC npc, ref bool handled)
    {
        Player plr = Main.player[npc.target];

        var flag = false;

        //始终保持保持玩家头顶
        if (bossAI.AlwaysTop && !plr.dead && plr.active)
        {
            npc.AI_120_HallowBoss_DashTo(plr.position);
            flag = true;
        }

        //猪鲨AI
        if (bossAI.DukeFishron)
        {
            npc.AI_069_DukeFishron();
            flag = true;
        }

        //鹦鹉螺AI
        if (bossAI.BloodNautilus)
        {
            npc.AI_117_BloodNautilus();
            flag = true;
        }

        //白光AI
        if (bossAI.HallowBoss)
        {
            npc.AI_120_HallowBoss();
            flag = true;
        }

        //鹿角怪AI
        if (bossAI.Deerclops)
        {
            npc.AI_123_Deerclops();
            flag = true;
        }

        handled = !flag;
    }
    #endregion
}
