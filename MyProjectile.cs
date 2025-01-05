using Terraria;
using Microsoft.Xna.Framework;
using static MonsterSpeed.Configuration;

namespace MonsterSpeed;

internal class MyProjectile
{
    #region 弹幕生成方法
    public static void SpawnProjectile(List<ProjData> data, NPC npc)
    {
        var count = Main.projectile.Count(p => p.active && p.owner == Main.myPlayer);

        foreach (var proj in data)
        {
            // 获取距离和方向向量
            var tar = npc.GetTargetData(true);
            var dict = tar.Center - npc.Center;
            if (count >= proj.Count || proj.ID <= 0 || tar.Invalid) continue; // 目标无效则跳过

            // 弧度：定义总角度范围的一半（从中心线两侧各偏移） 
            var radian = proj.Angle * (float)Math.PI / 180;
            // 计算每次发射的弧度增量
            var AddRadian = radian * 2 / (proj.Count - 1);

            // 初始化默认AI值
            var ai0 = proj.ai != null && proj.ai.ContainsKey(0) ? proj.ai[0] : 0f;
            var ai1 = proj.ai != null && proj.ai.ContainsKey(1) ? proj.ai[1] : 0f;
            var ai2 = proj.ai != null && proj.ai.ContainsKey(2) ? proj.ai[2] : 0f;

            // 根据弹幕数量属性发射多个弹幕，每次发射都进行速度衰减
            for (var i = 0; i < proj.Count; i++)
            {
                // 计算衰减值，随着弹幕数量的增加而减慢
                var decay = 1.0f - i / (float)proj.Count * (1.0f - 0.1f);

                // 应用发射速度
                var speed = proj.Velocity * decay;
                var vel = dict.SafeNormalize(Vector2.Zero) * speed;

                // 应用角度偏移
                var Angle = (i - (proj.Count - 1) / 2.0f) * AddRadian;
                vel = vel.RotatedBy(Angle);

                if (proj.ID != 0 && proj.Lift >= 0)
                {
                    // 创建并发射弹幕
                    var newProj = Projectile.NewProjectile(Projectile.GetNoneSource(),
                                                           npc.Center.X, npc.Center.Y, vel.X, vel.Y,
                                                           proj.ID, proj.Damage, proj.KnockBack, Main.myPlayer,
                                                           ai0, ai1, ai2);
                    // 弹幕生命
                    Main.projectile[newProj].timeLeft = proj.Lift > 0 ? proj.Lift : 0;
                    if (proj.Lift == 0)
                    {
                        Main.projectile[newProj].Kill();
                    }

                    count++; // 更新计数器
                }
            }
        }
    }
    #endregion

}
