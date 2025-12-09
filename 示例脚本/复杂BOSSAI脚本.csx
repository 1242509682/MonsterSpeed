using System;
using System.Linq;
using Terraria;
using Microsoft.Xna.Framework;
using TShockAPI;

// 安全检查
if (Npc == null) return "NPC为空";
if (State == null) return "State为空";
if (Msg == null) return "Msg为空";

Msg.AppendLine("=== BOSS AI 演示 ===");

// 获取所有玩家
var players = TShock.Players.Where(p => p != null && p.Active && !p.Dead).ToList();
if (players.Count == 0)
{
    return "没有玩家";
}

// 智能目标选择
Player bestTarget = null;
float bestScore = 0;

foreach (var plr in players)
{
    var player = plr.TPlayer;
    if (player == null || !player.active || player.dead) continue;

    // 计算分数：距离越近分数越高，同时考虑玩家生命值
    float distance = Vector2.Distance(Npc.Center, player.Center);
    float distanceScore = Math.Max(0, 1000 - distance); // 距离越近分数越高
    float healthScore = (float)player.statLife / player.statLifeMax2 * 100; // 血量百分比

    // 优先攻击低血量玩家
    float totalScore = distanceScore + (100 - healthScore) * 10;

    if (totalScore > bestScore)
    {
        bestScore = totalScore;
        bestTarget = player;
    }
}

if (bestTarget != null)
{
    // 切换目标
    Npc.target = bestTarget.whoAmI;
    Msg.AppendLine($"选择目标: {bestTarget.name} (分数: {bestScore:F0})");
}

// 动态阶段系统
if (!State.Markers.ContainsKey("Phase"))
{
    State.Markers["Phase"] = 1;
}

int phase = State.Markers["Phase"];
float lifePercent = (float)Npc.life / Npc.lifeMax * 100;

// 根据血量自动调整阶段
int newPhase = lifePercent switch
{
    >= 80 => 1,
    >= 60 => 2,
    >= 40 => 3,
    >= 20 => 4,
    _ => 5
};

if (newPhase != phase)
{
    State.Markers["Phase"] = newPhase;
    Msg.AppendLine($"阶段变更: {phase} -> {newPhase}");

    // 阶段切换特效
    switch (newPhase)
    {
        case 2:
            CombatText.NewText(Npc.getRect(), Color.Yellow, "Phase 2: Defense Up", true);
            Npc.defense += 20;
            break;
        case 3:
            CombatText.NewText(Npc.getRect(), Color.Orange, "Phase 3: Speed Up", true);
            Npc.velocity *= 1.3f;
            break;
        case 4:
            CombatText.NewText(Npc.getRect(), Color.Red, "Phase 4: Attack Up", true);
            Npc.damage *= 2;
            break;
        case 5:
            CombatText.NewText(Npc.getRect(), Color.DarkRed, "Final Phase: Berserk", true);
            Npc.defense += 30;
            Npc.velocity *= 1.5f;
            Npc.damage *= 3;
            break;
    }
}

// 技能冷却系统
if (!State.Markers.ContainsKey("SkillCooldown"))
{
    State.Markers["SkillCooldown"] = 0;
}

if (State.Markers["SkillCooldown"] > 0)
{
    State.Markers["SkillCooldown"]--;
}
else
{
    // 根据阶段使用不同技能
    switch (phase)
    {
        case 1:
            if (Main.rand.Next(30) == 0) // 每30帧有1/30几率
            {
                ShootFanProjectiles();
                State.Markers["SkillCooldown"] = 180; // 3秒冷却
            }
            break;
        case 2:
            if (Main.rand.Next(20) == 0)
            {
                ShootHomingProjectiles();
                State.Markers["SkillCooldown"] = 120; // 2秒冷却
            }
            break;
        case 3:
            if (Main.rand.Next(15) == 0)
            {
                SpawnMinions();
                State.Markers["SkillCooldown"] = 240; // 4秒冷却
            }
            break;
        case 4:
            if (Main.rand.Next(10) == 0)
            {
                ScreenExplosion();
                State.Markers["SkillCooldown"] = 300; // 5秒冷却
            }
            break;
        case 5:
            // 狂暴阶段连续技能
            if (Main.rand.Next(5) == 0)
            {
                MultiAttack();
                State.Markers["SkillCooldown"] = 60; // 1秒冷却
            }
            break;
    }
}

// 动态速度调整（根据玩家数量）
int activePlayers = players.Count;
float speedMultiplier = 1f + (activePlayers - 1) * 0.2f; // 每多一个玩家增加20%速度
Npc.velocity *= speedMultiplier;

Msg.AppendLine($"玩家数量: {activePlayers}, 速度倍率: {speedMultiplier:F1}");
Msg.AppendLine($"当前阶段: {phase}, 血量: {lifePercent:F1}%");
Msg.AppendLine($"技能冷却: {State.Markers["SkillCooldown"]}帧");

return $"BOSS AI执行完成，阶段{phase}";

// 技能函数
void ShootFanProjectiles()
{
    Msg.AppendLine("释放技能: 扇形弹幕");
    var target = Main.player[Npc.target];
    if (target == null) return;

    var dir = target.Center - Npc.Center;
    if (dir != Vector2.Zero) dir.Normalize();

    for (int i = 0; i < 5; i++)
    {
        var vel = dir * 10f;
        vel = vel.RotatedBy(MathHelper.ToRadians(-20 + i * 10));

        Projectile.NewProjectile(
            Npc.GetSpawnSourceForNPCFromNPCAI(),
            Npc.Center,
            vel,
            113, 30, 5f, Main.myPlayer
        );
    }
}

void ShootHomingProjectiles()
{
    Msg.AppendLine("释放技能: 追踪弹幕");
    for (int i = 0; i < 3; i++)
    {
        var vel = new Vector2(
            Main.rand.NextFloat() * 10f - 5f,
            Main.rand.NextFloat() * 10f - 5f
        );

        int proj = Projectile.NewProjectile(
            Npc.GetSpawnSourceForNPCFromNPCAI(),
            Npc.Center,
            vel,
            440, 40, 6f, Main.myPlayer, 0f, 1f
        );

        if (proj >= 0)
        {
            Main.projectile[proj].timeLeft = 300;
        }
    }
}

void SpawnMinions()
{
    Msg.AppendLine("释放技能: 召唤小怪");
    int[] minionTypes = { 50, 266, 325 }; // 小怪ID

    for (int i = 0; i < 3; i++)
    {
        var spawnPos = Npc.Center + new Vector2(
            Main.rand.NextFloat() * 200f - 100f,
            Main.rand.NextFloat() * 200f - 100f
        );

        int npcType = minionTypes[Main.rand.Next(minionTypes.Length)];
        int npcIndex = NPC.NewNPC(
            Npc.GetSpawnSourceForNPCFromNPCAI(),
            (int)spawnPos.X,
            (int)spawnPos.Y,
            npcType
        );

        if (npcIndex >= 0)
        {
            var minion = Main.npc[npcIndex];
            minion.netUpdate = true;

            // 给召唤物添加标志
            var minionState = StateApi.GetState(minion);
            if (minionState != null)
            {
                minionState.Flag = Npc.FullName + "_Minion";
                minionState.Markers["SpawnTime"] = State.ActiveTime;
            }
        }
    }
}

void ScreenExplosion()
{
    Msg.AppendLine("释放技能: 全屏爆炸");

    // 警告特效
    CombatText.NewText(Npc.getRect(), Color.Red, "Screen Explosion!", true);

    // 在所有玩家位置生成爆炸
    foreach (var plr in players)
    {
        var player = plr.TPlayer;
        if (player != null && player.active)
        {
            Projectile.NewProjectile(
                Npc.GetSpawnSourceForNPCFromNPCAI(),
                player.Center,
                Vector2.Zero,
                134, 50, 10f, Main.myPlayer
            );
        }
    }
}

void MultiAttack()
{
    Msg.AppendLine("释放技能: 多重攻击");

    // 同时使用多个技能
    ShootFanProjectiles();
    ShootHomingProjectiles();

    // 自身强化
    Npc.velocity *= 1.5f;
    Npc.defense += 10;

    // 特效
    for (int i = 0; i < 20; i++)
    {
        Dust.NewDust(Npc.position, Npc.width, Npc.height, 57, 0f, 0f, 150, Color.Orange, 2f);
    }
}