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
    public static void AIPairs(NPC npc, AIModes aiMode, string npcName)
    {
        if (!aiMode.Enabled) return;

        // 初始化或获取模式状态
        if (!AIPattern.TryGetValue(npcName, out var state))
        {
            state = new AIState
            {
                Values = new Dictionary<int, float>(),
                Directions = new Dictionary<int, int>()
            };
            AIPattern[npcName] = state;
        }

        // 处理固定AI
        if (aiMode.FixedAI != null && aiMode.FixedAI.Count > 0)
        {
            FixedAI(npc, aiMode.FixedAI, state);
        }

        // 处理步进AI
        if (aiMode.StepAI != null && aiMode.StepAI.Count > 0)
        {
            StepAI(npc, aiMode.StepAI, state, npcName);
        }
    }
    #endregion

    #region 固定AI
    private static void FixedAI(NPC npc, Dictionary<int, float> fixedAI, AIState state)
    {
        foreach (var kvp in fixedAI)
        {
            int Index = kvp.Key;
            float fixedValue = kvp.Value;
            if (Index >= 0 && Index < npc.ai.Length)
            {
                npc.ai[Index] = fixedValue;
                state.Values[Index] = fixedValue;
            }
        }
    }
    #endregion

    #region 步进AI
    private static Random random = new Random(); // 静态Random实例
    private static void StepAI(NPC npc, Dictionary<int, AISetting> stepAI, AIState state, string npcName)
    {
        foreach (var kvp in stepAI)
        {
            int Index = kvp.Key; // 键就是AI索引
            AISetting setting = kvp.Value;

            if (Index < 0 || Index >= npc.ai.Length) continue;

            // 初始化当前值
            if (!state.Values.ContainsKey(Index))
            {
                state.Values[Index] = npc.ai[Index];
            }

            // 初始化方向（用于往复模式）
            if (!state.Directions.ContainsKey(Index) && setting.Type == 2)
            {
                state.Directions[Index] = 1;
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
                    newVal = Value + (setting.Step * state.Directions[Index]);
                    if (newVal >= setting.MaxValue)
                    {
                        newVal = setting.MaxValue;
                        state.Directions[Index] = -1;
                    }
                    else if (newVal <= setting.MinValue)
                    {
                        newVal = setting.MinValue;
                        state.Directions[Index] = 1;
                    }
                    break;

                case 3: //  随机模式
                    if (setting.Loop && random.NextDouble() < 0.1)
                    {
                        newVal = setting.MinValue;
                    }
                    else
                    {
                        // 禁用循环模式：完全随机，无特殊逻辑
                        newVal = (float)(random.NextDouble() * (setting.MaxValue - setting.MinValue) + setting.MinValue);
                    }
                    break;
            }

            // 对于非随机模式，限制（随机模式已经限制在范围内，所以不需要额外限制）
            if (setting.Type != 3)
            {
                newVal = Math.Max(setting.MinValue, Math.Min(setting.MaxValue, newVal));
            }

            // 更新AI值
            npc.ai[Index] = newVal;
            state.Values[Index] = newVal;
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
                info.Append("固定:");
                foreach (var kvp in aiMode.FixedAI)
                {
                    info.Append($"ai{kvp.Key}:{kvp.Value:F1} ");
                }
            }

            // 显示步进AI
            if (aiMode.StepAI != null && aiMode.StepAI.Count > 0)
            {
                info.Append("步进:");
                foreach (var kvp in aiMode.StepAI)
                {
                    var aiIndex = kvp.Key;
                    var setting = kvp.Value;
                    if (state.Values.ContainsKey(aiIndex))
                    {
                        string modeName = GetModeName(setting.Type);
                        info.Append($"ai{aiIndex}:{state.Values[aiIndex]:F1}({modeName}) ");
                    }
                }
            }
        }

        return info.ToString().Trim();
    }
    #endregion
}
