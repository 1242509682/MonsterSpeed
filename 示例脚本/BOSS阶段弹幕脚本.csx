using System;
using Terraria;
using Microsoft.Xna.Framework;
using TShockAPI;

// BOSS阶段弹幕脚本
if (Npc == null) return "NPC为空";
if (State == null) return "State为空";

Msg?.AppendLine("BOSS阶段弹幕");

// 获取当前血量百分比
float hpRatio = Npc.life / (float)Npc.lifeMax;
int stage = GetMkr("阶段", 0);

// 根据血量确定阶段
if (hpRatio > 0.75f && stage != 1)
{
    SetMkr("阶段", 1);
    stage = 1;
    Msg?.AppendLine("进入阶段1: 75%-100%血量");
}
else if (hpRatio > 0.5f && hpRatio <= 0.75f && stage != 2)
{
    SetMkr("阶段", 2);
    stage = 2;
    Msg?.AppendLine("进入阶段2: 50%-75%血量");
    // 播放特效
    TSPlayer.All.SendMessage($"[c/FF5555:{Npc.FullName}] 进入第二阶段！", 255, 100, 100);
}
else if (hpRatio > 0.25f && hpRatio <= 0.5f && stage != 3)
{
    SetMkr("阶段", 3);
    stage = 3;
    Msg?.AppendLine("进入阶段3: 25%-50%血量");
    TSPlayer.All.SendMessage($"[c/FF0000:{Npc.FullName}] 狂暴化！", 255, 0, 0);
}
else if (hpRatio <= 0.25f && stage != 4)
{
    SetMkr("阶段", 4);
    stage = 4;
    Msg?.AppendLine("进入阶段4: 0%-25%血量");
    TSPlayer.All.SendMessage($"[c/FF00FF:{Npc.FullName}] 最终阶段！", 255, 0, 255);
}

// 根据不同阶段发射不同弹幕
var plr = GetTar();
if (plr == null || !plr.active || plr.dead) return "目标无效";

var npcPos = Npc.Center;
var tarPos = plr.Center;
var dir = tarPos - npcPos;

if (dir == Vector2.Zero) dir = new Vector2(1, 0);
dir.Normalize();

switch (stage)
{
    case 1: // 阶段1: 简单弹幕
        // 直线弹幕
        var vel1 = dir * 8f;
        for (int i = 0; i < 3; i++)
        {
            var v = vel1.RotatedBy(MathHelper.ToRadians(-10 + i * 10));
            SpawnProj(113, 25, v, 120);
        }
        break;
        
    case 2: // 阶段2: 环形弹幕
        int cnt2 = 8;
        float spd2 = 6f;
        for (int i = 0; i < cnt2; i++)
        {
            float ang = MathHelper.TwoPi / cnt2 * i;
            var vel2 = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * spd2;
            SpawnProj(115, 20, vel2, 180);
        }
        break;
        
    case 3: // 阶段3: 复合弹幕
        // 追踪弹幕
        var homingVel = dir * 5f;
        for (int i = 0; i < 4; i++)
        {
            var v = homingVel.RotatedBy(MathHelper.ToRadians(-15 + i * 10));
            int proj = Projectile.NewProjectile(
                Npc.GetSpawnSourceForNPCFromNPCAI(),
                npcPos, v, 440, 30, 5f, Main.myPlayer, 0f, 1f);
            if (proj >= 0)
            {
                Main.projectile[proj].timeLeft = 300;
                Main.projectile[proj].friendly = false;
            }
        }
        
        // 圆形弹幕
        int cnt3 = 12;
        float spd3 = 7f;
        for (int i = 0; i < cnt3; i++)
        {
            float ang = MathHelper.TwoPi / cnt3 * i;
            var vel3 = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * spd3;
            SpawnProj(671, 22, vel3, 200);
        }
        break;
        
    case 4: // 阶段4: 狂暴弹幕
        // 大量散射弹幕
        int cnt4 = 24;
        for (int i = 0; i < cnt4; i++)
        {
            float ang = MathHelper.TwoPi / cnt4 * i;
            var vel4 = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 9f;
            SpawnProj(131, 18, vel4, 150);
        }
        
        // 爆炸弹幕
        int expl = Projectile.NewProjectile(
            Npc.GetSpawnSourceForNPCFromNPCAI(),
            tarPos, Vector2.Zero, 134, 60, 12f, Main.myPlayer);
        if (expl >= 0)
        {
            Main.projectile[expl].timeLeft = 30;
            Main.projectile[expl].friendly = false;
        }
        
        // 回血
        HealNpc(50);
        break;
}

return $"阶段{stage}弹幕完成，血量:{hpRatio:P0}";