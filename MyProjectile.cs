using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using static Plugin.Configuration;

namespace MonsterSpeed;

internal class MyProjectile
{
    #region 生成弹幕方法：给弹幕生命赋值
    public static int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner = -1, float ai0 = 0f, float ai1 = 0f, float ai2 = 0f, int Left = -1)
    {
        var index = Projectile.NewProjectile(spawnSource, X, Y, SpeedX, SpeedY, Type, Damage, KnockBack, Owner, ai0, ai1, ai2);
        if (Left == 0)
        {
            Main.projectile[index].Kill();
        }
        else if (Left > 0)
        {
            Main.projectile[index].timeLeft = Left;
        }
        return index;
    }
    #endregion

    #region 弹幕生成条件
    public static void SpawnProjectile(List<ProjData> ProjData, NPC npc)
    {
        var count = Main.projectile.Count(p => p.active && p.owner == Main.myPlayer);

        foreach (var proj in ProjData)
        {
            //限制弹幕数量
            if (count >= proj.Count || proj.ID <= 0) continue;

            // 获取距离和方向向量
            var tar = npc.GetTargetData(true);
            var dict = tar.Center - npc.Center;

            if (tar.Invalid) continue; // 目标无效则跳过

            // 计算发射速度
            var speed = proj.Velocity;
            var velocity = dict.SafeNormalize(Vector2.Zero) * speed;

            // 创建并发射弹幕
            if (proj.Left != 0)
            {
                Projectile.NewProjectile(Terraria.Projectile.GetNoneSource(),
                    npc.Center.X, npc.Center.Y, velocity.X, velocity.Y, proj.ID, proj.Damage, proj.KnockBack,
                    Main.myPlayer, 0f, proj.Left);

                count++; // 更新计数器
            }
        }
    }
    #endregion

}
