using System;
using System.Threading.Tasks;
using System.Linq;
using Terraria;
using Microsoft.Xna.Framework;
using TShockAPI;

// 安全检查
if (Npc == null) return "NPC为空";
if (State == null) return "State为空";
if (Msg == null) return "Msg为空";

Msg.AppendLine("=== 异步任务演示 ===");

// 注意：此脚本需要在时间事件中设置为异步执行(AsyncExec = true)

// 模拟耗时操作
async System.Threading.Tasks.Task DoLongTask()
{
    Msg.AppendLine("开始异步任务...");

    // 模拟耗时操作
    for (int i = 1; i <= 5; i++)
    {
        await System.Threading.Tasks.Task.Delay(100); // 每100ms执行一步

        // 更新NPC状态（血量减少）
        if (Npc.life > 1)
        {
            Npc.life = Math.Max(1, Npc.life - (int)(Npc.lifeMax * 0.01f));
            Npc.netUpdate = true;

            Msg.AppendLine($"  第{i}步: 血量减少1%");

            // 生成特效
            Dust.NewDust(Npc.position, Npc.width, Npc.height, 6, 0f, 0f, 100, Color.Red, 0.5f);
        }
    }
}

// 并行处理多个NPC
async System.Threading.Tasks.Task ProcessMultipleNPCs()
{
    var sameTypeNPCs = Main.npc
        .Where(n => n != null && n.active && n.type == Npc.type)
        .ToList();

    Msg.AppendLine($"发现 {sameTypeNPCs.Count} 个相同类型NPC");

    foreach (var npc in sameTypeNPCs)
    {
        // 为每个NPC添加特效
        for (int i = 0; i < 3; i++)
        {
            await System.Threading.Tasks.Task.Delay(50);
            Dust.NewDust(npc.position, npc.width, npc.height, 57, 0f, 0f, 100, Color.Yellow, 1f);
        }

        // 同步速度
        npc.velocity = Npc.velocity;
        npc.netUpdate = true;
    }

    Msg.AppendLine($"已处理 {sameTypeNPCs.Count} 个NPC的速度同步");
}

// 主异步逻辑
try
{
    Msg.AppendLine("开始异步处理...");

    // 执行异步任务
    await DoLongTask();

    // 处理多个NPC
    await ProcessMultipleNPCs();

    // 任务完成后的操作
    Msg.AppendLine("任务完成");

    // 更新指示物
    if (!State.Markers.ContainsKey("AsyncExecCount"))
    {
        State.Markers["AsyncExecCount"] = 0;
    }
    State.Markers["AsyncExecCount"]++;

    // 根据异步执行次数调整行为
    int asyncCount = State.Markers["AsyncExecCount"];
    if (asyncCount >= 3)
    {
        Msg.AppendLine("异步执行超过3次，进入强化状态");
        Npc.defense += 10;
        Npc.damage += 10;

        // 全屏特效
        CombatText.NewText(Npc.getRect(), Color.Orange, "Async Boost!", true);
    }

    Msg.AppendLine($"异步任务执行完成，总计执行 {asyncCount} 次");
}
catch (Exception ex)
{
    Msg.AppendLine($"异步任务出错: {ex.Message}");
    return $"错误: {ex.Message}";
}

return "异步脚本执行完毕";