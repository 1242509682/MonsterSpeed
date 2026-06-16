using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Enums;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using static MonsterSpeed.Configuration;
using static MonsterSpeed.Utils;
using static MonsterSpeed.PxUtil;

namespace MonsterSpeed;

[ApiVersion(2, 1)]
public class MonsterSpeed : TerrariaPlugin
{
    #region 插件信息
    public override string Name => "怪物加速";
    public static readonly string LogName = "[怪物加速]";
    public override string Author => "羽学";
    public override Version Version => new Version(1, 3, 8, 4);
    public override string Description => "使boss拥有高速追击能力，并支持修改其弹幕、随从、Ai、防御等功能";
    #endregion

    #region 注册与释放   
    public MonsterSpeed(Main game) : base(game)
    {
        UpProj.UpdateState = new UpdProjState[1001];
    }

    public override void Initialize()
    {
        if (!Directory.Exists(Paths))
        {
            Directory.CreateDirectory(Paths);
        }

        LoadConfig();
        GeneralHooks.ReloadEvent += ReloadConfig;
        GetDataHandlers.KillMe += KillMe!;
        ServerApi.Hooks.NpcSpawn.Register(this, OnNpcSpawn);
        ServerApi.Hooks.NpcKilled.Register(this, this.OnNPCKilled);
        ServerApi.Hooks.NpcStrike.Register(this, this.OnNpcStrike);
        ServerApi.Hooks.NpcAIUpdate.Register(this, this.OnNpcAiUpdate);
        On.Terraria.Projectile.AI += OnProjAI;
        On.Terraria.Projectile.Kill += OnProjKill;
        On.Terraria.Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float_NewProjectileModifier += OnNewProj;
        Commands.ChatCommands.Add(new Command("mos.admin", MyCmd.CMD, "怪物加速", "mos"));
        ServerApi.Hooks.GamePostInitialize.Register(this, this.GamePost);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CSExecutor.Clear(); // 清理脚本执行器
            GeneralHooks.ReloadEvent -= ReloadConfig;
            GetDataHandlers.KillMe -= KillMe!;
            ServerApi.Hooks.NpcSpawn.Deregister(this, OnNpcSpawn);
            ServerApi.Hooks.NpcKilled.Deregister(this, this.OnNPCKilled);
            ServerApi.Hooks.NpcStrike.Deregister(this, this.OnNpcStrike);
            ServerApi.Hooks.NpcAIUpdate.Deregister(this, this.OnNpcAiUpdate);
            ServerApi.Hooks.GamePostInitialize.Deregister(this, this.GamePost);
            On.Terraria.Projectile.AI -= OnProjAI;
            On.Terraria.Projectile.Kill -= OnProjKill;
            On.Terraria.Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float_NewProjectileModifier -= OnNewProj;
            Commands.ChatCommands.RemoveAll(x => x.CommandDelegate == MyCmd.CMD);
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
        EvtFile.Init();   // 新增: 初始化时间事件文件夹

        // 决定初始化脚本执行器或释放内嵌文件
        if (ServerApi.Plugins.Any(p => p.Plugin.Name == "自动编译插件"))
        {
            CSExecutor.Register();
        }
        else
        {
            ExtractData(); // 释放内嵌依赖项

            Console.WriteLine($"\n---------------------------{LogName}---------------------------");
            TShock.Log.ConsoleError($"\n          缺失依赖项: AutoCompile.dll —— 自动编译插件", 80, 142, 200);
            TShock.Log.ConsoleInfo($"       请放入依赖项后重启服务器!确保C#脚本执行能正常工作!\n", 80, 200, 180);
            Console.WriteLine("----------------------------------------------------------------\n");
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
            args.Player.SendInfoMessage($"{LogName} 重新加载配置完毕。");
        }
        else
        {
            args.Player.SendErrorMessage($"{LogName} 重载失败: {result.Msg}");
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
            !npc.active || npc.friendly || npc.catchItem != 0 ||
             npc.SpawnedFromStatue || npc.netID == NPCID.TargetDummy ||
             Config.NpcDatas is null || !Config.NpcList.Contains(npc.type))
        {
            return;
        }

        // 检查是否已存在该怪物的配置
        bool hasConfig = Config.NpcDatas.Any(npcData => npcData.Type.Contains(npc.netID));

        if (!hasConfig)
        {
            NpcData nd = NewData(npc.FullName);
            nd.Type = new List<int> { npc.netID }; // 设置怪物ID
            Config.NpcDatas.Add(nd);
            Config.Write();

            var st = StateApi.GetState(npc);
            st.EventIndex = 0;
            st.SendProjIdx = 0;
            st.Struck = 0;
            st.KillPlay = 0;
            st.ActiveTime = 0;
            st.Cooldown = new Dictionary<int, DateTime>();
            st.LastTextTime = DateTime.UtcNow;
            st.MoveState = new MoveState();
            st.EventCounts = new Dictionary<int, int>();
            st.PlayCounts = new Dictionary<string, int>();
        }
        else if (StateApi.NpcStates.ContainsKey(npc.whoAmI))
        {
            var st = StateApi.GetState(npc);
            st.Struck++;

            // 记录所有攻击者
            var plr = args.Player;
            if (plr != null && plr.active && !plr.dead && !st.Attack.Contains(plr.whoAmI))
                st.Attack.Add(plr.whoAmI);
        }
    }
    #endregion

    #region 创建新数据
    internal static NpcData NewData(string? name = null)
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
                    CD = 5,
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
        if (!Config.Enabled || npc is null || npc.catchItem != 0 ||
            npc.SpawnedFromStatue || npc.friendly || npc.type == NPCID.TargetDummy)
        {
            return;
        }

        // 获取配置数据
        var data = Config.NpcDatas!.FirstOrDefault(d => d.Type.Contains(npc.netID));
        if (data != null)
        {
            var st = StateApi.GetState(npc);

            // 执行死亡事件
            if (data.DeadEvt != null && data.DeadEvt.Count > 0)
            {
                var all = new List<TimerData>();
                foreach (string evtName in data.DeadEvt)
                    all.AddRange(EvtFile.Load(evtName));

                bool handled = false;
                TimerEvents.Process(
                    npc, data, all,
                    ref st.DeadIdx, st.DeadCD, ref st.DeadLastText,
                    data.TextInterval, data.TextGradient, data.TextRange,
                    ref handled, showText: false
                );
            }

            data.DeadCount += 1;
            st.ActiveTime = 0;
            st.LastPlrCnt = -1;
            st.DefLifeMax = 0;
            Config.Write();
        }

        DelProjMap(npc.whoAmI);   // 清理属于该 NPC 的弹幕映射
        StateApi.ClearState(npc); // 清理指定npc所有状态
        UpProj.ClearStates(npc.whoAmI); // 清理弹幕更新状态
        TpTime.Remove(npc.whoAmI);    // 清理传送和回血记录
        HealTimes.Remove(npc.whoAmI); // 移除自动回血
        NpcMap.Remove(npc.whoAmI); // 移除缓存NPC
    }
    #endregion

    #region 击杀玩家事件
    private void KillMe(object? sender, GetDataHandlers.KillMeEventArgs e)
    {
        if (!Config.Enabled) return;

        var plr = e.Player;
        if (e.Handled || plr == null || e.Pvp) return;

        e.PlayerDeathReason.TryGetCausingEntity(out var entity);

        NPC? npc = null;
        if (entity is NPC direct)
            npc = direct;
        else if (entity is Projectile proj)
        {
            if (ProjMap.TryGetValue(proj.whoAmI, out int npcIdx) && npcIdx >= 0 && npcIdx < Main.maxNPCs)
                npc = Main.npc[npcIdx];
        }

        if (npc != null && npc.active && !npc.SpawnedFromStatue && !npc.friendly)
        {
            var state = StateApi.GetState(npc);
            state.KillPlay++;
        }
    }
    #endregion

    #region 弹幕映射（用于死亡归因）
    public static ConcurrentDictionary<int, int> ProjMap = new(); // 弹幕索引 → 发射者NPC索引
    private void DelProjMap(int npcIdx)
    {
        var del = ProjMap.Where(kv => kv.Value == npcIdx).Select(kv => kv.Key).ToList();
        foreach (int pid in del)
            ProjMap.TryRemove(pid, out _);
    }
    private int OnNewProj(On.Terraria.Projectile.orig_NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float_NewProjectileModifier orig,
        IEntitySource src, float x, float y, float spx, float spy,
        int type, int dmg, float kb, int owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)
    {
        int idx = orig(src, x, y, spx, spy, type, dmg, kb, owner, ai0, ai1, ai2, modifer);
        Projectile p = Main.projectile[idx];

        if (!Config.Enabled || !p.active) return idx;

        // 发射源是NPC实体时记录映射
        if (src is EntitySource_Parent par && par.Entity is NPC npc && npc.active)
        {
            if (npc.SpawnedFromStatue || npc.type == NPCID.TargetDummy ||
               npc.townNPC || npc.friendly || npc.catchItem != 0) return idx;

            ProjMap[idx] = npc.whoAmI;

            // 将友好弹幕转换成敌对弹幕 通过弹幕AI发送伤害
            var data = Config.NpcDatas?.FirstOrDefault(d => d.Type.Contains(npc.netID));
            if (data == null || data.TimerEvent == null || data.TimerEvent.Count <= 0) return idx;

            var st = StateApi.GetState(npc);
            if (st is null) return idx;

            var Event = data.TimerEvent[st.EventIndex];
            if (Event.SendProj != null && Event.SendProj.Count > 0)
            {
                foreach (var fileName in Event.SendProj)
                {
                    if (string.IsNullOrEmpty(fileName)) continue;
                    var file = SpawnProjFile.GetData(fileName);
                    if (file != null && file.Count > 0)
                    {
                        if (file == null || file.Count == 0 || st.SendProjIdx >= file.Count) continue;
                        var pd = file[st.SendProjIdx];
                        if (pd.Stack <= 0 || pd.Type != type) continue;

                        dmg = pd.Damage;
                        kb = pd.KnockBack;
                        p.hostile = true;
                        p.friendly = false;
                    }
                }
            }
        }

        return idx;
    }

    private void OnProjKill(On.Terraria.Projectile.orig_Kill orig, Projectile proj)
    {
        // 清理弹幕→发射者映射
        ProjMap.TryRemove(proj.whoAmI, out _);
        // 清理弹幕更新状态（包括 UpdateState 和 UpdateTimer）
        UpProj.Remove(proj.whoAmI);

        orig(proj);
    }

    private void OnProjAI(On.Terraria.Projectile.orig_AI orig, Projectile proj)
    {
        // 由 NPC 发射的弹幕（通过映射表判断）
        if (ProjMap.ContainsKey(proj.whoAmI) && !proj.active)
            ProjMap.TryRemove(proj.whoAmI, out _);

        orig(proj);

        // 修改后的友好弹幕伤害玩家方法
        if (Config.Enabled)
        {
            if (proj.active && proj.hostile && !proj.friendly && ProjMap.TryGetValue(proj.whoAmI, out var npcIdx))
            {
                var dmg = proj.damage > 0 ? proj.damage : 1;
                var reason = PlayerDeathReason.ByNPC(npcIdx);
                var plr = TShock.Players.FirstOrDefault(p => p != null && p.Active && p.TPlayer.Hitbox.Intersects(proj.Hitbox));

                if (plr != null && plr.Active && !plr.Dead)
                {
                    plr.DamagePlayer(dmg, reason);
                    HitEvent(npcIdx, plr);   // 触发命中事件
                }
            }
        }
    }
    #endregion

    #region 弹幕命中玩家事件
    private void HitEvent(int npcIdx, TSPlayer plr)
    {
        NPC npc = Main.npc[npcIdx];
        if (npc == null || !npc.active) return;

        var data = Config.NpcDatas?.FirstOrDefault(d => d.Type.Contains(npc.netID));
        if (data == null || data.HitEvt == null || data.HitEvt.Count == 0) return;

        var st = StateApi.GetState(npc);
        var all = new List<TimerData>();
        foreach (string evtName in data.HitEvt)
            all.AddRange(EvtFile.Load(evtName));

        bool handled = false;

        TimerEvents.Process(
            npc, data, all,
            ref st.HitIdx, st.HitCD, ref st.HitLastText,
            data.TextInterval, data.TextGradient, data.TextRange,
            ref handled, showText: false   // 命中事件通常不需要显示文本
        );
    }
    #endregion

    #region 怪物加速核心方法
    private static DateTime BroadcastTime = DateTime.UtcNow; // 跟踪最后一次广播时间
    private static long Timer = 0; // 计数器
    private void OnNpcAiUpdate(NpcAiUpdateEventArgs args)
    {
        if (!Config.Enabled) return;

        var npc = args.Npc;
        if (npc == null || !npc.active || npc.catchItem != 0 ||
            npc.friendly || npc.townNPC || npc.SpawnedFromStatue ||
            npc.netID == NPCID.TargetDummy) return;

        var st = StateApi.GetState(npc);
        var data = Config.NpcDatas!.FirstOrDefault(npcData => npcData.Type.Contains(npc.netID));

        int cur = TShock.Utils.GetActivePlayerCount();
        if (cur == 0)
        {
            st.ActiveTime = 0;
            st.LastPlrCnt = -1;
            st.DefLifeMax = 0;
            DelProjMap(npc.whoAmI);   // 清理属于该 NPC 的弹幕映射
            StateApi.ClearState(npc); // 清理指定npc所有状态
            UpProj.ClearStates(npc.whoAmI); // 清理弹幕更新状态
            TpTime.Remove(npc.whoAmI);    // 清理传送和回血记录
            HealTimes.Remove(npc.whoAmI); // 移除自动回血
            NpcMap.Clear(); // 清理附近NPC缓存
            return;
        }

        // 只有无数据表且在排除表中时才跳过全局配置
        bool skip = data == null && Config.IgnoreNpc.Contains(npc.type);

        // 动态血量
        float plrHp = data != null ? data.PlrHp : (skip ? 0f : Config.PlrHp);
        DynLife(npc, st, plrHp, cur);

        // 自动回血：优先数据表，否则统一配置
        double heal = data != null ? data.AutoHeal : (skip ? 0 : Config.AutoHeal);
        int healInt = data != null ? data.HealInt : (skip ? 10 : Config.HealInt);
        if (heal > 0) AutoHeal(npc, heal, healInt);

        // 无数据表则不执行后续事件
        if (data == null) return;

        // 碰撞事件（仅当与目标玩家碰撞时触发）
        CollideEvt(npc, st, data);

        #region 怪物活跃秒数统计
        if (++Timer >= 60)
        {
            st.ActiveTime++;
            Timer = 0;
        }
        #endregion

        var mess = new StringBuilder(); //用于存储广播内容
        var handled = false;
        TimerEvents.TimerEvent(npc, mess, data, ref handled); //时间事件
        FilePlayManager.HandleAll(npc, mess, data, st, ref handled); // 执行文件（并行处理）

        Track(npc, data); //超距离追击
        npc.netUpdate = true;
        Broadcast(mess, npc, data); //监控广播
        args.Handled = handled;
    }
    #endregion

    #region 碰撞事件
    private static void CollideEvt(NPC npc, NpcState st, NpcData data)
    {
        if (data.CollideEvt != null && data.CollideEvt.Count > 0)
        {
            var tar = npc.GetTargetData(false);

            if (!tar.Invalid && tar.Type == NPCTargetType.Player)
            {
                if (npc.Hitbox.Intersects(tar.Hitbox))
                {
                    var all = new List<TimerData>();
                    foreach (string evtName in data.CollideEvt)
                        all.AddRange(EvtFile.Load(evtName));

                    bool collHandled = false;
                    TimerEvents.Process(
                        npc, data, all,
                        ref st.CollideIdx, st.CollideCD, ref st.CollideLastText,
                        data.TextInterval, data.TextGradient, data.TextRange,
                        ref collHandled, showText: false
                    );
                }
            }
        }
    }
    #endregion

    #region 超距离追击模式
    private Dictionary<int, DateTime> TpTime = new Dictionary<int, DateTime>();
    private void Track(NPC npc, NpcData data)
    {
        // 无配置或未开启自动追击则返回
        if (data == null || !data.AutoTrack) return;

        // 获取当前追击目标
        NPCAimedTarget tar = npc.GetTargetData(false);

        // 当前目标无效 尝试重新寻找目标
        if (tar.Invalid || tar.Type == NPCTargetType.None)
            AutoTar(npc, data);

        var diff = tar.Center - npc.Center;     // 目标与NPC的向量差
        var dist = tar.Center.Distance(npc.Center); // 欧氏距离

        if (data.TrackRange != 0)
        {
            // 将配置的格数转换为像素（1格=16像素）
            float trRange = data.TrackRange * 16;       // 追击触发距离(px)
            float tsRange = data.TrackStopRange * 16;   // 停止追击距离(px)
            float smRange = tsRange + (trRange - tsRange) * 0.5f; // 平滑过渡区中点

            if (dist > trRange) // 超出追击范围
            {
                // 动态速度：超出越多速度越快，上限+50%
                float ratio = (dist - trRange) / trRange;
                float dySpd = data.TrackSpeed * (1f + Math.Min(ratio, 1f) * 0.5f);

                // 期望速度 = 朝向玩家的向量 * 动态速度 + 玩家速度的30%作为惯性
                Vector2 aimVel = diff * dySpd + tar.Velocity * 0.3f;
                if (aimVel.Length() > dySpd) // 限制最大速度
                {
                    aimVel.Normalize();
                    aimVel *= dySpd;
                }

                // 平滑移动到期望速度（系数0.6）
                npc.velocity = Vector2.Lerp(npc.velocity, aimVel, 0.6f);

                // 超出距离1.5倍时强制刷新目标（避免跟丢）
                if (dist > trRange * 1.5f)
                    AutoTar(npc, data);

                // 若配置了传送冷却，尝试传送
                if (data.Teleport > 0)
                    SafeTP(npc, data, tar, dist, trRange);
            }
            else if (dist > tsRange && dist <= smRange) // 进入减速区
            {
                npc.velocity *= 0.95f; // 每帧减速5%
            }
            else if (dist < tsRange) // 太近时不主动移动
            {
                return;
            }
        }
    }

    // 安全传送方法
    private void SafeTP(NPC npc, NpcData data, NPCAimedTarget plr, float range, float trackRange)
    {
        bool canTp = !TpTime.ContainsKey(npc.whoAmI) ||
                      (DateTime.UtcNow - TpTime[npc.whoAmI]).TotalSeconds >= data.Teleport;

        if (canTp && range > trackRange * 20f)
        {
            Vector2 safePos = FindPos(plr.Center, data.TrackStopRange * 16f);
            if (safePos != Vector2.Zero && InWorld(safePos))
            {
                npc.Teleport(safePos, 10);
                npc.NetUpdateIgnoreSpamLimit();  // 替换 npc.netUpdate = true;
                TpTime[npc.whoAmI] = DateTime.UtcNow;
                AutoTar(npc, data);
            }
        }
    }
    #endregion

    #region 自动仇恨方法
    internal static void AutoTar(NPC npc, NpcData data)
    {
        if (!data.AutoTarget)
        {
            return;
        }
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
    #endregion

    #region 怪物生成事件(怪物难度方法）
    public static List<int> NpcMap = new List<int>(); // 缓存当前活跃的敌对 NPC 索引
    private void OnNpcSpawn(NpcSpawnEventArgs args)
    {
        if (!Config.Enabled) return;

        NPC npc = Main.npc[args.NpcId];
        if (npc == null || !npc.active || npc.catchItem != 0 ||
            npc.friendly || npc.townNPC || npc.SpawnedFromStatue ||
            npc.type == NPCID.TargetDummy) return;

        // 初始化BOSS表 避免BOSS受到“统一”影响
        if (Config.NpcDatas != null && Config.NpcList.Contains(npc.type))
        {
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
                s.Cooldown = new Dictionary<int, DateTime>();
                s.LastTextTime = DateTime.UtcNow;
                s.MoveState = new MoveState();
                s.EventCounts = new Dictionary<int, int>();
                s.PlayCounts = new Dictionary<string, int>();
            }
        }

        // 难度乘数不能小于1
        var data = Config.NpcDatas?.FirstOrDefault(d => d.Type.Contains(npc.netID));
        // 只有无数据表且在排除表中时才跳过全局配置
        bool skip = data == null && Config.IgnoreNpc.Contains(npc.type);
        int diff = data != null ? data.Difficulty : (skip ? 0 : Config.Difficulty);
        float mult = data != null ? data.Multiplier : (skip ? 1f : Config.Multiplier);
        float plrHp = data != null ? data.PlrHp : (skip ? 0f : Config.PlrHp);
        if (mult < 1f) mult = 1f;

        var st = StateApi.GetState(npc);
        // 从静态模板获取原始基础血量
        if (st.DefLifeMax == 0)
            st.DefLifeMax = ContentSamples.NpcsByNetId[npc.type].defLifeMax;

        // 先处理难度参数（影响攻击、防御等）
        bool Scale = (diff > 0) || (Math.Abs(mult - 1f) > 0.01f);
        if (Scale)
        {
            NPCSpawnParams spawn = default;
            if (diff > 0)
                spawn.playerCountForMultiplayerDifficultyOverride = diff;
            if (Math.Abs(mult - 1f) > 0.01f)
                spawn.difficultyOverride = mult;

            int oldMax = npc.lifeMax;
            double oldRatio = (double)npc.life / oldMax;
            npc.SetDefaults(npc.netID, spawn);
            npc.life = Math.Max(1, (int)(npc.lifeMax * oldRatio));
            // npc.netUpdate = true 实际发送频率仍受 netSpam 限制（普通 NPC 约每 30 帧发一次，Boss 约每 5 帧一次）。
            // npc.NetUpdateIgnoreSpamLimit() 内部会主动减少 netSpam（减少 30 或 5），然后设置 netUpdate = true。
            // 使下次网络更新可以立即发送（绕过限流）。
            // 但同一个 NPC 在同一帧内多次调用，会让 netSpam 负得更多，不过原版逻辑中负值无害（后续会自然回升）
            npc.NetUpdateIgnoreSpamLimit();  // 替换 npc.netUpdate = true;
        }

        // 动态血量（覆盖生命上限）
        if (plrHp > 0f)
        {
            int cur = TShock.Utils.GetActivePlayerCount();
            st.LastPlrCnt = cur > 0 ? cur : 1;

            int newMax = (int)(st.DefLifeMax * (1f + plrHp * cur));
            if (newMax < 1) newMax = 1;

            // 有动态血量时不再强制恢复原始血量，因为已覆盖
            if (npc.lifeMax != newMax)
            {
                double ratio = (double)npc.life / npc.lifeMax;
                npc.lifeMax = newMax;
                npc.life = Math.Max(1, (int)(newMax * ratio));
                npc.NetUpdateIgnoreSpamLimit();  // 替换 npc.netUpdate = true;
            }

        }
        else if (!Scale && npc.lifeMax != st.DefLifeMax)
        {
            // 既无强化也无动态血量，确保血量恢复为原始单人血量
            double ratio = (double)npc.life / npc.lifeMax;
            npc.lifeMax = st.DefLifeMax;
            npc.life = Math.Max(1, (int)(st.DefLifeMax * ratio));
            npc.NetUpdateIgnoreSpamLimit();  // 替换 npc.netUpdate = true;
        }

        // 将有效 NPC 加入缓存
        if (!NpcMap.Contains(npc.whoAmI))
            NpcMap.Add(npc.whoAmI);
    }
    #endregion

    #region 动态血量方法
    private static DateTime LifeMessTime = DateTime.MinValue;
    private void DynLife(NPC npc, NpcState st, float plrHp, int cur)
    {
        if (plrHp <= 0f || st.DefLifeMax <= 0) return;

        if (cur == st.LastPlrCnt || cur <= 0 || st.LastPlrCnt <= 0) return;
        st.LastPlrCnt = cur;

        int newMax = (int)(st.DefLifeMax * (1f + plrHp * cur));
        if (newMax < 1) newMax = 1;
        if (newMax == npc.lifeMax) return;

        int oldMax = npc.lifeMax;
        int delta = newMax - oldMax;

        // 按比例调整当前血量
        double ratio = (double)npc.life / npc.lifeMax;
        npc.lifeMax = newMax;
        npc.life = Math.Max(1, (int)(newMax * ratio));
        npc.NetUpdateIgnoreSpamLimit();  // 替换 npc.netUpdate = true;

        // 限制发送频率
        if (Config.LfBcInt > 0 && (DateTime.UtcNow - LifeMessTime).TotalMilliseconds >= Config.LfBcInt)
        {
            string msg = $"{LogName} {npc.FullName} 因[c/F26F6B:{cur}]人 生命{(delta > 0 ? "[c/5FD769:上升]" : "[c/F26F6B:降低]")}{Math.Abs(delta)}点 ({oldMax} > {newMax})";
            TSPlayer.All.SendMessage(Grad(msg), color);
            LifeMessTime = DateTime.UtcNow;
        }
    }
    #endregion

    #region 自动回血
    public static Dictionary<int, DateTime> HealTimes = new Dictionary<int, DateTime>(); // 跟踪每个NPC上次回血的时间
    internal static void AutoHeal(NPC npc, double heal, int healInt)
    {
        if (heal <= 0) return;

        if (!HealTimes.ContainsKey(npc.whoAmI))
            HealTimes[npc.whoAmI] = DateTime.UtcNow.AddSeconds(-healInt);

        if ((DateTime.UtcNow - HealTimes[npc.whoAmI]).TotalMilliseconds >= healInt * 1000)
        {
            // 计算回复量：最大生命值 * (heal / 100)
            int add = (int)(npc.lifeMax * (heal / 100.0f));
            if (add < 1) add = 1; // 至少回复 1 点生命，避免无效

            if (npc.life + add >= npc.lifeMax) return; // 避免满血重置
            npc.life += add;
            if (npc.life > npc.lifeMax) npc.life = npc.lifeMax - 1;

            npc.NetUpdateIgnoreSpamLimit();  // 替换 npc.netUpdate = true;
            HealTimes[npc.whoAmI] = DateTime.UtcNow;
        }
    }
    #endregion

    #region 监控广播方法
    private static void Broadcast(StringBuilder mess, NPC npc, NpcData data)
    {
        if (Config.BcInt > 0 && (DateTime.UtcNow - BroadcastTime).TotalMilliseconds >= Config.BcInt)
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
            mess.Append($" {Utils.Grad(" ——————————————————")}\n");
            mess.Append($" [c/3A89D0:{npc.FullName}] [防] [c/3A89D0:{npc.defense}]【x】[c/38E06D:{npc.velocity.X:F0}] " +
            $"【y】[c/A5CEBB:{npc.velocity.Y:F0}] 【style】[c/3A89D0:{npc.aiStyle}]\n" +
            $" [ai0] [c/F3A144:{npc.ai[0]:F0}] [ai1] [c/D2A5DF:{npc.ai[1]:F0}]" +
            $" [ai2] [c/EBEB91:{npc.ai[2]:F0}] [ai3] [c/35E635:{npc.ai[3]:F0}]\n");

            // 添加localAI信息
            mess.Append($" [lai0] [c/F3A144:{npc.localAI[0]:F0}] [lai1] [c/D2A5DF:{npc.localAI[1]:F0}]" +
            $" [lai2] [c/EBEB91:{npc.localAI[2]:F0}] [lai3] [c/35E635:{npc.localAI[3]:F0}]\n");

            mess.Append($" {Utils.Grad(" ——————————————————")}\n");

            // 添加AI模式信息
            if (!string.IsNullOrEmpty(aiInfo))
            {
                mess.Append($" {Utils.Grad(" ——————— ai赋值 ——————— ")} \n" +
                            $" {aiInfo} \n" +
                            $" {Utils.Grad(" ——————————————————— ")}");
            }

            TSPlayer.All.SendMessage($"{mess}", 170, 170, 170);
            BroadcastTime = DateTime.UtcNow;
        }
    }
    #endregion
}