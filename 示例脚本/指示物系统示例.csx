using System;
using System.Linq;
using Terraria;
using TShockAPI;

// 安全检查
if (Npc == null) return "NPC为空";
if (State == null) return "State为空";
if (Msg == null) return "Msg为空";

Msg.AppendLine("=== 指示物系统演示 ===");

// 1. 初始化指示物
if (!State.Markers.ContainsKey("AttackCount"))
{
    State.Markers["AttackCount"] = 0;
}

if (!State.Markers.ContainsKey("SkillCooldown"))
{
    State.Markers["SkillCooldown"] = 0;
}

if (!State.Markers.ContainsKey("Phase"))
{
    State.Markers["Phase"] = 1;
}

// 2. 更新攻击计数
State.Markers["AttackCount"]++;
Msg.AppendLine($"攻击计数: {State.Markers["AttackCount"]}");

// 3. 冷却处理
if (State.Markers["SkillCooldown"] > 0)
{
    State.Markers["SkillCooldown"]--;
    Msg.AppendLine($"特殊技能冷却剩余: {State.Markers["SkillCooldown"]}秒");
}

// 4. 攻击计数触发特殊技能
if (State.Markers["AttackCount"] % 10 == 0 && State.Markers["SkillCooldown"] == 0)
{
    Msg.AppendLine("触发特殊技能!");

    // 执行特殊技能逻辑
    State.Markers["SkillCooldown"] = 30; // 30秒冷却
    State.Markers["Phase"]++; // 进入下一阶段

    // 技能效果
    Npc.velocity *= 1.5f; // 暂时加速
    CombatText.NewText(Npc.getRect(), Color.Yellow, "Special Skill!", true);

    // 生成特效粒子
    for (int i = 0; i < 10; i++)
    {
        Dust.NewDust(Npc.position, Npc.width, Npc.height, 57, 0f, 0f, 100, Color.Gold, 2f);
    }
}

// 5. 阶段效果
int currentPhase = State.Markers["Phase"];
Msg.AppendLine($"当前阶段: {currentPhase}");

switch (currentPhase)
{
    case 1:
        // 第一阶段：正常
        Npc.defense = 10;
        break;
    case 2:
        // 第二阶段：强化
        Npc.defense = 20;
        Npc.damage = (int)(Npc.damage * 1.2f);
        break;
    case 3:
        // 第三阶段：狂暴
        Npc.defense = 30;
        Npc.damage = (int)(Npc.damage * 1.5f);
        Npc.velocity *= 1.2f;
        break;
    default:
        // 第四阶段及以上：极限
        Npc.defense = 40;
        Npc.damage *= 2;
        Npc.velocity *= 1.5f;
        break;
}

// 6. 跨NPC指示物操作（如果其他NPC有相同标志）
var sameFlagNPCs = StateApi.FindByFlag(Npc.FullName);
if (sameFlagNPCs.Count > 1)
{
    Msg.AppendLine($"发现 {sameFlagNPCs.Count} 个相同标志的NPC");

    // 同步阶段信息
    StateApi.SetByFlag(Npc.FullName, "Phase", currentPhase);

    // 如果自身是指挥者，可以控制其他NPC
    if (Npc.whoAmI == sameFlagNPCs.First())
    {
        Msg.AppendLine("作为指挥者，同步所有同标志NPC的指示物");
        StateApi.AddByFlag(Npc.FullName, "CommandSync", 1);
    }
}

// 7. 输出当前所有指示物
Msg.AppendLine("=== 当前指示物 ===");
foreach (var marker in State.Markers)
{
    if (marker.Value != 0) // 只显示非零的
    {
        Msg.AppendLine($"  {marker.Key}: {marker.Value}");
    }
}

return $"指示物操作完成，阶段: {currentPhase}";