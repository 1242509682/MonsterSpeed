﻿using Microsoft.Xna.Framework;
using MonsterSpeed.Progress;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;

namespace MonsterSpeed;

//随从怪物结构
public class SpawnNpcData
{
    [JsonProperty("怪物ID", Order = 0)]
    public List<int> NPCID = new List<int>();
    [JsonProperty("范围", Order = 1)]
    public int Range = 25;
    [JsonProperty("数量", Order = 2)]
    public int NpcStack = 5;
    [JsonProperty("间隔", Order = 3)]
    public float Interval = 300;
    [JsonProperty("以玩家为中心", Order = 5)]
    public bool TarCenter = false;
}

internal class MyMonster
{
    #region 召唤怪物方法
    public static int SNCount = new int(); //用于追踪所有NPC生成次数
    private static Dictionary<int, float> SpawnTimer = new Dictionary<int, float>(); //用于追踪生成随从NPC冷却时间
    public static void SpawnMonsters(List<SpawnNpcData> SpawnNpc, NPC npc)
    {
        var flag = false;
        foreach (var mos in SpawnNpc)
        {
            //配置为空
            if (mos == null) continue;

            foreach (var id in mos.NPCID)
            {
                var Stack = Main.npc.Count(p => p.active && p.type == id);
                if (Stack >= mos.NpcStack) continue;

                // 检查是否已经有冷却时间设置，如果没有则初始化为0（即没有冷却）
                if (!SpawnTimer.ContainsKey(id))
                {
                    SpawnTimer[id] = 0f;
                }

                // 如果冷却时间为0或小于等于0，则允许生成怪物
                if (SpawnTimer[id] <= 0f)
                {
                    var npc2 = TShock.Utils.GetNPCById(id);
                    if (npc2 == null) return;

                    if (npc2.type != 113 && npc2.type != 0 && npc2.type < Terraria.ID.NPCID.Count)
                    {
                        //以“玩家为中心”为true 以玩家为中心,否则以被击中的npc为中心
                        var plr = Main.player[npc.target];
                        var pos = mos.TarCenter
                                ? new Vector2(plr.Center.X, plr.Center.Y)
                                : new Vector2(npc.Center.X, npc.Center.Y);

                        // 新的生成位置
                        var NewPos = Terraria.Utils.ToTileCoordinates(pos);// 将世界坐标转换为瓷砖坐标

                        //召唤怪物
                        TSPlayer.Server.SpawnNPC(npc2.type, npc2.FullName, mos.NpcStack,
                                                 NewPos.X, NewPos.Y, mos.Range, mos.Range);

                        // 设置冷却时间
                        SpawnTimer[id] = mos.Interval;
                        Stack++;
                        flag = true;
                    }
                }
                else
                {
                    // 减少冷却时间
                    SpawnTimer[id] -= 1f;
                }
            }
        }

        if (flag)
        {
            SNCount++; //增加怪物生成次数
        }
    } 
    #endregion
}
