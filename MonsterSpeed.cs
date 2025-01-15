using System.Text;
using Microsoft.Xna.Framework;
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
    public override Version Version => new Version(1, 2, 1);
    public override string Description => "涡轮增压不蒸鸭";
    #endregion

    #region 注册与释放
    public MonsterSpeed(Main game) : base(game) { }
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
        if (!Config.Enabled || args.Npc == null ||
            !Config.NpcList.Contains(args.Npc.netID))
        {
            return;
        }

        var NewNpc = !Config.Dict!.ContainsKey(args.Npc.FullName);
        if (NewNpc)
        {
            var newData = new Configuration.NpcData()
            {
                DeadCount = 0,
                AutoTarget = true,
                TrackSpeed = 35,
                TrackRange = 62,
                TrackStopRange = 25,
                AutoHeal = 20,
                CoolTimer = 5f,
                TextInterval = 500f,
                TimerEvent = new List<TimerData>() { }
            };

            Config.Dict[args.Npc.FullName] = newData;
            TimerEvents.CoolTrack[args.Npc.FullName] = (0, DateTime.UtcNow);
            Config.Write();
        }

        var (CD_Count, CD_Timer) = TimerEvents.GetOrAdd(args.Npc.FullName);
        TimerEvents.UpdateTrack(args.Npc.FullName, CD_Count, CD_Timer);
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

        Custom(npc, data); // 自定义修改

        npc.netUpdate = true;

        //监控广播
        if (Config.Monitorinterval > 0 && (DateTime.UtcNow - BroadcastTime).TotalMilliseconds >= Config.Monitorinterval)
        {
            mess.Append($" [c/3A89D0:{npc.FullName}] 【x】[c/38E06D:{npc.velocity.X:F0}] " +
            $"【y】[c/A5CEBB:{npc.velocity.Y:F0}] 【style】[c/3A89D0:{npc.aiStyle}]\n" +
            $" [ai0] [c/F3A144:{npc.ai[0]:F0}] [ai1] [c/D2A5DF:{npc.ai[1]:F0}] " +
            $"[ai2] [c/EBEB91:{npc.ai[2]:F0}] [ai3] [c/35E635:{npc.ai[3]:F0}]\n");

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
                if (data.AutoTarget)
                {
                    npc.TargetClosest(true);
                    npc.netSpam = 0;
                    npc.spriteDirection = npc.direction = Terraria.Utils.ToDirectionInt(npc.velocity.X > 0f);
                }

                //超距离传送
                if (data.Teleport > 0 && (!Teleport.ContainsKey(npc.FullName) ||
                   (DateTime.UtcNow - Teleport[npc.FullName]).TotalSeconds >= data.Teleport))
                {
                    npc.Teleport(tar.Center,10);
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

    #region 自定义修改
    private Dictionary<string, DateTime> HealTimes = new Dictionary<string, DateTime>(); // 跟踪每个NPC上次回血的时间
    private void Custom(NPC npc, NpcData data)
    {
        if (data == null) return;

        //免疫岩浆 免疫陷阱 能够穿墙
        npc.lavaImmune |= data.lavaImmune;
        npc.trapImmune |= data.trapImmune;
        npc.noTileCollide |= data.NoTileCollide;

        //修改防御
        if (data.Defense > 0)
        {
            npc.defense = npc.defDefense = data.Defense;
        }

        //自动回血
        if (data.AutoHeal > 0)
        {
            if (!HealTimes.ContainsKey(npc.FullName))
            {
                HealTimes[npc.FullName] = DateTime.UtcNow.AddSeconds(-1); // 初始化为1秒前，确保第一次调用时立即回血
            }

            if ((DateTime.UtcNow - HealTimes[npc.FullName]).TotalMilliseconds >= 1000f) // 每秒回血
            {
                // 将AutoHeal视为百分比并计算相应的生命值恢复量
                var num = (int)(npc.lifeMax * (data.AutoHeal / 100.0f));
                npc.life = (int)Math.Min(npc.lifeMax, npc.life + num);
                HealTimes[npc.FullName] = DateTime.UtcNow;
            }
        }
    }
    #endregion

}