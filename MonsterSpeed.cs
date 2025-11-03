using System.Text;
using Microsoft.Xna.Framework;
using ReLogic.Text;
using Terraria;
using Terraria.DataStructures;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using static MonsterSpeed.Configuration;

namespace MonsterSpeed;

[ApiVersion(2, 1)]
public class MonsterSpeed : TerrariaPlugin
{
    #region 插件信息
    public override string Name => "怪物加速";
    public override string Author => "羽学";
    public override Version Version => new Version(1, 3, 0);
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
            TrackSpeed = 25,
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
        Teleport.Remove(args.npc.FullName);
        HealTimes.Remove(args.npc.FullName);

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

        var tar = npc.GetTargetData(true); // 获取玩家与怪物的距离和相对位置向量
        if (tar.Invalid) return; //目标无效返回
        var range = Vector2.Distance(tar.Center, npc.Center);
        var dict = tar.Center - npc.Center; // 目标到NPC的方向向量

        // 自动回血
        if (data.AutoHeal > 0)
        {
            AutoHeal(npc, data);
        }

        var handled = false;
        TimerEvents.TimerEvent(npc, mess, data, dict, range, ref handled); //时间事件

        TrackMode(npc, data, tar, range, dict); //超距离追击

        npc.netUpdate = true;

        //监控广播
        Broadcast(mess, npc, data);

        args.Handled = handled;
    }
    #endregion

    #region 超距离追击模式
    private Dictionary<string, DateTime> Teleport = new Dictionary<string, DateTime>(); // 跟踪每个NPC上次传送的时间
    private void TrackMode(NPC npc, NpcData data, NPCAimedTarget tar, float range, Vector2 dict)
    {
        if (data == null) return;
        if (data.TrackRange != 0)
        {
            if (range > data.TrackRange * 16f) // 超距离追击
            {
                var speedMax = dict * data.TrackSpeed + tar.Velocity;
                if (speedMax.Length() > data.TrackSpeed)
                {
                    speedMax.Normalize();
                    speedMax *= data.TrackSpeed;
                }
                npc.velocity = speedMax;

                //自动转换仇恨目标
                AutoTar(npc, data);

                //超距离传送
                if (data.Teleport > 0 && (!Teleport.ContainsKey(npc.FullName) ||
                   (DateTime.UtcNow - Teleport[npc.FullName]).TotalSeconds >= data.Teleport))
                {
                    npc.Teleport(tar.Center, 10);
                    Teleport[npc.FullName] = DateTime.UtcNow;
                }

                npc.netUpdate = true;
                return;
            }
            else if (range < data.TrackStopRange)  // 在最小距离内停止
            {
                npc.netUpdate = false;
                return;
            }
        }
    }
    #endregion

    #region 自动仇恨方法
    internal static void AutoTar(NPC npc, NpcData data)
    {
        if (data.AutoTarget)
        {
            npc.TargetClosest(true);
            npc.netSpam = 0;
            npc.spriteDirection = npc.direction = Terraria.Utils.ToDirectionInt(npc.velocity.X > 0f);
        }
    }
    #endregion

    #region 自动回血
    public static Dictionary<string, DateTime> HealTimes = new Dictionary<string, DateTime>(); // 跟踪每个NPC上次回血的时间
    internal static void AutoHeal(NPC npc, NpcData data)
    {
        if (!HealTimes.ContainsKey(npc.FullName))
        {
            HealTimes[npc.FullName] = DateTime.UtcNow.AddSeconds(-data.AutoHealInterval); // 初始化为1秒前，确保第一次调用时立即回血
        }

        // 回血间隔
        if ((DateTime.UtcNow - HealTimes[npc.FullName]).TotalMilliseconds >= data.AutoHealInterval * 1000)
        {
            // 将AutoHeal视为百分比并计算相应的生命值恢复量
            var num = (int)(npc.lifeMax * (data.AutoHeal / 100.0f));
            npc.life = (int)Math.Min(npc.lifeMax, npc.life + num);
            HealTimes[npc.FullName] = DateTime.UtcNow;
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