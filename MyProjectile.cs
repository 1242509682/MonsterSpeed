﻿using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;

namespace MonsterSpeed;

//弹幕数据结构
public class ProjData
{
    [JsonProperty("弹幕ID", Order = 0)]
    public int Type = 115;
    [JsonProperty("伤害", Order = 1)]
    public int Damage = 30;
    [JsonProperty("数量", Order = 2)]
    public int stack = 5;
    [JsonProperty("间隔", Order = 3)]
    public float interval = 30f;
    [JsonProperty("击退", Order = 4)]
    public int KnockBack = 5;
    [JsonProperty("速度", Order = 5)]
    public float Velocity = 10f;
    [JsonProperty("速度衰减", Order = 6)]
    public float decay = 0.9f;
    [JsonProperty("随数量衰减", Order = 7)]
    public bool decayForStack = true;
    [JsonProperty("半径", Order = 8)]
    public float Radius = 0f;
    [JsonProperty("偏移", Order = 9)]
    public float Angle = 0;
    [JsonProperty("旋转", Order = 10)]
    public float Rotate = 0;
    [JsonProperty("弹幕AI", Order = 11)]
    public Dictionary<int, float> ai { get; set; } = new Dictionary<int, float>();
    [JsonProperty("持续时间", Order = 12)]
    public int Lift = 120;
    [JsonProperty("以玩家为中心", Order = 13)]
    public bool TarCenter = false;
    [JsonProperty("更新间隔", Order = 14)]
    public double UpdateTime = 500f;
    [JsonProperty("更新弹幕", Order = 15)]
    public List<UpdateProjData> UpdateProj { get; set; } = new List<UpdateProjData>();
}

//更新弹幕数据
public class UpdateProjData
{
    [JsonProperty("死亡回弹", Order = -1)]
    public bool Backer = false;
    [JsonProperty("间隔", Order = 0)]
    public float interval = 30f;
    [JsonProperty("速度", Order = 1)]
    public float Velocity = 0f;
    [JsonProperty("速度衰减", Order = 2)]
    public float decay = 0.9f;
    [JsonProperty("随数量衰减", Order = 3)]
    public bool decayForStack = true;
    [JsonProperty("半径", Order = 4)]
    public float Radius = 0f;
    [JsonProperty("偏移", Order = 5)]
    public float Angle = 0f;
    [JsonProperty("旋转", Order = 6)]
    public float Rotate = 0;
    [JsonProperty("弹幕AI", Order = 7)]
    public Dictionary<int, float> ai { get; set; } = new Dictionary<int, float>();
}

//已发射出去的弹幕数据
public class SpawnProj
{
    //弹幕索引
    public int Index { get; set; }
    //怪物索引
    public int whoAmI { get; set; }
    //弹幕ID
    public int Type { get; set; }

    public SpawnProj(int index, int useIndex, int type)
    {
        Index = index;
        whoAmI = useIndex;
        Type = type;
    }
}

internal class MyProjectile
{
    #region 弹幕生成方法
    private static int Index = 0; //存储弹幕组索引
    public static int SPCount = new int(); //用于追踪所有弹幕生成次数
    private static Dictionary<int, int> SendStack = new Dictionary<int, int>(); //追踪每发弹幕的数量
    private static Dictionary<int, float> EachCooldowns = new Dictionary<int, float>(); //追踪每发弹幕之间的发射间隔
    public static Dictionary<int, DateTime> UpdateTimer = new(); //用于追踪更新弹幕的时间
    public static void SpawnProjectile(List<ProjData> SpawnProj, NPC npc)
    {
        if (SpawnProj == null || SpawnProj.Count == 0 || Index >= SpawnProj.Count) return;

        ProjData proj = SpawnProj[Index];

        // 初始化当前弹幕组的发射数量
        if (!SendStack.ContainsKey(Index)) SendStack[Index] = 0;

        // 初始化每个弹幕的发射间隔
        if (!EachCooldowns.ContainsKey(Index)) EachCooldowns[Index] = 0;

        // 获取距离和方向向量 新增目标筛选逻辑
        Player plr = Main.player[npc.target];
        Vector2 dict = plr.Center - npc.Center;

        // 数量超标 目标无效 或 不在进度则跳过
        if (SendStack[Index] >= proj.stack || proj.Type <= 0 || plr == null)
        {
            Next(SpawnProj);
            return;
        }

        // 检查冷却时间
        if (EachCooldowns[Index] > 0f)
        {
            EachCooldowns[Index] -= 1f;
        }
        else
        {
            // 弧度：定义总角度范围的一半（从中心线两侧各偏移） 
            double radian = proj.Angle * (float)Math.PI / 180;

            // 计算每次发射的弧度增量
            double addRadian = radian * 2 / (proj.stack - 1);

            // 初始化默认AI值
            float ai0 = proj.ai != null && proj.ai.ContainsKey(0) ? proj.ai[0] : 0f;
            float ai1 = proj.ai != null && proj.ai.ContainsKey(1) ? proj.ai[1] : 0f;
            float ai2 = proj.ai != null && proj.ai.ContainsKey(2) ? proj.ai[2] : 0f;

            // 计算衰减值
            float decay = 0;
            //随着弹幕数量的增加而减慢
            if (proj.decayForStack)
            {
                decay = 1.0f - SendStack[Index] / (float)proj.stack * proj.decay;
            }
            else if (proj.decay != 0)
            {
                //自定义衰减值
                decay = proj.decay;
            }

            // 应用发射速度
            var speed = proj.Velocity * decay;
            Vector2 vel = dict.SafeNormalize(Vector2.Zero) * speed;

            // 应用角度偏移
            double Angle = (SendStack[Index] - (proj.stack - 1) / 2.0f) * addRadian;

            //vel = vel.RotatedBy(Angle);
            // 如果旋转角度不为0，则设置旋转角度
            if (proj.Rotate != 0)
            {
                vel = vel.RotatedBy(Angle + proj.Rotate * SendStack[Index]);
            }

            //以“玩家为中心”为true 以玩家为中心,否则以被击中的npc为中心
            Vector2 pos = proj.TarCenter
                    ? new Vector2(plr.Center.X, plr.Center.Y)
                    : new Vector2(npc.Center.X, npc.Center.Y);

            //中心半径
            Vector2 NewPos = pos;
            if (proj.Radius != 0)
            {
                // 计算相对于中心点的偏移量，直接使用 偏移半径 作为偏移距离
                double ExAngle = SendStack[Index] / (float)(proj.stack - 1) * MathHelper.TwoPi; // 均匀分布的角度
                Vector2 offset = new Vector2((float)Math.Cos(ExAngle), (float)Math.Sin(ExAngle)) * (proj.Radius * 16);
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

                //当弹幕生命不为0时触发更新弹幕
                if (Main.projectile[newProj].timeLeft != 0)
                {
                    // 更新弹幕
                    if (proj.UpdateProj != null && proj.UpdateProj.Count > 0)
                    {
                        SpawnPorj[newProj] = new SpawnProj(newProj, npc.whoAmI, proj.Type);

                        // 如果没有记录过这个弹幕的更新计时器，则初始化
                        if (!UpdateTimer.ContainsKey(newProj))
                        {
                            UpdateTimer[newProj] = DateTime.UtcNow;
                        }

                        if ((DateTime.UtcNow - UpdateTimer[newProj]).TotalMilliseconds >= proj.UpdateTime)
                        {
                            UpdateProjectile(proj, proj.UpdateProj, npc, plr, proj.Type, proj.stack);
                            UpdateTimer[newProj] = DateTime.UtcNow; // 更新最后一次更新的时间
                        }
                    }
                }
                else
                {
                    // 如果弹幕生命为0，则直接销毁弹幕
                    Main.projectile[newProj].Kill();
                }
            }

            SendStack[Index]++; //更新发射数计数器
            EachCooldowns[Index] = proj.interval;  //设置间隔
        }
    }
    #endregion

    #region 更新弹幕方法
    public static SpawnProj[] SpawnPorj { get; set; } = new SpawnProj[Main.maxProjectiles];  //用于存储已经发射出去的弹幕
    private static void UpdateProjectile(ProjData proj, List<UpdateProjData> Update, NPC npc, Player plr, int type, int stack)
    {
        if (Update == null || Update.Count <= 0) return;

        //存储需要更新的弹幕属性
        List<int> UpList = new List<int>();

        foreach (var up in Update)
        {
            if (up == null) continue;

            for (var i = 0; i < SpawnPorj.Length; i++)
            {
                if (SpawnPorj[i] == null) continue;
                if (type <= 0 || SpawnPorj[i].Index < 0 || SpawnPorj[i].Type != type || SpawnPorj[i].whoAmI != npc.whoAmI) continue;

                int index = SpawnPorj[i].Index;

                Projectile NewProj = Main.projectile[index];

                if (NewProj == null || !NewProj.active || NewProj.type != type || NewProj.owner != Main.myPlayer)
                {
                    continue;
                }

                // 计算衰减值
                float decay = 0;
                //随着弹幕数量的增加而减慢
                if (up.decayForStack)
                {
                    decay = 1.0f - SendStack[Index] / (float)stack * up.decay;
                }
                else if (up.decay != 0)
                {
                    //自定义衰减值
                    decay = up.decay;
                }

                // 计算速度
                double velocity = up.Velocity * decay;

                float speed2;
                if (velocity != 0)
                {
                    speed2 = (float)velocity;
                    add(UpList, index);
                }
                else
                {
                    speed2 = (float)NewProj.velocity.Length();
                }

                // 计算速度向量
                Vector2 vel = NewProj.velocity.SafeNormalize(Vector2.Zero) * speed2;

                // 计算角度偏移
                double angle = up.Angle * Math.PI / 180;
                double radian;
                if (angle != 0)
                {
                    radian = (double)angle;
                    add(UpList, index);
                }
                else
                {
                    radian = 0;
                }

                // 计算每次发射的弧度增量
                double addRadian = radian * 2.0f / (stack - 1);
                double Angle = (SendStack[Index] - (stack - 1) / 2.0f) * addRadian;
                if (up.Rotate != 0)
                {
                    vel = vel.RotatedBy(Angle + ((float)(up.Rotate * SendStack[Index])));
                    add(UpList, index);
                }
                else
                {
                    vel = vel.RotatedBy(Angle + (0));
                }

                Vector2 pos = NewProj.position;
                Vector2 newPos = pos;

                // 如果半径不为0，则计算偏移位置
                if (up.Radius != 0)
                {
                    double ExAngle = SendStack[Index] / (float)(stack - 1) * MathHelper.TwoPi;
                    Vector2 offset = new Vector2((float)Math.Cos(ExAngle), (float)Math.Sin(ExAngle)) * (Math.Abs(up.Radius) * 16) * Math.Sign(up.Radius);

                    if (up.Radius < 0)
                    {
                        offset *= -1;
                    }
                    newPos += offset;
                    add(UpList, index);
                }

                // 检查并更新弹幕位置、速度和AI
                if (NewProj.position != newPos)
                {
                    NewProj.position = newPos;
                    add(UpList, index);
                }

                // 更新弹幕速度
                if (NewProj.velocity != vel)
                {
                    NewProj.velocity = vel;
                    add(UpList, index);
                }

                // 更新弹幕AI
                if (up.ai.Count > 0)
                {
                    for (var j = 0; j < NewProj.ai.Count(); j++)
                    {
                        if (up.ai.ContainsKey(j) && up.ai.TryGetValue(j, out var value))
                        {
                            Main.projectile[index].ai[j] = value;
                            add(UpList, index);
                        }
                    }
                }

                // 添加弹幕死亡检测
                if (NewProj.timeLeft <= 1)
                {
                    // 如果开启回弹模式
                    if (up.Backer)
                    {
                        // 计算反向速度
                        Vector2 ReVel = (npc.Center - NewProj.Center).SafeNormalize(Vector2.Zero) * up.Velocity;

                        // 生成回弹弹幕
                        int newProj2 = Projectile.NewProjectile(Projectile.GetNoneSource(),
                            NewProj.Center, ReVel, proj.Type, proj.Damage, proj.KnockBack, Main.myPlayer);

                        // 继承持续时间
                        Main.projectile[newProj2].timeLeft = proj.Lift;
                    }
                }

                // 如果开启持续追踪模式
                if (up.Backer && proj.UpdateTime > 0)
                {
                    // 逐渐转向Boss方向
                    Vector2 desiredVel = (npc.Center - NewProj.Center).SafeNormalize(Vector2.Zero);
                    NewProj.velocity = Vector2.Lerp(NewProj.velocity, desiredVel * up.Velocity, 0.1f);

                    add(UpList, index); // 添加到更新列表
                }
            }
        }

        //如果有需要更新的弹幕索引，则发送数据包统一更新
        if (UpList == null || UpList.Count == 0) return;
        foreach (var all in UpList)
        {
            TSPlayer.All.SendData(PacketTypes.ProjectileNew, null, all, 0f, 0f, 0f, 0);
        }
    }

    //添加需要更新的弹幕索引到列表
    private static void add(List<int> UpList, int index)
    {
        if (!UpList.Contains(index))
        {
            UpList.Add(index);
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
}
