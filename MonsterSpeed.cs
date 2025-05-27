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
    public override Version Version => new Version(1, 2, 4);
    public override string Description => "使boss拥有高速追击能力，并支持修改其弹幕、随从、血量、防御等功能";
    #endregion

    #region 注册与释放
    public MonsterSpeed(Main game) : base(game) 
    {
        MyProjectile.SpawnPorj = new SpawnProj[1001];
    }

    public override void Initialize()
    {
        LoadConfig();
        ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
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
            ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
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

    #region 世界更新事件
    private void OnGameUpdate(EventArgs args)
    {
        GameTimer.Update();
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

        var NewNpc = !Config.Dict!.ContainsKey(npc.FullName);
        if (NewNpc)
        {
            var newData = new Configuration.NpcData()
            {
                DeadCount = 0,
                AutoTarget = true,
                TrackSpeed = 35,
                TrackRange = 62,
                TrackStopRange = 25,
                CoolTimer = 5f,
                TextInterval = 1000f,
                TimerEvent = new List<TimerData>()
                {
                    new TimerData()
                    {
                        Condition = new List<ConditionData>()
                        {
                            new ConditionData()
                            {
                                NpcLift = "0,100"
                            }
                        },

                        SendProj = new List<ProjData>()
                        {
                            new ProjData()
                            {
                                Type = 115,
                                Damage = 30,
                                stack = 15,
                                interval = 60f,
                                KnockBack = 5,
                                Velocity = 10f,
                            }
                        },
                    }
                },
            };

            Config.Dict[npc.FullName] = newData;
            TimerEvents.CoolTrack[npc.FullName] = (0, DateTime.UtcNow);
            Config.Write();
        }

        var (CD_Count, CD_Timer) = TimerEvents.GetOrAdd(npc.FullName);
        TimerEvents.UpdateTrack(npc.FullName, CD_Count, CD_Timer);
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

        var (CD_Count, CD_Timer) = TimerEvents.GetOrAdd(args.npc.FullName);
        Config.Dict!.TryGetValue(args.npc.FullName, out var data);
        if (data != null)
        {
            CD_Count = 0;
            data.DeadCount += 1;
            MyMonster.SNCount = 0;
            MyProjectile.SPCount = 0;

            Config.Write();
            TimerEvents.UpdateTrack(args.npc.FullName, CD_Count, CD_Timer);
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

        var tar = npc.GetTargetData(true);  // 获取玩家与怪物的距离和相对位置向量
        if (tar.Invalid) return; //目标无效返回
        var range = Vector2.Distance(tar.Center, npc.Center);
        var dict = tar.Center - npc.Center; // 目标到NPC的方向向量

        TimerEvents.TimerEvent(npc, mess, data, dict, range); //时间事件 

        TrackMode(npc, data, tar, range, dict); //超距离追击

        npc.netUpdate = true;

        //监控广播
        if (Config.Monitorinterval > 0 && (DateTime.UtcNow - BroadcastTime).TotalMilliseconds >= Config.Monitorinterval)
        {
            mess.Append($" [c/3A89D0:{npc.FullName}] 【x】[c/38E06D:{npc.velocity.X:F0}] " +
            $"【y】[c/A5CEBB:{npc.velocity.Y:F0}] 【style】[c/3A89D0:{npc.aiStyle}]\n" +
            $" [ai0] [c/F3A144:{npc.ai[0]:F0}] [ai1] [c/D2A5DF:{npc.ai[1]:F0}]" +
            $" [ai2] [c/EBEB91:{npc.ai[2]:F0}] [ai3] [c/35E635:{npc.ai[3]:F0}]\n" +
            $" [de] [c/3A89D0:{npc.defense}]\n");

            TSPlayer.All.SendMessage($"{mess}", 170, 170, 170);
            BroadcastTime = DateTime.UtcNow;
        }

    }
    #endregion

    #region 超距离追击模式
    private Dictionary<string, DateTime> Teleport = new Dictionary<string, DateTime>(); // 跟踪每个NPC上次回血的时间
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
            else if (range < data.TrackStopRange) // 在最小距离内停止
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
    #endregion.

}
