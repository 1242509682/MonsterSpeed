using System.Text;
using Microsoft.Xna.Framework;
using ReLogic.Text;
using Terraria;
using Terraria.DataStructures;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using static MonoMod.InlineRT.MonoModRule;
using static MonsterSpeed.Configuration;

namespace MonsterSpeed;

[ApiVersion(2, 1)]
public class MonsterSpeed : TerrariaPlugin
{
    #region 插件信息
    public override string Name => "怪物加速";
    public override string Author => "羽学";
    public override Version Version => new Version(1, 3, 2);
    public override string Description => "使boss拥有高速追击能力，并支持修改其弹幕、随从、Ai、防御等功能";
    #endregion

    #region 注册与释放
    public MonsterSpeed(Main game) : base(game)
    {
        MyProjectile.UpdateState = new UpdateProj[1001];
    }

    public override void Initialize()
    {
        LoadConfig();
        GeneralHooks.ReloadEvent += ReloadConfig;
        ServerApi.Hooks.NpcKilled.Register(this, this.OnNPCKilled);
        ServerApi.Hooks.NpcStrike.Register(this, this.OnNpcStrike);
        ServerApi.Hooks.NpcAIUpdate.Register(this, this.OnNpcAiUpdate);
        TShockAPI.Commands.ChatCommands.Add(new TShockAPI.Command("mos.admin", Command.CMD, "怪物加速", "mos"));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GeneralHooks.ReloadEvent -= ReloadConfig;
            ServerApi.Hooks.NpcKilled.Deregister(this, this.OnNPCKilled);
            ServerApi.Hooks.NpcStrike.Deregister(this, this.OnNpcStrike);
            ServerApi.Hooks.NpcAIUpdate.Deregister(this, this.OnNpcAiUpdate);
            TShockAPI.Commands.ChatCommands.RemoveAll(x => x.CommandDelegate == Command.CMD);
        }
        base.Dispose(disposing);
    }
    #endregion

    #region 配置重载读取与写入方法
    internal static Configuration Config = new();
    private static void ReloadConfig(ReloadEventArgs args = null!)
    {
        LoadConfig();
        args.Player.SendInfoMessage("[怪物加速]重新加载配置完毕。");
    }
    private static void LoadConfig()
    {
        Config = Configuration.Read();
        Config.Write();
    }
    #endregion

    #region 伤怪建表法
    private void OnNpcStrike(NpcStrikeEventArgs args)
    {
        var npc = args.Npc;
        if (!Config.Enabled || npc == null ||
            !Config.NpcList.Contains(npc.netID))
        {
            return;
        }

        var newNpc = !Config.Dict!.ContainsKey(npc.FullName);
        if (newNpc)
        {
            NpcData nd = NewData();
            Config.Dict[npc.FullName] = nd;
            Config.Write();

            var state = TimerEvents.GetState(npc);
            if (state != null)
            {
                state.Index = 0;
                state.UpdateTimer = DateTime.UtcNow;
                state.FileState = new FilePlayState();
                state.PauseState = new PauseState();
            }
        }
    }
    #endregion

    #region 创建新数据
    internal static NpcData NewData()
    {
        var newData = new Configuration.NpcData()
        {
            DeadCount = 0,
            AutoTarget = true,
            TrackSpeed = 35,
            TrackRange = 62,
            TrackStopRange = 25,
            ActiveTime = 5f,
            TextInterval = 1000f,
            TimerEvent = new List<TimerData>()
            {
                new TimerData()
                {
                    Condition = new Conditions()
                    {
                        NpcLift = "0,100"
                    }
                }
            },
        };

        return newData;
    }
    #endregion

    #region 怪物死亡更新数据方法
    private void OnNPCKilled(NpcKilledEventArgs args)
    {
        if (!Config.Enabled || args.npc == null ||
            !Config.NpcList.Contains(args.npc.netID))
        {
            return;
        }

        // 清理MyProjectile中的状态
        MyProjectile.ClearState(args.npc);
        MyProjectile.ClearUpState(args.npc);
        // 清理MyMonster中的状态
        MyMonster.ClearState(args.npc);
        // 清理TimerEvents中的状态
        TimerEvents.ClearStates(args.npc);
        // 清理传送和回血记录
        Teleport.Remove(args.npc.whoAmI);
        HealTimes.Remove(args.npc.whoAmI);

        // 更新配置数据
        Config.Dict!.TryGetValue(args.npc.FullName, out var data);
        if (data != null)
        {
            data.DeadCount += 1;
            Config.Write();
        }
    }
    #endregion

    #region 怪物加速核心方法
    private static DateTime BroadcastTime = DateTime.UtcNow; // 跟踪最后一次广播时间
    private void OnNpcAiUpdate(NpcAiUpdateEventArgs args)
    {
        var mess = new StringBuilder(); //用于存储广播内容
        var npc = args.Npc;
        Config.Dict!.TryGetValue(npc.FullName, out var data);

        if (npc == null || data == null || !Config.Enabled || !npc.active ||
            npc.townNPC || npc.SpawnedFromStatue || npc.netID == 488 ||
            !Config.NpcList.Contains(npc.netID))
        {
            return;
        }

        // 自动回血
        if (data.AutoHeal > 0)
        {
            AutoHeal(npc, data);
        }

        var handled = false;
        TimerEvents.TimerEvent(npc, mess, data, ref handled); //时间事件

        TrackMode(npc, data); //超距离追击

        npc.netUpdate = true;

        //监控广播
        Broadcast(mess, npc, data);

        args.Handled = handled;
    }
    #endregion

    #region 超距离追击模式
    private Dictionary<int, DateTime> Teleport = new Dictionary<int, DateTime>(); // 跟踪每个NPC上次传送的时间
    private void TrackMode(NPC npc, NpcData data)
    {
        if (data == null || !data.AutoTrack) return;

        var tar = npc.GetTargetData(true); // 获取玩家与怪物的距离和相对位置向量
        var dict = tar.Center - npc.Center; // 目标到NPC的方向向量
        var range = Vector2.Distance(tar.Center, npc.Center);

        if (data.TrackRange != 0)
        {
            // 使用平方距离比较优化性能
            float TrackRange = data.TrackRange * 16f;
            float TrackStopRange = data.TrackStopRange * 16f;
            // 计算中间区域范围
            float SmoothRange = TrackStopRange + (TrackRange - TrackStopRange) * 0.5f;

            if (range > TrackRange) // 超距离追击
            {
                // 动态速度调整：距离越远速度越快
                float dicRatio = (range - TrackRange) / TrackRange;
                float dySpeed = data.TrackSpeed * (1f + Math.Min(dicRatio, 1f) * 0.5f);

                // 优化速度计算
                Vector2 speedMax = dict * dySpeed + tar.Velocity * 0.3f;
                if (speedMax.Length() > dySpeed)
                {
                    speedMax.Normalize();
                    speedMax *= dySpeed;
                }

                // 平滑速度过渡
                npc.velocity = Vector2.Lerp(npc.velocity, speedMax, 0.6f);

                // 智能目标切换：只在很远的距离切换
                if (range > TrackRange * 1.5f)
                {
                    AutoTar(npc, data);
                }

                // 优化传送逻辑
                if (data.Teleport > 0)
                {
                    SmartTeleport(npc, data, tar, range);
                }
            }
            else if (range > TrackStopRange && range <= SmoothRange) // 中间区域平滑处理
            {
                npc.velocity *= 0.95f;
            }
            else if (range < TrackStopRange)  // 在最小距离内停止更新恢复原版AI行为
            {
                return;
            }
        }
    }
    #endregion

    #region 智能传送方法
    private void SmartTeleport(NPC npc, NpcData data, NPCAimedTarget tar, float range)
    {
        bool canTeleport = !Teleport.ContainsKey(npc.whoAmI) ||
                          (DateTime.UtcNow - Teleport[npc.whoAmI]).TotalSeconds >= data.Teleport;

        // 只在足够远的距离传送，避免频繁传送
        if (canTeleport && range > data.TrackRange * 20f)
        {
            // 尝试找到安全传送位置
            Vector2 safePos = FindSafePosition(tar.Center, data.TrackStopRange * 16f);
            if (safePos != Vector2.Zero)
            {
                npc.Teleport(safePos, 10);
                Teleport[npc.whoAmI] = DateTime.UtcNow;
            }
        }
    }
    #endregion

    #region 传送安全位置检测
    private Vector2 FindSafePosition(Vector2 Center, float TrackStopRange)
    {
        // 在目标周围随机寻找安全位置
        for (int i = 0; i < 5; i++)
        {
            Vector2 testPos = Center + new Vector2(Main.rand.Next(-80, 81),Main.rand.Next(-80, 81));

            // 简单的位置有效性检查
            Tile tile = (Tile)Framing.GetTileSafely((int)testPos.X / 16, (int)testPos.Y / 16);
            if (!tile.active() || !Main.tileSolid[tile.type])
            {
                return testPos;
            }
        }

        // 没找到安全位置就传送到追击停止范围
        return new Vector2 (Center.X + TrackStopRange, Center.Y + TrackStopRange); 
    }
    #endregion

    #region 自动仇恨方法（优化）
    internal static void AutoTar(NPC npc, NpcData data)
    {
        if (data.AutoTarget)
        {
            // 保存当前速度方向
            float SpeedX = npc.velocity.X;

            npc.TargetClosest(true);
            npc.netSpam = 0;

            // 保持原有的移动方向逻辑
            npc.spriteDirection = npc.direction = Terraria.Utils.ToDirectionInt(npc.velocity.X > 0f);

            // 如果速度方向改变，平滑过渡
            if (SpeedX * npc.velocity.X < 0)
            {
                npc.velocity.X *= 0.7f;
            }
        }
    }
    #endregion

    #region 自动回血
    public static Dictionary<int, DateTime> HealTimes = new Dictionary<int, DateTime>(); // 跟踪每个NPC上次回血的时间
    internal static void AutoHeal(NPC npc, NpcData data)
    {
        if (!HealTimes.ContainsKey(npc.whoAmI))
        {
            HealTimes[npc.whoAmI] = DateTime.UtcNow.AddSeconds(-data.AutoHealInterval); // 初始化为1秒前，确保第一次调用时立即回血
        }

        // 回血间隔
        if ((DateTime.UtcNow - HealTimes[npc.whoAmI]).TotalMilliseconds >= data.AutoHealInterval * 1000)
        {
            // 将AutoHeal视为百分比并计算相应的生命值恢复量
            var num = (int)(npc.lifeMax * (data.AutoHeal / 100.0f));
            npc.life = (int)Math.Min(npc.lifeMax, npc.life + num);
            HealTimes[npc.whoAmI] = DateTime.UtcNow;
        }
    }
    #endregion

    #region 监控广播方法
    private static void Broadcast(StringBuilder mess, NPC npc, NpcData data)
    {
        if (Config.Monitorinterval > 0 && (DateTime.UtcNow - BroadcastTime).TotalMilliseconds >= Config.Monitorinterval)
        {
            // 使用新的状态管理方法获取当前事件索引
            var state = TimerEvents.GetState(npc);
            string aiInfo = "";
            if (data.TimerEvent != null && data.TimerEvent.Count > 0)
            {
                var idx = state!.Index;
                if (idx >= 0 && idx < data.TimerEvent.Count)
                {
                    var evt = data.TimerEvent[idx];
                    if (evt?.AIMode != null && evt.AIMode.Enabled)
                    {
                        aiInfo = AISystem.GetAiInfo(evt.AIMode, npc.FullName);
                    }
                }
            }

            // 构建基础信息
            mess.Append($" {Tool.TextGradient(" ——————————————————")}\n");
            mess.Append($" [c/3A89D0:{npc.FullName}] [防] [c/3A89D0:{npc.defense}]【x】[c/38E06D:{npc.velocity.X:F0}] " +
            $"【y】[c/A5CEBB:{npc.velocity.Y:F0}] 【style】[c/3A89D0:{npc.aiStyle}]\n" +
            $" [ai0] [c/F3A144:{npc.ai[0]:F0}] [ai1] [c/D2A5DF:{npc.ai[1]:F0}]" +
            $" [ai2] [c/EBEB91:{npc.ai[2]:F0}] [ai3] [c/35E635:{npc.ai[3]:F0}]\n");

            // 添加localAI信息
            mess.Append($" [lai0] [c/F3A144:{npc.localAI[0]:F0}] [lai1] [c/D2A5DF:{npc.localAI[1]:F0}]" +
            $" [lai2] [c/EBEB91:{npc.localAI[2]:F0}] [lai3] [c/35E635:{npc.localAI[3]:F0}]\n");

            mess.Append($" {Tool.TextGradient(" ——————————————————")}\n");

            // 添加AI模式信息
            if (!string.IsNullOrEmpty(aiInfo))
            {
                mess.Append($" {Tool.TextGradient(" ——————— ai赋值 ——————— ")} \n" +
                            $" {aiInfo} \n" +
                            $" {Tool.TextGradient(" ——————————————————— ")}");
            }

            TSPlayer.All.SendMessage($"{mess}", 170, 170, 170);
            BroadcastTime = DateTime.UtcNow;
        }
    } 
    #endregion
}