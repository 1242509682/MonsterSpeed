using Microsoft.Xna.Framework;
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

// 生成弹幕状态管理类
public class SpawnProjState
{
    public int Index = 0; //存储弹幕组索引
    public int SPCount = 0; //用于追踪所有弹幕生成次数
    public Dictionary<int, int> SendStack = new Dictionary<int, int>(); //追踪每发弹幕的数量
    public Dictionary<int, float> EachCooldowns = new Dictionary<int, float>(); //追踪每发弹幕之间的发射间隔
}

// 更新弹幕数据
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

    // 智能行为
    [JsonProperty("追踪模式", Order = 7)]
    public bool Homing = false;
    [JsonProperty("追踪强度", Order = 8)]
    public float HomingStrength = 0.1f;
    [JsonProperty("最大追踪角度", Order = 9)]
    public float MaxHomingAngle = 45f;
    [JsonProperty("预测秒数", Order = 10)]
    public float PredictTime = 0f;
    [JsonProperty("智能规避", Order = 11)]
    public bool SmartDodge = false;
    [JsonProperty("规避距离", Order = 12)]
    public float DodgeDistance = 100f;

    // 运动模式
    [JsonProperty("运动模式(0直线/1正弦/2螺旋/3环绕/4八字)", Order = 13)]
    public int MovementPattern = 0;
    [JsonProperty("正弦振幅", Order = 14)]
    public float SineAmplitude = 0f;
    [JsonProperty("正弦频率", Order = 15)]
    public float SineFrequency = 1f;
    [JsonProperty("螺旋半径", Order = 16)]
    public float SpiralRadius = 0f;
    [JsonProperty("螺旋速度", Order = 17)]
    public float SpiralSpeed = 1f;
    [JsonProperty("环绕半径", Order = 18)]
    public float OrbitRadius = 0f;
    [JsonProperty("环绕速度", Order = 19)]
    public float OrbitSpeed = 1f;
    [JsonProperty("八字幅度", Order = 20)]
    public float EightAmplitude = 0f;
    [JsonProperty("八字频率", Order = 21)]
    public float EightFrequency = 1f;

    [JsonProperty("弹幕AI", Order = 22)]
    public Dictionary<int, float> ai { get; set; } = new Dictionary<int, float>();
}

// 用于存储更新弹幕的信息
public class UpdateProj
{
    //弹幕索引
    public int Index { get; set; }
    //怪物索引
    public int whoAmI { get; set; }
    //弹幕ID
    public int Type { get; set; }
    // 运动状态
    public float MovementTimer { get; set; }

    public UpdateProj(int index, int useIndex, int type)
    {
        Index = index;
        whoAmI = useIndex;
        Type = type;
        MovementTimer = 0f;
    }
}

internal class MyProjectile
{
    #region 弹幕生成方法
    public static Dictionary<int, DateTime> UpdateTimer = new(); //用于追踪更新弹幕的时间
    public static void SpawnProjectile(List<ProjData> SpawnProj, NPC npc)
    {
        if (SpawnProj == null || SpawnProj.Count == 0) return;

        var tar = npc.GetTargetData(true); // 获取玩家与怪物的距离和相对位置向量
        var range = Vector2.Distance(tar.Center, npc.Center);
        var dict = tar.Center - npc.Center; // 目标到NPC的方向向量

        // 获取NPC的状态
        var state = GetState(npc);
        if (state == null) return;

        if (state.Index >= SpawnProj.Count) return;

        ProjData proj = SpawnProj[state.Index];

        // 初始化当前弹幕组的发射数量
        if (!state.SendStack.ContainsKey(state.Index)) state.SendStack[state.Index] = 0;

        // 初始化每个弹幕的发射间隔
        if (!state.EachCooldowns.ContainsKey(state.Index)) state.EachCooldowns[state.Index] = 0;

        // 获取距离和方向向量 新增目标筛选逻辑
        Player plr = Main.player[npc.target];

        // 数量超标 目标无效 或 不在进度则跳过
        if (state.SendStack[state.Index] >= proj.stack || proj.Type <= 0 || plr == null)
        {
            Next(SpawnProj, state);
            return;
        }

        // 检查冷却时间
        if (state.EachCooldowns[state.Index] > 0f)
        {
            state.EachCooldowns[state.Index] -= 1f;
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
                decay = 1.0f - state.SendStack[state.Index] / (float)proj.stack * proj.decay;
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
            double Angle = (state.SendStack[state.Index] - (proj.stack - 1) / 2.0f) * addRadian;

            // 如果旋转角度不为0，则设置旋转角度
            if (proj.Rotate != 0)
            {
                vel = vel.RotatedBy(Angle + proj.Rotate * state.SendStack[state.Index]);
            }

            //以"玩家为中心"为true 以玩家为中心,否则以被击中的npc为中心
            Vector2 pos = proj.TarCenter
                    ? new Vector2(plr.Center.X, plr.Center.Y)
                    : new Vector2(npc.Center.X, npc.Center.Y);

            //中心半径
            Vector2 NewPos = pos;
            if (proj.Radius != 0)
            {
                // 计算相对于中心点的偏移量，直接使用 偏移半径 作为偏移距离
                double ExAngle = state.SendStack[state.Index] / (float)(proj.stack - 1) * MathHelper.TwoPi; // 均匀分布的角度
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
                var newProj = Projectile.NewProjectile(npc.GetSpawnSourceForNPCFromNPCAI(),
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
                        UpdateState[newProj] = new UpdateProj(newProj, npc.whoAmI, proj.Type);

                        // 如果没有记录过这个弹幕的更新计时器，则初始化
                        if (!UpdateTimer.ContainsKey(newProj))
                        {
                            UpdateTimer[newProj] = DateTime.UtcNow;
                        }

                        if ((DateTime.UtcNow - UpdateTimer[newProj]).TotalMilliseconds >= proj.UpdateTime)
                        {
                            UpdateProjectile(proj, proj.UpdateProj, npc, plr, proj.Type, proj.stack, state);
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

            state.SendStack[state.Index]++; //更新发射数计数器
            state.EachCooldowns[state.Index] = proj.interval;  //设置间隔
        }
    }
    #endregion

    #region 更新弹幕方法
    public static UpdateProj[] UpdateState { get; set; } = new UpdateProj[Main.maxProjectiles];  //用于存储已经发射出去的弹幕
    private static void UpdateProjectile(ProjData proj, List<UpdateProjData> Update, NPC npc, Player plr, int type, int stack, SpawnProjState state)
    {
        if (Update == null || Update.Count <= 0) return;

        //存储需要更新的弹幕属性
        List<int> UpList = new List<int>();

        foreach (var up in Update)
        {
            if (up == null) continue;

            for (var i = 0; i < UpdateState.Length; i++)
            {
                if (UpdateState[i] == null) continue;
                if (type <= 0 || UpdateState[i].Index < 0 || UpdateState[i].Type != type || UpdateState[i].whoAmI != npc.whoAmI) continue;

                int index = UpdateState[i].Index;
                UpdateProj updateState = UpdateState[i];

                Projectile NewProj = Main.projectile[index];

                if (NewProj == null || !NewProj.active || NewProj.type != type || NewProj.owner != Main.myPlayer)
                {
                    continue;
                }

                // 更新运动计时器
                updateState.MovementTimer += 1f;

                // 计算衰减值
                float decay = 0;
                //随着弹幕数量的增加而减慢
                if (up.decayForStack)
                {
                    decay = 1.0f - state.SendStack[state.Index] / (float)stack * up.decay;
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
                    Add(UpList, index);
                }
                else
                {
                    speed2 = (float)NewProj.velocity.Length();
                }

                // 计算速度向量
                Vector2 vel = NewProj.velocity.SafeNormalize(Vector2.Zero) * speed2;

                // 应用智能行为
                vel = ApplyIntelligentBehavior(vel, NewProj, plr, up, updateState, ref UpList);

                // 应用运动模式
                vel = ApplyMovementPattern(vel, NewProj, plr, up, updateState, ref UpList);

                // 计算角度偏移
                double angle = up.Angle * Math.PI / 180;
                double radian;
                if (angle != 0)
                {
                    radian = (double)angle;
                    Add(UpList, index);
                }
                else
                {
                    radian = 0;
                }

                // 计算每次发射的弧度增量
                double addRadian = radian * 2.0f / (stack - 1);
                double Angle = (state.SendStack[state.Index] - (stack - 1) / 2.0f) * addRadian;
                if (up.Rotate != 0)
                {
                    vel = vel.RotatedBy(Angle + ((float)(up.Rotate * state.SendStack[state.Index])));
                    Add(UpList, index);
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
                    double ExAngle = state.SendStack[state.Index] / (float)(stack - 1) * MathHelper.TwoPi;
                    Vector2 offset = new Vector2((float)Math.Cos(ExAngle), (float)Math.Sin(ExAngle)) * (Math.Abs(up.Radius) * 16) * Math.Sign(up.Radius);

                    if (up.Radius < 0)
                    {
                        offset *= -1;
                    }
                    newPos += offset;
                    Add(UpList, index);
                }

                // 检查并更新弹幕位置、速度和AI
                if (NewProj.position != newPos)
                {
                    NewProj.position = newPos;
                    Add(UpList, index);
                }

                // 更新弹幕速度
                if (NewProj.velocity != vel)
                {
                    NewProj.velocity = vel;
                    Add(UpList, index);
                }

                // 更新弹幕AI
                if (up.ai.Count > 0)
                {
                    for (var j = 0; j < NewProj.ai.Count(); j++)
                    {
                        if (up.ai.ContainsKey(j) && up.ai.TryGetValue(j, out var value))
                        {
                            Main.projectile[index].ai[j] = value;
                            Add(UpList, index);
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

                    Add(UpList, index); // 添加到更新列表
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
    #endregion

    #region 添加需要更新的弹幕索引到列表
    private static void Add(List<int> UpList, int index)
    {
        if (!UpList.Contains(index))
        {
            UpList.Add(index);
        }
    }
    #endregion

    #region 移动到下一个要发射的弹幕方法
    private static void Next(List<ProjData> data, SpawnProjState state)
    {
        //只有当前组的所有弹幕都发射完毕时才更新索引
        if (state.SendStack.ContainsKey(state.Index) && state.SendStack[state.Index] >= data[state.Index].stack)
        {
            state.Index++;
            if (state.Index >= data.Count)
            {
                state.Index = 0;
            }
            state.SendStack[state.Index] = 0; // 重置发射数量 
            state.EachCooldowns[state.Index] = 0f; // 重置冷却时间
            state.SPCount++; //增加弹幕生成次数
        }
    }
    #endregion

    #region 智能行为方法
    /// <summary>
    /// 应用智能行为到弹幕速度
    /// </summary>
    private static Vector2 ApplyIntelligentBehavior(Vector2 vel, Projectile proj, Player plr, UpdateProjData up, UpdateProj state, ref List<int> upList)
    {
        Vector2 result = vel;

        // 追踪模式
        if (up.Homing && plr != null && plr.active)
        {
            Vector2 pos = plr.Center;

            // 预测走位
            if (up.PredictTime > 0)
            {
                pos += plr.velocity * up.PredictTime * 60f; // 预测目标位置
            }

            Vector2 dir = pos - proj.Center;
            dir = dir.SafeNormalize(Vector2.Zero);

            // 计算当前方向与目标方向的夹角
            float NowAngle = (float)Math.Atan2(vel.Y, vel.X);
            float tarAngle = (float)Math.Atan2(dir.Y, dir.X);
            float angleDiff = tarAngle - NowAngle;

            // 规范化角度差
            while (angleDiff > Math.PI) angleDiff -= (float)Math.PI * 2;
            while (angleDiff < -Math.PI) angleDiff += (float)Math.PI * 2;

            // 应用最大追踪角度限制
            float maxAngle = MathHelper.ToRadians(up.MaxHomingAngle);
            angleDiff = MathHelper.Clamp(angleDiff, -maxAngle, maxAngle);

            // 应用追踪强度
            float Strength = up.HomingStrength;
            float newAngle = NowAngle + angleDiff * Strength;

            // 创建新的速度向量
            result = new Vector2((float)Math.Cos(newAngle), (float)Math.Sin(newAngle)) * vel.Length();
            Add(upList, proj.whoAmI);
        }

        // 智能规避（简化版）
        if (up.SmartDodge)
        {
            // 检测前方障碍物
            float dodge = up.DodgeDistance;
            Vector2 checkPos = proj.Center + vel.SafeNormalize(Vector2.Zero) * dodge;

            // 简单规避逻辑：检测到障碍物时轻微转向
            if (Collision.SolidCollision(checkPos, proj.width, proj.height))
            {
                // 随机选择左右转向
                float dodgeAngle = (Main.rand.Next(2) == 0) ? MathHelper.PiOver4 : -MathHelper.PiOver4;
                result = result.RotatedBy(dodgeAngle * 0.1f); // 轻微转向
                Add(upList, proj.whoAmI);
            }
        }

        return result;
    }
    #endregion

    #region 运动模式方法
    /// <summary>
    /// 应用运动模式到弹幕速度
    /// </summary>
    private static Vector2 ApplyMovementPattern(Vector2 vel, Projectile proj, Player plr, UpdateProjData up, UpdateProj state, ref List<int> upList)
    {
        Vector2 result = vel;
        float timer = state.MovementTimer;

        switch (up.MovementPattern)
        {
            case 1: // 正弦运动
                if (up.SineAmplitude > 0)
                {
                    float sineOffset = (float)Math.Sin(timer * 0.1f * up.SineFrequency) * up.SineAmplitude;
                    Vector2 perpendicular = new Vector2(-vel.Y, vel.X).SafeNormalize(Vector2.Zero);
                    result += perpendicular * sineOffset;
                    Add(upList, proj.whoAmI);
                }
                break;

            case 2: // 螺旋运动
                if (up.SpiralRadius > 0)
                {
                    float spiralAngle = timer * 0.1f * up.SpiralSpeed;
                    Vector2 spiralOffset = new Vector2(
                        (float)Math.Cos(spiralAngle) * up.SpiralRadius,
                        (float)Math.Sin(spiralAngle) * up.SpiralRadius
                    );
                    result = vel.SafeNormalize(Vector2.Zero) * vel.Length() + spiralOffset * 0.1f;
                    Add(upList, proj.whoAmI);
                }
                break;

            case 3: // 环绕运动
                if (up.OrbitRadius > 0)
                {
                    if (plr != null && plr.active)
                    {
                        float orbitAngle = timer * 0.1f * up.OrbitSpeed;
                        Vector2 orbitPos = plr.Center + new Vector2(
                            (float)Math.Cos(orbitAngle) * up.OrbitRadius,
                            (float)Math.Sin(orbitAngle) * up.OrbitRadius
                        );
                        result = (orbitPos - proj.Center).SafeNormalize(Vector2.Zero) * vel.Length();
                        Add(upList, proj.whoAmI);
                    }
                }
                break;

            case 4: // 八字运动
                if (up.EightAmplitude > 0)
                {
                    float eightX = (float)Math.Sin(timer * 0.1f * up.EightFrequency) * up.EightAmplitude;
                    float eightY = (float)Math.Sin(timer * 0.1f * up.EightFrequency * 2) * up.EightAmplitude * 0.5f;
                    Vector2 eightOffset = new Vector2(eightX, eightY);
                    result += eightOffset * 0.1f;
                    Add(upList, proj.whoAmI);
                }
                break;
        }

        return result;
    }
    #endregion

    #region 状态管理
    public static Dictionary<int, SpawnProjState> ProjStates = new Dictionary<int, SpawnProjState>(); // 存储每个NPC的状态

    /// <summary>
    /// 获取或创建NPC的状态
    /// </summary>
    /// <param name="npc">NPC实例</param>
    /// <returns>状态对象</returns>
    public static SpawnProjState? GetState(NPC npc)
    {
        if (npc == null || !npc.active)
            return new SpawnProjState();

        if (!ProjStates.ContainsKey(npc.whoAmI))
        {
            ProjStates[npc.whoAmI] = new SpawnProjState();
        }
        return ProjStates[npc.whoAmI];
    }

    /// <summary>
    /// 清理NPC的状态
    /// </summary>
    /// <param name="npc">NPC实例</param>
    public static void ClearState(NPC npc)
    {
        if (npc != null && ProjStates.ContainsKey(npc.whoAmI))
        {
            ProjStates.Remove(npc.whoAmI);
        }
    }

    /// <summary>
    /// 清理所有状态（用于重置）
    /// </summary>
    public static void ClearAllStates()
    {
        ProjStates.Clear();
    }

    // 清理UpdateTimer中与该NPC相关的条目
    public static void ClearUpState(NPC npc)
    {
        // 清理UpdateTimer中与该NPC相关的条目
        var Remove = UpdateTimer.Keys
            .Where(key =>
            {
                var updateProj = UpdateState[key];
                return updateProj != null && updateProj.whoAmI == npc.whoAmI;
            })
            .ToList();

        foreach (var key in Remove)
        {
            UpdateTimer.Remove(key);
            UpdateState[key] = null;
        }
    }
    #endregion
}