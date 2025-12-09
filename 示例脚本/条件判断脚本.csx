using System;
using Terraria;
using Microsoft.Xna.Framework;

// 安全检查
if (Npc == null) return "NPC为空";
if (State == null) return "State为空";
if (Msg == null) return "Msg为空";

Msg.AppendLine("=== 血量条件检查 ===");

var lifePercent = (float)Npc.life / Npc.lifeMax * 100;
Msg.AppendLine($"当前血量百分比: {lifePercent:F1}%");

// 血量阶段判断
if (lifePercent >= 80)
{
    Msg.AppendLine("阶段: 正常");
    Npc.defense = 0;
}
else if (lifePercent >= 50)
{
    Msg.AppendLine("阶段: 警告");
    Npc.defense = 15;

    // 偶尔闪现红色（20分之1几率）
    if (Main.rand.Next(20) == 0)
    {
        CombatText.NewText(Npc.getRect(), Color.Red, "危险!", true);
    }
}
else if (lifePercent >= 20)
{
    Msg.AppendLine("阶段: 危险");
    Npc.defense = 30;
    Npc.velocity *= 1.3f; // 加速30%

    // 闪烁红光（10分之1几率）
    if (Main.rand.Next(10) == 0)
    {
        CombatText.NewText(Npc.getRect(), Color.DarkRed, "狂暴!", true);
    }
}
else
{
    Msg.AppendLine("阶段: 濒死");
    Npc.defense = 50;
    Npc.velocity *= 1.5f; // 加速50%
    Npc.damage *= 2; // 攻击力翻倍

    CombatText.NewText(Npc.getRect(), Color.Crimson, "濒死狂暴!", true);
}

// 添加指示物记录
if (!State.Markers.ContainsKey("HealthStage"))
{
    State.Markers["HealthStage"] = 0;
}

int oldStage = State.Markers["HealthStage"];
int newStage = lifePercent switch
{
    >= 80 => 1,
    >= 50 => 2,
    >= 20 => 3,
    _ => 4
};

if (oldStage != newStage)
{
    State.Markers["HealthStage"] = newStage;
    Msg.AppendLine($"血量阶段变更: {oldStage} -> {newStage}");

    // 阶段切换特效
    for (int i = 0; i < 5; i++)
    {
        Dust.NewDust(Npc.position, Npc.width, Npc.height, 6, 0f, 0f, 150, Color.Red, 1.5f);
    }
}

return $"血量阶段: {newStage}";