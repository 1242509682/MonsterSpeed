// ConditionSystem.csx - 替代复杂的条件检查系统
public class ConditionSystem
{
    private ScriptCtx ctx;
    
    public ConditionSystem(ScriptCtx c) { ctx = c; }
    
    public object Run()
    {
        var npc = ctx.Npc;
        var api = ctx.Api;
        var msg = ctx.Msg;
        
        // 1. 血量条件（替代NpcLift）
        float lifePercent = ctx.LifePercent;
        bool hpCondition = lifePercent < 50; // 半血以下
        
        // 2. 玩家条件（替代RangePlayers）
        var players = ScriptApi.GetPlayersInRange(npc, 20);
        bool playerCondition = players.Count >= 2; // 至少2个玩家
        
        // 3. 时间条件（替代Timer）
        bool timeCondition = !Main.dayTime; // 夜晚
        
        // 4. 进度条件（通过TShockAPI检查）
        bool progressCondition = NPC.downedBoss3; // 击败骷髅王后
        
        // 5. 复合条件
        bool allConditions = hpCondition && playerCondition && timeCondition && progressCondition;
        
        if (allConditions)
        {
            msg?.AppendLine(" 所有条件满足，执行特殊行为");
            ExecuteSpecialBehavior(npc, api, players);
        }
        else
        {
            msg?.AppendLine($" 条件未满足: HP<50%={hpCondition}, 玩家≥2={playerCondition}, 夜晚={timeCondition}, 进度={progressCondition}");
        }
        
        // 6. 随机行为（替代随机指示物）
        if (api.Chance(30)) // 30%概率
        {
            ExecuteRandomBehavior(npc, api);
        }
        
        return new { 
            conditionsMet = allConditions,
            hp = lifePercent,
            players = players.Count,
            time = Main.dayTime ? "白天" : "夜晚"
        };
    }
    
    private void ExecuteSpecialBehavior(NPC npc, ScriptApiProxy api, List<TSPlayer> players)
    {
        // 增强自身
        npc.defense += 20;
        npc.damage = (int)(npc.damage * 1.3f);
        
        api.Message($"{npc.FullName} 在夜晚吸收了玩家的力量!", Color.DarkViolet);
        
        // 对每个玩家施加Debuff
        foreach (var player in players)
        {
            if (player != null && player.Active)
            {
                api.AddBuffToPlayer(player, BuffID.Slow, 300); // 5秒缓慢
                api.AddBuffToPlayer(player, BuffID.Weak, 300); // 5秒虚弱
            }
        }
        
        // 记录特殊行为次数
        api.Inc(npc, "special_behaviors", 1);
    }
    
    private void ExecuteRandomBehavior(NPC npc, ScriptApiProxy api)
    {
        // 随机选择一种行为
        var behaviors = new[] { "teleport", "heal", "summon", "buff" };
        var choice = api.Choice(behaviors);
        
        switch (choice)
        {
            case "teleport":
                // 随机传送
                var offset = new Vector2(api.Random(-300, 300), api.Random(-300, 300));
                api.Teleport(npc, npc.Center + offset);
                msg?.AppendLine(" 随机传送");
                break;
                
            case "heal":
                // 治疗
                api.Heal(npc, npc.lifeMax / 10);
                msg?.AppendLine(" 随机治疗");
                break;
                
            case "summon":
                // 召唤
                api.SpawnMob(npc, NPCID.GreenSlime, npc.Center);
                msg?.AppendLine(" 随机召唤");
                break;
                
            case "buff":
                // 增益
                npc.AddBuff(BuffID.Ironskin, 600);
                msg?.AppendLine(" 随机增益");
                break;
        }
    }
}

return new ConditionSystem(ctx).Run();