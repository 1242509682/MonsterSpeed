using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using static Plugin.Configuration;

namespace Plugin;

[ApiVersion(2, 1)]
public class MonsterSpeed : TerrariaPlugin
{
    #region 插件信息
    public override string Name => "怪物加速";
    public override string Author => "羽学";
    public override Version Version => new Version(1, 0, 5);
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
        TShockAPI.Commands.ChatCommands.Add(new Command("mos.admin", global::MonsterSpeed.Command.CMD, "怪物加速", "mos"));
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GeneralHooks.ReloadEvent -= ReloadConfig;
            ServerApi.Hooks.NpcKilled.Deregister(this, this.OnNPCKilled);
            ServerApi.Hooks.NpcStrike.Deregister(this, this.OnNpcStrike);
            ServerApi.Hooks.NpcAIUpdate.Deregister(this, this.OnNpcAiUpdate);
            TShockAPI.Commands.ChatCommands.RemoveAll(x => x.CommandDelegate == global::MonsterSpeed.Command.CMD);
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
        var npcID = args.Npc.netID;
        if (!Config.Enabled || args.Npc == null || !Config.NpcList.Contains(npcID)) return;
        var name = TShock.Utils.GetNPCById(npcID).FullName;
        var NewNpc = !Config.Dict!.ContainsKey(name);
        if (NewNpc)
        {
            var newData = new Configuration.NpcData()
            {
                Count = 0,
                Track = true,
                TrackRange = Config.TrackRange,
                Speed = Config.Speed,
                MaxSpeed = Config.MaxSpeed,
                Range = 10f,
                MaxRange = Config.MaxRange,
                CoolTimer = 5f,
                InActive = 5f,
                MaxActive = Config.MaxActive,
                UpdateTimer = DateTime.UtcNow,
                LifeEvent = new List<LifeData>()
                {
                    new LifeData
                    {
                        MinLife = 0, MaxLife = 50, AiStyle = args.Npc.aiStyle,
                        AIPairs = new Dictionary<int, float>() { }
                    },

                    new LifeData
                    {
                        MinLife = 50, MaxLife = 100, AiStyle = args.Npc.aiStyle,
                        AIPairs = new Dictionary<int, float>() { }
                    },
                },
                TimerEvent = new List<TimerData>()
                {
                    new TimerData
                    {
                        Order = 1,
                        AiStyle = args.Npc.aiStyle,
                        AIPairs = new Dictionary<int, float>() { },
                    },

                    new TimerData
                    {
                        Order = 2,
                        AiStyle = args.Npc.aiStyle,
                        AIPairs = new Dictionary<int, float>() { }
                    },
                }
            };
            Config.Dict[name] = newData;
        }

        Config.Write();
    }
    #endregion

    #region 怪物死亡更新数据方法
    private void OnNPCKilled(NpcKilledEventArgs args)
    {
        var npcID = args.npc.netID;
        if (!Config.Enabled || args.npc == null || !Config.NpcList.Contains(npcID)) return;
        var name = TShock.Utils.GetNPCById(npcID).FullName;
        if (Config.Dict!.TryGetValue(name, out var data) && data != null)
        {
            if (data.TimerEvent != null &&
                data.TimerEvent.Count > 0 &&
                data.CDCount > data.TimerEvent.Count)
            {
                data.CDCount = data.TimerEvent.Count;
            }
            else
            {
                data.CDCount = 2;
            }
            data.Count += 1;
            data.Speed = Math.Min(data.MaxSpeed, data.Speed + Config.Killed);
            data.CoolTimer = Math.Max(1, data.CoolTimer - Config.Ratio);
            data.InActive = Math.Min(data.MaxActive, data.InActive + Config.Ratio);
            data.Range = Math.Max(1, data.Range - 1);
        }

        Config.Write();
    }
    #endregion

    #region 怪物加速核心方法
    private static DateTime BroadcastTime = DateTime.UtcNow; // 跟踪最后一次广播时间
    private void OnNpcAiUpdate(NpcAiUpdateEventArgs args)
    {
        var npc = args.Npc;
        var now = DateTime.UtcNow;
        var name = TShock.Utils.GetNPCById(npc.type)?.FullName ?? "未知NPC";
        var plr = TShock.Players.FirstOrDefault(p => p != null && p.IsLoggedIn && p.Active);
        Config.Dict!.TryGetValue(name, out var data);
        var mess = new StringBuilder();
        if (npc == null || data == null || !Config.Enabled || plr == null ||
            !npc.active || npc.townNPC || npc.SpawnedFromStatue || npc.netID == 488)
        {
            return;
        }

        if (!Config.NpcList.Contains(npc.type)) return;

        //获取玩家与怪物的距离和相对位置向量
        var (dict, range) = GetPlyrRange(npc, plr.TPlayer);
        if (data.Track)
        {
            if (range > data.TrackRange * 16f) // 超距离追击
            {
                var speed = dict * data.MaxSpeed + plr.TPlayer.velocity;
                if (speed.Length() > data.MaxSpeed)
                {
                    speed.Normalize();
                    speed *= data.MaxSpeed;
                }
                npc.velocity = speed;
                npc.netUpdate = true;
                return;
            }
            else if (range < data.Range) // 在最小距离内停止
            {
                npc.netUpdate = false;
                return;
            }
        }

        //时间条件
        var timer = now - data.UpdateTimer;
        var active = timer.TotalSeconds <= data.InActive;
        var cd = timer.TotalSeconds >= data.InActive + Math.Max(data.CoolTimer, 1.0);
        if (cd || active)
        {
            if (cd)
            {
                data.CDCount++;
                data.UpdateTimer = now;
                Config.Write();
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
                npc.velocity = speed;
                npc.netUpdate = true;
            }
            else if (Config.HealEffect && active) //不在触发范围且在触发时间内 开启回血
            {
                npc.life = Math.Min(npc.lifeMax, npc.life + 1);
                npc.netUpdate = true;
            }

            //时间事件
            if (data.TimerEvent != null && data.TimerEvent.Count > 0)
            {
                var order = (data.CDCount - 1) % data.TimerEvent.Count;
                var cycle = data.TimerEvent.FirstOrDefault(c => c.Order == order + 1);
                if (cycle != null)
                {
                    if (cycle.AiStyle != -1 && npc.aiStyle != cycle.AiStyle)
                    {
                        npc.aiStyle = cycle.AiStyle;
                    }
                    AIPairs(cycle.AIPairs, npc);

                    var AiInfo = AIPairsInfo(cycle.AIPairs);
                    mess.Append($" 顺序:[c/A2E4DB:{order + 1}/{data.TimerEvent.Count}] 赋值:[c/A2E4DB:{AiInfo}]\n");
                }
            }

            // 血量事件
            var life = (int)(npc.life / (float)npc.lifeMax * 100);
            if (data.LifeEvent != null && data.LifeEvent.Count > 0) // 基于血量的静态AI赋值
            {
                foreach (var healAI in data.LifeEvent)
                {
                    if (life > healAI.MaxLife || life < healAI.MinLife)
                    {
                        continue;
                    }

                    if (healAI.AiStyle != -1 && npc.aiStyle != healAI.AiStyle)
                    {
                        npc.aiStyle = healAI.AiStyle;
                    }

                    AIPairs(healAI.AIPairs, npc);

                    var AiInfo = AIPairsInfo(healAI.AIPairs);
                    mess.Append($" 血量:[c/A2E4DB:{life}%] 赋值:[c/A2E4DB:{AiInfo}]\n");
                    break;
                }
            }

        }
        else
        {
            npc.netUpdate = false;
        }

        mess.Append($" [c/3A89D0:{name}] 【x】[c/38E06D:{npc.velocity.X:F0}] " +
            $"【y】[c/A5CEBB:{npc.velocity.Y:F0}] 【style】[c/3A89D0:{npc.aiStyle}]\n");
        mess.Append($" [ai0] [c/F3A144:{npc.ai[0]:F0}] [ai1] [c/D2A5DF:{npc.ai[1]:F0}] " +
            $"[ai2] [c/EBEB91:{npc.ai[2]:F0}] [ai3] [c/35E635:{npc.ai[3]:F0}]\n");
        var CDTimer = now - data.UpdateTimer;
        var remaining = TimeSpan.FromSeconds(data.InActive) - CDTimer;
        var color = remaining.TotalSeconds >= 0 ? "38E06D" : "E73A79"; // 触发时间为绿色 冷却时间为红色
        mess.Append($" 时间:[c/{color}:{remaining.TotalSeconds:F2}s]\n");

        //监控广播
        if (Config.Monitor && plr.HasPermission("mos.admin") &&
            (now - BroadcastTime).TotalMilliseconds >= Config.Monitorinterval)
        {
            plr.SendMessage($"{mess}", 170, 170, 170);
            BroadcastTime = now;
        }
    }
    #endregion

    #region AI赋值
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

    #region 获取玩家与怪物的距离
    private (Vector2 dict, float range) GetPlyrRange(NPC npc, Player plr)
    {
        if (plr == null) return (Vector2.Zero, float.MaxValue);
        var plrCenter = new Vector2(plr.position.X + (plr.width / 2), plr.position.Y + (plr.height / 2));
        var npcCenter = new Vector2(npc.position.X + (npc.width / 2), npc.position.Y + (npc.height / 2));
        var dict = plrCenter - npcCenter;
        var range = dict.Length();
        return (dict, range);
    }
    #endregion

}