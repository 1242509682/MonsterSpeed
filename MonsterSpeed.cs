using System.Reflection;
using System.Text;
using AutoCompile;
using Microsoft.Xna.Framework;
using Terraria;
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
    public override Version Version => new Version(1, 3, 7, 2);
    public override string Description => "使boss拥有高速追击能力，并支持修改其弹幕、随从、Ai、防御等功能";
    #endregion

    #region 注册与释放   
    public MonsterSpeed(Main game) : base(game)
    {
        UpProj.UpdateState = new UpdateProjState[1001];
    }

    public override void Initialize()
    {
        if (!Directory.Exists(Paths))
        {
            Directory.CreateDirectory(Paths);
            ExtractData();
        }

        LoadConfig();
        GeneralHooks.ReloadEvent += ReloadConfig;
        GetDataHandlers.KillMe += KillMe!;
        ServerApi.Hooks.NpcKilled.Register(this, this.OnNPCKilled);
        ServerApi.Hooks.NpcStrike.Register(this, this.OnNpcStrike);
        ServerApi.Hooks.NpcAIUpdate.Register(this, this.OnNpcAiUpdate);
        TShockAPI.Commands.ChatCommands.Add(new TShockAPI.Command("mos.admin", Command.CMD, "怪物加速", "mos"));
        ServerApi.Hooks.GamePostInitialize.Register(this, this.GamePost);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GeneralHooks.ReloadEvent -= ReloadConfig;
            GetDataHandlers.KillMe -= KillMe!;
            ServerApi.Hooks.NpcKilled.Deregister(this, this.OnNPCKilled);
            ServerApi.Hooks.NpcStrike.Deregister(this, this.OnNpcStrike);
            ServerApi.Hooks.NpcAIUpdate.Deregister(this, this.OnNpcAiUpdate);
            ServerApi.Hooks.GamePostInitialize.Deregister(this, this.GamePost);
            TShockAPI.Commands.ChatCommands.RemoveAll(x => x.CommandDelegate == Command.CMD);
        }
        base.Dispose(disposing);
    }
    #endregion

    #region 内嵌依赖项
    private void ExtractData()
    {
        var files = new List<string>
        {
            "AutoCompile.dll",
        };

        foreach (var file in files)
        {
            var asm = Assembly.GetExecutingAssembly();
            var res = $"{asm.GetName().Name}.依赖项.{file}";

            using (var stream = asm.GetManifestResourceStream(res))
            {
                if (stream == null) continue;
                var tshockPath = typeof(TShock).Assembly.Location;
                var USing = Path.Combine(tshockPath, "ServerPlugins");
                var tarPath = Path.Combine(USing, file);

                if (File.Exists(tarPath)) continue;

                using (var fs = new FileStream(tarPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096))
                {
                    stream.CopyTo(fs);
                }
            }
        }
    }
    #endregion

    #region 加载完世界后事件
    private void GamePost(EventArgs args)
    {
        ConditionFile.Init(); // 新增：初始化触发条件文件系统
        SpawnProjFile.Init(); // 新增：初始化弹幕文件系统
        UpProjFile.Init(); // 新增：初始化更新弹幕文件系统
        MoveFile.Init(); // 新增：初始化移动模式文件系统

        if (Directory.Exists(Path.Combine(TShock.SavePath, "自动编译", "程序集")))
        {
            CSExecutor.Init(); // 新增：初始化异步执行器  
            CSExecutor.CopyMosDll(); // 复制自己到自动编译《程序集文件夹》方便脚本能正确引用

            // 启动服务器自动批量编译
            if (Config.ScriptCfg?.Int == true &&
                Config.ScriptCfg?.Enabled == true)
            {
                try
                {
                    TShock.Log.ConsoleInfo($"[怪物加速] 开始初始化编译脚本...");

                    var result = CSExecutor.BatchCompile();
                    if (result.Ok)
                    {
                        TShock.Log.ConsoleInfo($"[怪物加速] {result.Msg}");
                    }
                    else
                    {
                        TShock.Log.ConsoleWarn($"[怪物加速] 初始化编译有错误: {result.Msg}");
                    }
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError($"[怪物加速] 初始化编译异常: {ex.Message}");
                }
                finally
                {
                    // 编译结束 清理程序集引用 避免一直占用内存
                    Compiler.ClearMetaRefs();
                    GC.Collect();
                }
            }
        }
        else
        {
            Console.WriteLine(" ----------------------------------------------------------------");
            TShock.Log.ConsoleError($"[怪物加速] 请重启服务器 否则《C#脚本》无法使用！！！");
            TShock.Log.ConsoleWarn($"[怪物加速] 请重启服务器 否则《C#脚本》无法使用！！！");
            TShock.Log.ConsoleError($"[怪物加速] 请重启服务器 否则《C#脚本》无法使用！！！");
            TShock.Log.ConsoleWarn($"[怪物加速] 请重启服务器 否则《C#脚本》无法使用！！！");
            TShock.Log.ConsoleError($"[怪物加速] 请重启服务器 否则《C#脚本》无法使用！！！");
            TShock.Utils.Broadcast($"已释放依赖项: AutoCompile.dll —— 自动编译插件", 80, 142, 200);
            TShock.Utils.Broadcast($"确保编译插件正常工作请重启服务器!", 80, 200, 180);
            Console.WriteLine(" ----------------------------------------------------------------");
        }
    }
    #endregion

    #region 配置重载读取与写入方法
    internal static Configuration Config = new();
    private static void ReloadConfig(ReloadEventArgs args = null!)
    {
        LoadConfig();
        SpawnProjFile.Reload();
        ConditionFile.Reload();
        UpProjFile.Reload();
        MoveFile.Reload();

        // 重载脚本执行器并预编译
        var result = CSExecutor.Reload();
        if (result.Ok)
        {
            args.Player.SendInfoMessage("[怪物加速] 重新加载配置完毕。");
        }
        else
        {
            args.Player.SendErrorMessage($"[怪物加速] 重载失败: {result.Msg}");
        }
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
            !npc.active || npc.friendly ||
             npc.netID == 488 ||
            !Config.NpcList.Contains(npc.netID) ||
             Config.NpcDatas is null)
        {
            return;
        }

        // 修改：检查是否已存在该怪物的配置
        bool hasConfig = Config.NpcDatas.Any(npcData => npcData.Type.Contains(npc.netID));

        if (!hasConfig)
        {
            NpcData nd = NewData(npc.FullName);
            nd.Type = new List<int> { npc.netID }; // 设置怪物ID
            Config.NpcDatas.Add(nd);
            Config.Write();

            var s = StateApi.GetState(npc);
            s.EventIndex = 0;
            s.SendProjIdx = 0;
            s.Struck = 0;
            s.KillPlay = 0;
            s.ActiveTime = 0;
            s.CooldownTime = new Dictionary<int, DateTime>();
            s.LastTextTime = DateTime.UtcNow;
            s.MoveState = new MoveModeState();
            s.EventCounts = new Dictionary<int, int>();
            s.PlayCounts = new Dictionary<string, int>();
        }
        else if (StateApi.NpcStates.ContainsKey(npc.whoAmI))
        {
            StateApi.GetState(npc).Struck++;
        }
    }
    #endregion

    #region 创建新数据
    internal static NpcData NewData(string name = null)
    {
        var newData = new Configuration.NpcData()
        {
            DeadCount = 0,
            AutoTarget = true,
            TrackSpeed = 35,
            TrackRange = 62,
            TrackStopRange = 25,
            TextInterval = 1000f,
            FilePlay = new List<FilePlayData>()
            {
                new FilePlayData()
                {
                    Name = name + "的事件",
                    Flag = name,
                    Cond = "默认配置"

                }
            },
            TimerEvent = new List<TimerData>()
            {
                new TimerData()
                {
                    CoolTime = 5,
                    Condition = "默认配置"
                }
            },
        };

        return newData;
    }
    #endregion

    #region 怪物死亡更新数据方法
    private void OnNPCKilled(NpcKilledEventArgs args)
    {
        var npc = args.npc;
        if (!Config.Enabled || npc is null ||
            !Config.NpcList.Contains(npc.netID))
        {
            return;
        }

        StateApi.ClearState(npc); // 清理指定npc所有状态
        UpProj.ClearStates(npc.whoAmI); // 清理弹幕更新状态
        Teleport.Remove(npc.whoAmI);    // 清理传送和回血记录
        HealTimes.Remove(npc.whoAmI);

        // 修改：更新配置数据 - 查找对应的NpcData
        var data = Config.NpcDatas!.FirstOrDefault(d => d.Type.Contains(npc.netID));
        if (data != null)
        {
            data.DeadCount += 1;
            Config.Write();
        }
    }
    #endregion

    #region 击杀玩家事件
    private void KillMe(object? sender, GetDataHandlers.KillMeEventArgs e)
    {
        var plr = e.Player;
        if (e.Handled || plr == null || e.Pvp) return;

        e.PlayerDeathReason.TryGetCausingEntity(out var entity);

        var whoAmI = entity?.whoAmI ?? -1;

        if (entity is NPC npc)
        {
            if (npc is null || !npc.active || !Config.NpcList.Contains(npc.netID))
            {
                return;
            }

            var state = StateApi.GetState(npc);
            if (state is not null)
            {
                state.KillPlay++;
            }
        }
    }
    #endregion

    #region 怪物加速核心方法
    private static DateTime BroadcastTime = DateTime.UtcNow; // 跟踪最后一次广播时间
    private static long Timer = 0; // 计数器
    private void OnNpcAiUpdate(NpcAiUpdateEventArgs args)
    {
        var mess = new StringBuilder(); //用于存储广播内容
        var npc = args.Npc;
        var data = Config.NpcDatas!.FirstOrDefault(npcData => npcData.Type.Contains(npc.netID));

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

        #region 怪物活跃秒数统计
        Timer++;
        if (Timer >= 60)
        {
            StateApi.GetState(npc).ActiveTime++;
            Timer = 0;
        }
        #endregion

        var handled = false;
        TimerEvents.TimerEvent(npc, mess, data, ref handled); //时间事件
        FilePlayManager.HandleAll(npc, mess, data, StateApi.GetState(npc), ref handled); // 执行文件（并行处理）

        TrackMode(npc, data); //超距离追击
        npc.netUpdate = true;
        Broadcast(mess, npc, data); //监控广播
        args.Handled = handled;
    }
    #endregion

    #region 修复：超距离追击模式（解决传送到0,0问题）
    private Dictionary<int, DateTime> Teleport = new Dictionary<int, DateTime>();
    private void TrackMode(NPC npc, NpcData data)
    {
        if (data == null || !data.AutoTrack) return;

        // 修复1：检查目标有效性
        if (!IsValidTarget(npc, data))
        {
            // 尝试寻找有效目标
            SafeAutoTarget(npc, data);
            // 如果仍然没有有效目标，直接返回
            if (!IsValidTarget(npc, data))
                return;
        }

        var plr = Main.player[npc.target];
        var dict = plr.Center - npc.Center;
        var range = Vector2.Distance(plr.Center, npc.Center);

        if (data.TrackRange != 0)
        {
            // 修复2：使用PxUtil进行距离转换
            float TrackRange = PxUtil.ToPx(data.TrackRange);
            float TrackStopRange = PxUtil.ToPx(data.TrackStopRange);
            float SmoothRange = TrackStopRange + (TrackRange - TrackStopRange) * 0.5f;

            if (range > TrackRange)
            {
                // 动态速度调整
                float dicRatio = (range - TrackRange) / TrackRange;
                float dySpeed = data.TrackSpeed * (1f + Math.Min(dicRatio, 1f) * 0.5f);

                Vector2 speedMax = dict * dySpeed + plr.velocity * 0.3f;
                if (speedMax.Length() > dySpeed)
                {
                    speedMax.Normalize();
                    speedMax *= dySpeed;
                }

                npc.velocity = Vector2.Lerp(npc.velocity, speedMax, 0.6f);

                // 智能目标切换
                if (range > TrackRange * 1.5f)
                {
                    SafeAutoTarget(npc, data);
                }

                // 修复3：安全的传送逻辑
                if (data.Teleport > 0)
                {
                    SafeTeleport(npc, data, plr, range, TrackRange);
                }
            }
            else if (range > TrackStopRange && range <= SmoothRange)
            {
                npc.velocity *= 0.95f;
            }
            else if (range < TrackStopRange)
            {
                return;
            }
        }
    }

    // 修复：目标有效性检查
    private bool IsValidTarget(NPC npc, NpcData data)
    {
        if (npc.target < 0 || npc.target >= Main.maxPlayers)
            return false;

        var plr = Main.player[npc.target];
        return PxUtil.IsValidPlr(plr) && plr.Center != Vector2.Zero;
    }

    // 修复：安全的自动目标
    private void SafeAutoTarget(NPC npc, NpcData data)
    {
        if (data.AutoTarget)
        {
            float SpeedX = npc.velocity.X;

            // 使用PxUtil寻找最近的有效玩家
            Player plr2 = new Player();
            float Distance = float.MaxValue;
            float maxDistance = PxUtil.ToPx(data.TrackRange * 3f);

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                var plr = Main.player[i];
                if (plr is null || !plr.active || plr.dead ||
                    !PxUtil.IsValidPlr(plr) || plr.Center == Vector2.Zero)
                    continue;

                float distance = PxUtil.DistanceSquared(npc.Center, plr.Center);
                if (distance < Distance && distance <= maxDistance * maxDistance)
                {
                    Distance = distance;
                    plr2 = plr;
                }
            }

            if (plr2 != null && !plr2.dead && plr2.active)
            {
                npc.target = plr2.whoAmI;
                npc.netSpam = 0;

                // 保持原有的移动方向逻辑
                npc.spriteDirection = npc.direction = Terraria.Utils.ToDirectionInt(npc.velocity.X > 0f);

                if (SpeedX * npc.velocity.X < 0)
                {
                    npc.velocity.X *= 0.7f;
                }
            }
        }
    }

    // 修复：安全的传送方法
    private void SafeTeleport(NPC npc, NpcData data, Player plr, float range, float trackRange)
    {
        // 检查传送冷却
        bool canTp = !Teleport.ContainsKey(npc.whoAmI) ||
                      (DateTime.UtcNow - Teleport[npc.whoAmI]).TotalSeconds >= data.Teleport;

        // 只在足够远的距离传送，避免频繁传送
        if (canTp && range > trackRange * 20f)
        {
            // 使用PxUtil寻找安全位置
            Vector2 safePos = FindSafePos(plr.Center, PxUtil.ToPx(data.TrackStopRange));
            if (safePos != Vector2.Zero && PxUtil.InWorldBounds(safePos))
            {
                npc.Teleport(safePos, 10);
                Teleport[npc.whoAmI] = DateTime.UtcNow;

                // 传送后重新寻找目标
                SafeAutoTarget(npc, data);
            }
        }
    }

    // 查找安全传送位置
    private Vector2 FindSafePos(Vector2 center, float dice)
    {
        // 在目标周围随机寻找安全位置
        for (int i = 0; i < 10; i++) // 增加尝试次数
        {
            // 使用PxUtil生成随机偏移
            Vector2 offset = PxUtil.RandomOffset(dice * 0.8f, dice * 1.2f);
            Vector2 testPos = center + offset;

            // 使用PxUtil检查世界边界和位置有效性
            if (PxUtil.InWorldBounds(testPos) && IsPositionSafe(testPos))
            {
                return testPos;
            }
        }

        // 没找到安全位置就返回一个相对安全的位置（不在0,0）
        Vector2 fallbackPos = center + new Vector2(dice, dice);
        return PxUtil.InWorldBounds(fallbackPos) ? fallbackPos : center;
    }

    // 检查传送位置安全性
    private bool IsPositionSafe(Vector2 pos)
    {
        // 检查是否在固体方块内
        Point tilePos = pos.ToTileCoordinates();
        if (tilePos.X < 0 || tilePos.X >= Main.maxTilesX || tilePos.Y < 0 || tilePos.Y >= Main.maxTilesY)
            return false;

        Tile tile = (Tile)Main.tile[tilePos.X, tilePos.Y];
        return !(tile.active() && Main.tileSolid[tile.type]);
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
            var state = StateApi.GetState(npc);

            // 新增：显示关键指示物
            if (state?.Markers != null && state.Markers.Count > 0)
            {
                mess.Append($" [指示物] ");
                int count = 0;
                foreach (var marker in state.Markers)
                {
                    if (count >= 5) break; // 最多显示5个
                    mess.Append($"{marker.Key}:{marker.Value} ");
                    count++;
                }
                mess.Append("\n");
            }

            string aiInfo = "";
            if (data.TimerEvent != null && data.TimerEvent.Count > 0)
            {
                var idx = state!.EventIndex;
                if (idx >= 0 && idx < data.TimerEvent.Count)
                {
                    var evt = data.TimerEvent[idx];
                    if (evt?.AIMode != null && evt.AIMode.Enabled)
                    {
                        aiInfo = AISystem.GetAiInfo(state.AIState, evt.AIMode, npc.FullName);
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