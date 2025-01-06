﻿using Terraria;
using Microsoft.Xna.Framework;
using static MonsterSpeed.Configuration;

namespace MonsterSpeed;

internal class MyProjectile
{
    #region 弹幕生成方法
    private static int index = 0;
    private static Dictionary<int, int> stack = new Dictionary<int, int>();
    private static Dictionary<int, float> cooldowns = new Dictionary<int, float>();
    public static void SpawnProjectile(List<ProjData> data, NPC npc)
    {
        var proj = data[index];

        // 初始化或更新当前弹幕组的发射进度
        if (!stack.ContainsKey(index))
        {
            stack[index] = 0;
        }

        // 获取距离和方向向量
        var tar = npc.GetTargetData(true);
        var dict = tar.Center - npc.Center;
        if (stack[index] >= proj.stack || proj.ID <= 0 || tar.Invalid)
        {
            Next(data);
            return; // 目标无效则跳过
        }

        // 检查冷却时间
        if (!cooldowns.ContainsKey(index) || cooldowns[index] <= 0f)
        {
            // 弧度：定义总角度范围的一半（从中心线两侧各偏移） 
            var radian = proj.Angle * (float)Math.PI / 180;
            // 计算每次发射的弧度增量
            var addRadian = radian * 2 / (proj.stack - 1);

            // 初始化默认AI值
            var ai0 = proj.ai != null && proj.ai.ContainsKey(0) ? proj.ai[0] : 0f;
            var ai1 = proj.ai != null && proj.ai.ContainsKey(1) ? proj.ai[1] : 0f;
            var ai2 = proj.ai != null && proj.ai.ContainsKey(2) ? proj.ai[2] : 0f;

            //以“玩家为中心”为true 以玩家为中心,否则以被击中的npc为中心
            var pos = proj.TarCenter
                    ? new Vector2(tar.Center.X, tar.Center.Y)
                    : new Vector2(npc.Center.X, npc.Center.Y);


            // 计算衰减值，随着弹幕数量的增加而减慢
            var decay = 1.0f - stack[index] / (float)proj.stack * proj.decay;

            // 应用发射速度
            var speed = proj.Velocity * decay;
            var vel = dict.SafeNormalize(Vector2.Zero) * speed;

            // 应用角度偏移
            var Angle = (stack[index] - (proj.stack - 1) / 2.0f) * addRadian;
            vel = vel.RotatedBy(Angle);

            // 如果旋转角度不为0，则设置旋转角度
            if (proj.Rotate != 0)
            {
                vel = vel.RotatedBy(Angle + proj.Rotate * stack[index]);
            }

            // 如果中心扩展不为0，则应用中心外扩或内缩
            var NewPos = pos;
            if (proj.CEC != 0)
            {
                // 计算相对于中心点的偏移量
                var ExAngle = stack[index] / (float)(proj.stack - 1) * MathHelper.TwoPi; // 均匀分布的角度
                var absEx = Math.Abs(proj.CEC); // 使用绝对值以确保正确的扩展距离
                var offset = new Vector2((float)Math.Cos(ExAngle), (float)Math.Sin(ExAngle)) * absEx;

                // 如果 CEC 是负数，则反向偏移量
                if (proj.CEC < 0)
                {
                    offset *= -1;
                }
                NewPos += offset;
            }

            if (proj.Lift >= 0)
            {
                // 创建并发射弹幕
                var newProj = Projectile.NewProjectile(Projectile.GetNoneSource(),
                                                       NewPos.X, NewPos.Y, vel.X, vel.Y,
                                                       proj.ID, proj.Damage, proj.KnockBack,
                                                       Main.myPlayer, ai0, ai1, ai2);
                // 弹幕生命
                Main.projectile[newProj].timeLeft = proj.Lift > 0 ? proj.Lift : 0;
                if (proj.Lift == 0)
                {
                    Main.projectile[newProj].Kill();
                }
            }

            stack[index]++; // 更新计数器
            cooldowns[index] = proj.interval;  // 设置冷却时间
        }
        else
        {
            cooldowns[index] -= 1f;
        }
    }
    #endregion

    #region 移动到下一个要发射的弹幕方法
    private static void Next(List<ProjData> data)
    {
        //只有当前组的所有弹幕都发射完毕时才更新索引
        if (stack.ContainsKey(index) && stack[index] >= data[index].stack)
        {
            index++;
            if (index >= data.Count)
            {
                index = 0;
            }
            stack[index] = 0;
            cooldowns[index] = 0f; // 重置冷却时间
        }
    }
    #endregion

}
