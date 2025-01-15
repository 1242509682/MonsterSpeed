using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;

namespace MonsterSpeed;

//弹幕数据结构
public class ProjData
{
    [JsonProperty("弹幕ID", Order = 0)]
    public int ProjID = 0;
    [JsonProperty("伤害", Order = 1)]
    public int Damage = 30;
    [JsonProperty("数量", Order = 2)]
    public int stack = 5;
    [JsonProperty("间隔", Order = 3)]
    public float interval = 15f;
    [JsonProperty("击退", Order = 4)]
    public int KnockBack = 5;
    [JsonProperty("速度", Order = 5)]
    public float Velocity = 10f;
    [JsonProperty("半径", Order = 7)]
    public float Radius = 0f;
    [JsonProperty("偏移", Order = 8)]
    public float Angle = 15f;
    [JsonProperty("旋转", Order = 9)]
    public float Rotate = 5f;
    [JsonProperty("弹幕AI", Order = 10)]
    public Dictionary<int, float> ai { get; set; } = new Dictionary<int, float>();
    [JsonProperty("生命周期", Order = 11)]
    public int Lift = 120;
    [JsonProperty("以玩家为中心", Order = 13)]
    public bool TarCenter = false;
}

internal class MyProjectile
{
    #region 弹幕生成方法
    private static int index = 0;
    private static bool reverse;
    public static int SPCount = new int(); //用于追踪所有弹幕生成次数
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

        // 数量超标 目标无效 或 不在进度则跳过
        if (stack[index] >= proj.stack || proj.ProjID <= 0 || tar.Invalid)
        {
            Next(data);
            return;
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
            var decay = 1.0f - stack[index] / (float)proj.stack * 0.9f;

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

            //中心半径
            var NewPos = pos;
            if (proj.Radius != 0)
            {
                // 计算相对于中心点的偏移量，直接使用 偏移半径 作为偏移距离
                var ExAngle = stack[index] / (float)(proj.stack - 1) * MathHelper.TwoPi; // 均匀分布的角度
                var offset = new Vector2((float)Math.Cos(ExAngle), (float)Math.Sin(ExAngle)) * (proj.Radius * 16);
                // 如果 偏移半径 是负数，则反向偏移量
                if (proj.Radius < 0)
                {
                    offset *= -1;
                }
                NewPos += offset;
            }

            //弹幕生命>=0时才发射
            if (proj.Lift >= 0)
            {
                // 创建并发射弹幕
                var newProj = Projectile.NewProjectile(Projectile.GetNoneSource(),
                                                       NewPos.X, NewPos.Y, vel.X, vel.Y,
                                                       proj.ProjID, proj.Damage, proj.KnockBack,
                                                       Main.myPlayer, ai0, ai1, ai2);

                if (newProj == -1)
                {
                    newProj = Projectile.FindOldestProjectile();
                }

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
            SPCount++; //增加弹幕生成次数
        }
    }
    #endregion

}
