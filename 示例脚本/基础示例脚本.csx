using System;
using System.Text;
using Terraria;
using TShockAPI;
using Microsoft.Xna.Framework;

// 安全检查
if (Npc == null) return "NPC为空";
if (State == null) return "State为空";
if (Msg == null) return "Msg为空";

// 脚本入口
var npcName = Npc.FullName;
var npcLife = Npc.life;
var maxLife = Npc.lifeMax;
var lifePercent = (int)((float)npcLife / maxLife * 100);

// 向消息输出添加信息
Msg.AppendLine("=== 怪物信息 ===");
Msg.AppendLine($"名称: {npcName}");
Msg.AppendLine($"血量: {npcLife}/{maxLife} ({lifePercent}%)");
Msg.AppendLine($"位置: X={Npc.Center.X:F0}, Y={Npc.Center.Y:F0}");
Msg.AppendLine($"速度: X={Npc.velocity.X:F1}, Y={Npc.velocity.Y:F1}");

// 修改NPC属性（根据血量调整防御）
if (lifePercent < 50)
{
    Npc.defense += 10;
    TSPlayer.All.SendMessage($"触发半血状态，防御增加10点，当前防御: {Npc.defense}",250,240,150);
}

// 根据时间调整速度
if (State.ActiveTime > 30)
{
    Npc.velocity *= 1.2f;
    TSPlayer.All.SendMessage("活跃超过30秒，速度提升20%", 250, 240, 150);
}

// 返回结果
return "脚本执行完成";