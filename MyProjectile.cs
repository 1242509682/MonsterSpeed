using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;

namespace MonsterSpeed;

//额外弹幕数据
public class ProjData2
{
    [JsonProperty("弹幕ID", Order = 0)]
    public int Type = 0;
    [JsonProperty("间隔", Order = 1)]
    public float Interval = 60;
    [JsonProperty("速度", Order = 2)]
    public float Velocity = 0f;
    [JsonProperty("半径", Order = 3)]
    public float Radius = 0f;
    [JsonProperty("偏移", Order = 4)]
    public float Angle = 0f;
    [JsonProperty("旋转", Order = 5)]
    public float Rotate = 0;
    [JsonProperty("弹幕AI", Order = 6)]
    public Dictionary<int, float> ai { get; set; } = new Dictionary<int, float>();
}

//弹幕数据结构
public class ProjData
{
    [JsonProperty("弹幕ID", Order = 0)]
    public int Type = 0;
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

    [JsonProperty("额外弹幕", Order = 14)]
    public List<ProjData2> UpdateProj { get; set; } = new List<ProjData2>();
}

internal class MyProjectile
{
    #region 弹幕生成方法
    private static int Index = 0; //存储弹幕组索引
    public static int SPCount = new int(); //用于追踪所有弹幕生成次数
    private static Dictionary<int, int> SendStack = new Dictionary<int, int>(); //追踪每发弹幕的数量
    private static Dictionary<int, float> EachCooldowns = new Dictionary<int, float>(); //追踪每发弹幕之间的发射间隔
    public static void SpawnProjectile(List<ProjData> data, NPC npc)
    {
        if (data == null || data.Count == 0 || Index > data.Count) return;

        var proj = data[Index];

        // 初始化当前弹幕组的发射数量
        if (!SendStack.ContainsKey(Index)) SendStack[Index] = 0;

        // 初始化每个弹幕的发射间隔
        if (!EachCooldowns.ContainsKey(Index)) EachCooldowns[Index] = 0;

        // 获取距离和方向向量
        var tar = npc.GetTargetData(true);
        var dict = tar.Center - npc.Center;

        // 数量超标 目标无效 或 不在进度则跳过
        if (SendStack[Index] >= proj.stack || proj.Type <= 0 || tar.Invalid)
        {
            Next(data);
            return;
        }

        // 检查冷却时间
        if (EachCooldowns[Index] <= 0f)
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
            var decay = 1.0f - SendStack[Index] / (float)proj.stack * 0.9f;

            // 应用发射速度
            var speed = proj.Velocity * decay;
            var vel = dict.SafeNormalize(Vector2.Zero) * speed;

            // 应用角度偏移
            var Angle = (SendStack[Index] - (proj.stack - 1) / 2.0f) * addRadian;
            vel = vel.RotatedBy(Angle);

            // 如果旋转角度不为0，则设置旋转角度
            if (proj.Rotate != 0)
            {
                vel = vel.RotatedBy(Angle + proj.Rotate * SendStack[Index]);
            }

            //中心半径
            var NewPos = pos;
            if (proj.Radius != 0)
            {
                // 计算相对于中心点的偏移量，直接使用 偏移半径 作为偏移距离
                var ExAngle = SendStack[Index] / (float)(proj.stack - 1) * MathHelper.TwoPi; // 均匀分布的角度
                var offset = new Vector2((float)Math.Cos(ExAngle), (float)Math.Sin(ExAngle)) * (proj.Radius * 16);
                // 如果 偏移半径 是负数，则反向偏移量
                if (proj.Radius < 0)
                {
                    offset *= -1;
                }
                NewPos += offset;
            }

            //弹幕生命>0时才发射
            if (proj.Lift > 0)
            {
                // 创建并发射弹幕
                var newProj = Projectile.NewProjectile(Projectile.GetNoneSource(),
                                                       NewPos.X, NewPos.Y, vel.X, vel.Y,
                                                       proj.Type, proj.Damage, proj.KnockBack,
                                                       Main.myPlayer, ai0, ai1, ai2);
                // 弹幕生命
                Main.projectile[newProj].timeLeft = proj.Lift > 0 ? proj.Lift : 0;

                // 更新弹幕
                if (proj.UpdateProj != null && proj.UpdateProj.Count > 0)
                {
                    UpdateProjectile(proj.UpdateProj, npc, tar, newProj, proj.stack);
                }
            }

            SendStack[Index]++; //更新发射数计数器
            EachCooldowns[Index] = proj.interval;  //设置间隔
        }
        else
        {
            EachCooldowns[Index] -= 1f;
        }
    }
    #endregion

    #region 移动到下一个要发射的弹幕方法
    private static void Next(List<ProjData> data)
    {
        //只有当前组的所有弹幕都发射完毕时才更新索引
        if (SendStack.ContainsKey(Index) && SendStack[Index] >= data[Index].stack)
        {
            Index++;
            if (Index >= data.Count)
            {
                Index = 0;
            }
            SendStack[Index] = 0;
            EachCooldowns[Index] = 0f; // 重置冷却时间
            SPCount++; //增加弹幕生成次数
        }
    }
    #endregion

    #region 额外弹幕方法
    private static Dictionary<int, float> Cooldowns = new Dictionary<int, float>();
    private static void UpdateProjectile(List<ProjData2> projs, NPC npc, Terraria.DataStructures.NPCAimedTarget tar, int index, int stack)
    {
        if (projs == null || projs.Count <= 0) return;

        // 检查是否已经有冷却时间设置，如果没有则初始化为0（即没有冷却）
        if (!Cooldowns.ContainsKey(index))
        {
            Cooldowns[index] = 0f;
        }

        if (Cooldowns[index] <= 0f)
        {
            var proj = Main.projectile[index];
            foreach (var newProj in projs)
            {
                // if (Main.GameUpdateCount % newProj.Interval != 0) continue;
                if (newProj == null) continue;

                var ai0 = newProj.ai != null && newProj.ai.ContainsKey(0) ? newProj.ai[0] : 0f;
                var ai1 = newProj.ai != null && newProj.ai.ContainsKey(1) ? newProj.ai[1] : 0f;
                var ai2 = newProj.ai != null && newProj.ai.ContainsKey(2) ? newProj.ai[2] : 0f;

                var decay = 1.0f - SendStack[Index] / (float)stack * 0.9f;
                var velocity = newProj.Velocity * decay;

                var speed2 = velocity != 0 ? velocity : proj.velocity.Length();
                var vel = (tar.Center - npc.Center).SafeNormalize(Vector2.Zero) * speed2;

                var angle = newProj.Angle * Math.PI / 180;
                var radian = angle != 0 ? angle : 0;
                var addRadian = radian * 2.0f / (stack - 1);
                var Angle = (SendStack[Index] - (stack - 1) / 2.0f) * addRadian;

                vel = vel.RotatedBy(Angle + (newProj.Rotate != 0 ? newProj.Rotate * SendStack[Index] : 0));

                var newPos = proj.position;
                if (newProj.Radius != 0)
                {
                    var ExAngle = SendStack[Index] / (float)(stack - 1) * MathHelper.TwoPi;
                    var offset = new Vector2((float)Math.Cos(ExAngle), (float)Math.Sin(ExAngle)) * (Math.Abs(newProj.Radius) * 16) * Math.Sign(newProj.Radius);

                    if (newProj.Radius < 0)
                    {
                        offset *= -1;
                    }
                    newPos += offset;
                }

                if (proj.type != newProj.Type)
                    proj.type = newProj.Type;
                if (proj.position != newPos)
                    proj.position = newPos;
                if (proj.velocity != vel)
                    proj.velocity = vel;
                if (proj.ai[0] != ai0)
                    proj.ai[0] = ai0;
                if (proj.ai[1] != ai1)
                    proj.ai[1] = ai1;
                if (proj.ai[2] != ai2)
                    proj.ai[2] = ai2;
                
                TSPlayer.All.SendData(PacketTypes.ProjectileNew, null, index);
                Cooldowns[index] = newProj.Interval;
            }
        }
        else
        {
            // 减少冷却时间
            Cooldowns[index] -= 1f;
        }
    }
    #endregion

}
