using System;
using Terraria;
using Microsoft.Xna.Framework;
using TShockAPI;

// 安全检查
if (Npc == null) return "NPC为空";
if (State == null) return "State为空";
if (Msg == null) return "Msg为空";

Msg.AppendLine("=== 弹幕生成演示 ===");

// 获取玩家目标
if (Npc.target < 0 || Npc.target >= Main.maxPlayers)
{
    Msg.AppendLine("没有有效目标");
    return "无效目标";
}

var target = Main.player[Npc.target];
if (target == null || !target.active || target.dead)
{
    Msg.AppendLine("目标无效或已死亡");
    return "目标无效";
}

var npcCenter = Npc.Center;
var targetCenter = target.Center;
var direction = targetCenter - npcCenter;

if (direction == Vector2.Zero)
{
    Msg.AppendLine("方向向量为零");
    direction = new Vector2(1, 0); // 默认向右
}

direction.Normalize();

// 1. 简单直线弹幕
Msg.AppendLine("发射直线弹幕");
for (int i = 0; i < 3; i++)
{
    var velocity = direction * 10f;
    velocity = velocity.RotatedBy(MathHelper.ToRadians(-15 + i * 15));

    try
    {
        int proj = Projectile.NewProjectile(
            Npc.GetSpawnSourceForNPCFromNPCAI(),
            npcCenter,
            velocity,
            113, // 弹幕ID
            30,  // 伤害
            5f,  // 击退
            Main.myPlayer
        );

        if (proj >= 0 && proj < Main.maxProjectiles)
        {
            Main.projectile[proj].timeLeft = 300; // 5秒存在时间
            Msg.AppendLine($"  发射弹幕{i + 1}: ID={proj}, 速度={velocity.Length():F1}");
        }
    }
    catch (Exception ex)
    {
        Msg.AppendLine($"  发射弹幕{i + 1}失败: {ex.Message}");
    }
}

// 2. 圆形弹幕
Msg.AppendLine("发射圆形弹幕");
int circleCount = 8;
float circleSpeed = 8f;

for (int i = 0; i < circleCount; i++)
{
    float angle = MathHelper.TwoPi / circleCount * i;
    var circleVelocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * circleSpeed;

    try
    {
        int proj = Projectile.NewProjectile(
            Npc.GetSpawnSourceForNPCFromNPCAI(),
            npcCenter,
            circleVelocity,
            115, // 弹幕ID
            20,  // 伤害
            3f,  // 击退
            Main.myPlayer
        );

        if (proj >= 0 && proj < Main.maxProjectiles)
        {
            Main.projectile[proj].timeLeft = 180; // 3秒存在时间
        }
    }
    catch (Exception ex)
    {
        Msg.AppendLine($"  圆形弹幕{i + 1}失败: {ex.Message}");
    }
}

// 3. 追踪弹幕（仅当追踪弹幕ID存在时）
try
{
    Msg.AppendLine("发射追踪弹幕");
    var homingVelocity = direction * 6f;
    int homingProj = Projectile.NewProjectile(
        Npc.GetSpawnSourceForNPCFromNPCAI(),
        npcCenter,
        homingVelocity,
        440, // 追踪弹幕ID
        40,  // 伤害
        6f,  // 击退
        Main.myPlayer,
        0f,  // ai0
        1f   // ai1 - 追踪强度
    );

    if (homingProj >= 0 && homingProj < Main.maxProjectiles)
    {
        Msg.AppendLine($"  发射追踪弹幕: ID={homingProj}");
        Main.projectile[homingProj].timeLeft = 600; // 10秒存在时间
    }
}
catch (Exception ex)
{
    Msg.AppendLine($"  追踪弹幕失败: {ex.Message}");
}

// 4. 爆炸特效弹幕
try
{
    Msg.AppendLine("发射爆炸弹幕");
    int explosionProj = Projectile.NewProjectile(
        Npc.GetSpawnSourceForNPCFromNPCAI(),
        targetCenter,
        Vector2.Zero,
        134, // 爆炸弹幕ID
        50,  // 伤害
        10f, // 击退
        Main.myPlayer
    );

    if (explosionProj >= 0 && explosionProj < Main.maxProjectiles)
    {
        Msg.AppendLine("  发射爆炸弹幕到玩家位置");
        Main.projectile[explosionProj].timeLeft = 60; // 1秒后爆炸
    }
}
catch (Exception ex)
{
    Msg.AppendLine($"  爆炸弹幕失败: {ex.Message}");
}

// 5. 随机散射弹幕
Msg.AppendLine("发射随机散射弹幕");
for (int i = 0; i < 5; i++)
{
    try
    {
        var randomVel = new Vector2(
            (Main.rand?.NextFloat() ?? 0.5f) * 20f - 10f,
            (Main.rand?.NextFloat() ?? 0.5f) * 20f - 10f
        );

        int proj = Projectile.NewProjectile(
            Npc.GetSpawnSourceForNPCFromNPCAI(),
            npcCenter + new Vector2(0, -20), // 从头部发射
            randomVel,
            131, // 散射弹幕ID
            15,  // 伤害
            2f,  // 击退
            Main.myPlayer
        );

        if (proj >= 0 && proj < Main.maxProjectiles)
        {
            Main.projectile[proj].timeLeft = 120; // 2秒存在时间
        }
    }
    catch (Exception ex)
    {
        Msg.AppendLine($"  散射弹幕{i + 1}失败: {ex.Message}");
    }
}

// 更新发射计数
if (State != null)
{
    if (!State.Markers.ContainsKey("ProjectileCount"))
    {
        State.Markers["ProjectileCount"] = 0;
    }
    State.Markers["ProjectileCount"]++;

    return $"弹幕发射完成，总计发射 {State.Markers["ProjectileCount"]} 次";
}

return "弹幕发射完成";