using System.Text;
using Microsoft.Xna.Framework;
using MonsterSpeed;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using static Plugin.Configuration;
using Command = MonsterSpeed.Command;

namespace Plugin;

[ApiVersion(2, 1)]
public class MonsterSpeed : TerrariaPlugin
{
    #region 插件信息
    public override string Name => "怪物加速";
    public override string Author => "羽学";
    public override Version Version => new Version(1, 0, 6);
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

    #region 冷却计数与更新冷却时间方法
    private static readonly Dictionary<string, (int CDCount, DateTime UpdateTimer)> CoolTrack = new();
    private static (int CDCount, DateTime UpdateTimer) GetOrAdd(string key)
    {
        return CoolTrack.TryGetValue(key, out var value) ? value : (CoolTrack[key] = (0, DateTime.UtcNow));
    }
    private static void UpdateTrack(string key, int cdCount, DateTime updateTimer)
    {
        CoolTrack[key] = (cdCount, updateTimer);
    }
    #endregion

    #region 伤怪建表法
    private void OnNpcStrike(NpcStrikeEventArgs args)
    {
        var npcID = args.Npc.netID;
        if (!Config.Enabled || args.Npc == null || !Config.NpcList.Contains(npcID)) return;
        var name = TShock.Utils.GetNPCById(npcID).FullName;
        var NewNpc = !Config.Dict!.ContainsKey(name);
        var (cdCount, updateTimer) = GetOrAdd(name);
        if (NewNpc)
        {
            var newData = new Configuration.NpcData()
            {
                DeadCount = 0,
                AutoTarget = true,
                Track = true,
                TrackRange = Config.TrackRange,
                Speed = Config.Speed,
                MaxSpeed = Config.MaxSpeed,
                Range = 10f,
                MaxRange = Config.MaxRange,
                CoolTimer = 5f,
                InActive = 5f,
                MaxActive = Config.MaxActive,
                LifeEvent = new List<LifeData>() { },
                TimerEvent = new List<TimerData>() { }
            };

            Config.Dict[name] = newData;
            CoolTrack[name] = (0, DateTime.UtcNow);
        }

        Config.Write();
        UpdateTrack(name, cdCount, updateTimer);
    }
    #endregion

    #region 怪物死亡更新数据方法
    private void OnNPCKilled(NpcKilledEventArgs args)
    {
        var npcID = args.npc.netID;
        if (!Config.Enabled || args.npc == null || !Config.NpcList.Contains(npcID)) return;
        var name = TShock.Utils.GetNPCById(npcID).FullName;
        var (cdCount, updateTimer) = GetOrAdd(name);
        if (Config.Dict!.TryGetValue(name, out var data) && data != null)
        {
            if (data.TimerEvent != null &&
                data.TimerEvent.Count > 0 &&
                cdCount > data.TimerEvent.Count)
            {
                cdCount = data.TimerEvent.Count;
            }
            else
            {
                cdCount = 0;
            }
            data.DeadCount += 1;
            data.Speed = Math.Min(data.MaxSpeed, data.Speed + Config.Killed);
            data.CoolTimer = Math.Max(1, data.CoolTimer - Config.Ratio);
            data.InActive = Math.Min(data.MaxActive, data.InActive + Config.Ratio);
            data.Range = Math.Max(1, data.Range - 1);
        }

        Config.Write();
        UpdateTrack(name, cdCount, updateTimer);
    }
    #endregion

    #region 怪物加速核心方法
    private static DateTime BroadcastTime = DateTime.UtcNow; // 跟踪最后一次广播时间
    private void OnNpcAiUpdate(NpcAiUpdateEventArgs args)
    {
        var npc = args.Npc;
        var now = DateTime.UtcNow;
        var name = TShock.Utils.GetNPCById(npc.type).FullName;
        var mess = new StringBuilder(); //用于存储广播内容

        if (Config.Dict == null) return;
        Config.Dict.TryGetValue(name, out var data);

        if (npc == null || data == null || !Config.Enabled || !npc.active ||
            npc.townNPC || npc.SpawnedFromStatue || npc.netID == 488)
        {
            return;
        }

        // 获取玩家与怪物的距离和相对位置向量
        var tar = npc.GetTargetData(true);
        if (tar.Invalid || !Config.NpcList.Contains(npc.type)) return;
        var range = Vector2.Distance(tar.Center, npc.Center);
        var dict = tar.Center - npc.Center; // 目标到NPC的方向向量

        //冷却时间条件
        var (cdCount, updateTimer) = GetOrAdd(name);
        var timer = now - updateTimer;
        var active = timer.TotalSeconds <= data.InActive;
        var cd = timer.TotalSeconds >= data.InActive + Math.Max(data.CoolTimer, 1.0);

        if (data.Track)
        {
            if (range > data.TrackRange * 16f) // 超距离追击
            {
                var speed = dict * data.MaxSpeed + tar.Velocity;
                if (speed.Length() > data.MaxSpeed)
                {
                    speed.Normalize();
                    speed *= data.MaxSpeed;
                }
                npc.velocity = speed;

                if (data.AutoTarget)//自动转换仇恨目标
                {
                    npc.netSpam = 0;
                    npc.TargetClosest(true);
                    npc.spriteDirection = npc.direction = Terraria.Utils.ToDirectionInt(npc.velocity.X > 0f);
                }

                npc.netUpdate = true;
                return;
            }
            else if (range < data.Range) // 在最小距离内停止
            {
                npc.netUpdate = false;
                return;
            }
        }

        if (cd || active)
        {
            if (cd)
            {
                cdCount++;
                updateTimer = now;
                UpdateTrack(name, cdCount, updateTimer); // 更新内存中的计数器和时间
            }

            //距离加速事件
            if (range >= data.Range * 16f && range <= data.MaxRange * 16f && active)
            {
                var speed = dict * data.Speed;
                if (speed.Length() > data.Speed)
                {
                    speed.Normalize();
                    speed *= data.Speed;
                }

                //自动仇恨
                if (data.AutoTarget)
                {
                    npc.spriteDirection = npc.direction;
                    npc.rotation = (npc.rotation * 9f + npc.velocity.X * 0.025f) / 10f;
                }

                npc.velocity = speed;

            }
            else if (Config.HealEffect && active) //不在触发范围且在触发时间内 开启回血
            {
                npc.life = Math.Min(npc.lifeMax, npc.life + 1);
            }

            //时间事件
            if (data.TimerEvent != null && data.TimerEvent.Count > 0)
            {
                var order = (cdCount - 1) % data.TimerEvent.Count;
                var cycle = data.TimerEvent.FirstOrDefault(c => c.Order == order + 1);
                if (cycle != null)
                {
                    //AI赋值
                    AIPairs(cycle.AIPairs, npc);

                    //召唤怪物
                    if (cycle.SpawnMonster != null && cycle.SpawnMonster.Count > 0)
                    {
                        foreach (var npcid in cycle.SpawnMonster)
                        {
                            var count = Main.npc.Count(p => p.active && p.type == npcid);
                            if (count >= cycle.NpcCount) continue;

                            var nPCById = TShock.Utils.GetNPCById(npcid);
                            if (nPCById != null && nPCById.type != 113 &&
                                nPCById.type != 0 && nPCById.type < Terraria.ID.NPCID.Count)
                            {
                                TSPlayer.Server.SpawnNPC(nPCById.type, nPCById.FullName, 1,
                                    Terraria.Utils.ToTileCoordinates(npc.Center).X,
                                    Terraria.Utils.ToTileCoordinates(npc.Center).Y, 15, 15);

                                count++;
                            }
                        }
                    }

                    //生成弹幕
                    if (cycle.SendProj != null && cycle.SendProj.Count > 0)
                    {
                        MyProjectile.SpawnProjectile(cycle.SendProj, npc);
                    }

                    //监控
                    var AiInfo = AIPairsInfo(cycle.AIPairs);
                    mess.Append($" 顺序:[c/A2E4DB:{order + 1}/{data.TimerEvent.Count}] 赋值:[c/A2E4DB:{AiInfo}]\n");
                }
            }

            // 血量事件
            var life = (int)(npc.life / (float)npc.lifeMax * 100);
            if (data.LifeEvent != null && data.LifeEvent.Count > 0)
            {
                foreach (var heal in data.LifeEvent)
                {
                    if (life > heal.MaxLife || life < heal.MinLife)
                    {
                        continue;
                    }

                    //AI赋值
                    AIPairs(heal.AIPairs, npc);

                    //自动转换仇恨目标
                    if (data.AutoTarget)
                    {
                        npc.TargetClosest(true);
                    }

                    //召唤怪物
                    if (heal.SpawnMonster != null && heal.SpawnMonster.Count > 0)
                    {
                        foreach (var npcid in heal.SpawnMonster)
                        {
                            var count = Main.npc.Count(p => p.active && p.type == npcid);
                            if (count >= heal.NpcStack) continue;

                            var nPCById = TShock.Utils.GetNPCById(npcid);
                            if (nPCById != null && nPCById.type != 113 &&
                                nPCById.type != 0 && nPCById.type < Terraria.ID.NPCID.Count)
                            {
                                TSPlayer.Server.SpawnNPC(nPCById.type, nPCById.FullName, 1,
                                    Terraria.Utils.ToTileCoordinates(npc.Center).X,
                                    Terraria.Utils.ToTileCoordinates(npc.Center).Y, 15, 15);
                                count++;
                            }
                        }
                    }

                    //生成弹幕
                    if (heal.SendProj != null && heal.SendProj.Count > 0)
                    {
                        MyProjectile.SpawnProjectile(heal.SendProj, npc);
                    }

                    //监控
                    var AiInfo = AIPairsInfo(heal.AIPairs);
                    mess.Append($" 血量:[c/A2E4DB:{life}%] 赋值:[c/A2E4DB:{AiInfo}]\n");
                    break;
                }
            }

            npc.netUpdate = true;
        }
        else
        {
            npc.netUpdate = false;
        }

        //监控
        mess.Append($" [c/3A89D0:{name}] 【x】[c/38E06D:{npc.velocity.X:F0}] " +
            $"【y】[c/A5CEBB:{npc.velocity.Y:F0}] 【style】[c/3A89D0:{npc.aiStyle}]\n");

        mess.Append($" [ai0] [c/F3A144:{npc.ai[0]:F0}] [ai1] [c/D2A5DF:{npc.ai[1]:F0}] " +
            $"[ai2] [c/EBEB91:{npc.ai[2]:F0}] [ai3] [c/35E635:{npc.ai[3]:F0}]\n");

        var remaining = TimeSpan.FromSeconds(data.InActive) - timer;
        var color = remaining.TotalSeconds >= 0 ? "38E06D" : "E73A79"; // 触发时间为绿色 冷却时间为红色

        mess.Append($" 时间:[c/{color}:{remaining.TotalSeconds:F2}s]\n");

        //监控广播
        if (Config.Monitor &&
            (now - BroadcastTime).TotalMilliseconds >= Config.Monitorinterval)
        {
            TSPlayer.All.SendMessage($"{mess}", 170, 170, 170);
            BroadcastTime = now;
        }
    }
    #endregion

    #region 怪物AI赋值
    private void AIPairs(Dictionary<int, float> Pairs, NPC npc)
    {
        if (Pairs == null || Pairs.Count == 0) return;

        foreach (var Pair in Pairs)
        {
            var i = Pair.Key;

            if (i >= 0 && i < npc.ai.Length)
            {
                npc.ai[i] = Pair.Value;
            }
        }
    }
    #endregion

    #region 输出正在赋值的AI信息
    private string AIPairsInfo(Dictionary<int, float> Pairs)
    {
        if (Pairs == null || Pairs.Count == 0) return "无";
        var info = new StringBuilder();
        foreach (var Pair in Pairs)
        {
            info.Append($"ai{Pair.Key}_{Pair.Value:F0} ");
        }

        return info.ToString();
    }
    #endregion


}