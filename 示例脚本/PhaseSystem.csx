// PhaseSystem.csx - 替代复杂的指示物阶段系统
public class PhaseSystem
{
    private ScriptCtx ctx;
    
    public PhaseSystem(ScriptCtx c) { ctx = c; }
    
    public object Run()
    {
        var npc = ctx.Npc;
        var api = ctx.Api;
        var msg = ctx.Msg;
        
        try
        {
            // 1. 获取当前阶段（替代指示物"phase"）
            int phase = api.Get(npc, "phase", 0);
            
            // 2. 根据血量切换阶段（替代血量条件）
            float lifePercent = ctx.LifePercent;
            
            if (lifePercent > 70 && phase < 1)
            {
                // 进入阶段1
                api.Set(npc, "phase", 1);
                api.Message($"{npc.FullName} 进入第一阶段", Color.Yellow);
                msg?.AppendLine(" 进入第一阶段");
                phase = 1;
            }
            else if (lifePercent > 30 && lifePercent <= 70 && phase < 2)
            {
                // 进入阶段2
                api.Set(npc, "phase", 2);
                api.Message($"{npc.FullName} 进入第二阶段", Color.Orange);
                msg?.AppendLine(" 进入第二阶段");
                phase = 2;
            }
            else if (lifePercent <= 30 && phase < 3)
            {
                // 进入狂暴阶段
                api.Set(npc, "phase", 3);
                api.Message($"{npc.FullName} 进入狂暴阶段!", Color.Red);
                msg?.AppendLine(" 进入狂暴阶段");
                phase = 3;
            }
            
            // 3. 执行阶段逻辑
            ExecutePhaseLogic(phase, npc, api, msg);
            
            // 4. 使用计时器控制技能冷却（替代CD系统）
            ManageCooldowns(npc, api);
            
            return new { phase, lifePercent };
        }
        catch (Exception ex)
        {
            msg?.AppendLine($" 错误: {ex.Message}");
            return new { error = ex.Message };
        }
    }
    
    private void ExecutePhaseLogic(int phase, NPC npc, ScriptApiProxy api, StringBuilder msg)
    {
        switch (phase)
        {
            case 1:
                // 阶段1：发射弹幕
                if (api.TimerDone("phase1_attack", 3.0))
                {
                    FireProjectiles(npc, api, 3, 10f);
                    api.StartTimer("phase1_attack");
                }
                break;
                
            case 2:
                // 阶段2：召唤随从
                if (api.TimerDone("phase2_summon", 5.0))
                {
                    SummonMinions(npc, api, 2);
                    api.StartTimer("phase2_summon");
                }
                // 发射更多弹幕
                if (api.TimerDone("phase2_attack", 2.0))
                {
                    FireProjectiles(npc, api, 5, 15f);
                    api.StartTimer("phase2_attack");
                }
                break;
                
            case 3:
                // 阶段3：狂暴模式
                npc.defense += 10;
                npc.damage = (int)(npc.damage * 1.5f);
                
                // 快速攻击
                if (api.TimerDone("phase3_attack", 1.0))
                {
                    FireProjectiles(npc, api, 8, 20f);
                    api.StartTimer("phase3_attack");
                }
                
                // 随机传送
                if (api.Chance(10)) // 10%概率
                {
                    var target = ScriptApi.GetTarget(npc);
                    if (target != null)
                    {
                        var offset = new Vector2(
                            api.Random(-100, 100),
                            api.Random(-100, 100)
                        );
                        api.Teleport(npc, target.TPlayer.Center + offset);
                    }
                }
                break;
        }
    }
    
    private void FireProjectiles(NPC npc, ScriptApiProxy api, int count, float speed)
    {
        var target = ScriptApi.GetTarget(npc);
        if (target == null) return;
        
        var dir = target.TPlayer.Center - npc.Center;
        if (dir != Vector2.Zero) dir.Normalize();
        
        for (int i = 0; i < count; i++)
        {
            var angle = (i - count/2f) * 0.3f;
            var vel = dir.RotatedBy(angle) * speed;
            api.SpawnProj(npc, 132, npc.Center, vel, 40, 5f);
        }
        
        // 记录发射次数
        int fired = api.Inc(npc, "projectiles_fired", 1);
        api.SetGlobal("total_projectiles", api.Global("total_projectiles", 0) + 1);
    }
    
    private void SummonMinions(NPC npc, ScriptApiProxy api, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var offset = new Vector2(
                api.Random(-200, 200),
                api.Random(-200, 200)
            );
            api.SpawnMob(npc, NPCID.BlueSlime, npc.Center + offset);
        }
        
        // 记录召唤次数
        int summoned = api.Inc(npc, "minions_summoned", count);
        api.Message($"召唤了{count}个随从，总计{summoned}个", Color.Green);
    }
    
    private void ManageCooldowns(NPC npc, ScriptApiProxy api)
    {
        // 检查特殊技能冷却
        if (api.Check(npc, "special_skill_ready", "==", true))
        {
            // 特殊技能已就绪
            if (api.Chance(20)) // 20%概率释放
            {
                UseSpecialSkill(npc, api);
                api.Set(npc, "special_skill_ready", false);
                
                // 启动10秒冷却
                api.StartTimer("special_skill_cd");
            }
        }
        else if (api.TimerDone("special_skill_cd", 10.0))
        {
            // 冷却结束
            api.Set(npc, "special_skill_ready", true);
        }
    }
    
    private void UseSpecialSkill(NPC npc, ScriptApiProxy api)
    {
        api.Message($"{npc.FullName} 使用了特殊技能!", Color.Purple);
        
        // 发射一圈弹幕
        for (int i = 0; i < 12; i++)
        {
            var angle = i * Math.PI / 6;
            var vel = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 15f;
            api.SpawnProj(npc, 132, npc.Center, vel, 60, 8f, 180, false);
        }
    }
}

return new PhaseSystem(ctx).Run();