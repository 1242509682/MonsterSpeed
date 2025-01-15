using Microsoft.Xna.Framework;
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
    private static Dictionary<int, float> Cooldowns = new Dictionary<int, float>(); //用于追踪生成随从NPC冷却时间
    public static void SpawnMonsters(List<SpawnNpcData> datas, NPC npc)
    {
        var flag = false;
        foreach (var data in datas)
        {
            //配置为空
            if (data == null) continue;

            foreach (var id in data.NPCID)
            {
                var Stack = Main.npc.Count(p => p.active && p.type == id);
                if (Stack >= data.NpcStack) continue;

                // 检查是否已经有冷却时间设置，如果没有则初始化为0（即没有冷却）
                if (!Cooldowns.ContainsKey(id))
                {
                    Cooldowns[id] = 0f;
                }

                // 如果冷却时间为0或小于等于0，则允许生成怪物
                if (Cooldowns[id] <= 0f)
                {
                    var npc2 = TShock.Utils.GetNPCById(id);
                    if (npc2 == null) return;

                    if (npc2.type != 113 && npc2.type != 0 && npc2.type < Terraria.ID.NPCID.Count)
                    {
                        //以“玩家为中心”为true 以玩家为中心,否则以被击中的npc为中心
                        var tar = npc.GetTargetData(true);
                        var pos = data.TarCenter
                                ? new Vector2(tar.Center.X, tar.Center.Y)
                                : new Vector2(npc.Center.X, npc.Center.Y);

                        // 新的生成位置
                        var NewPos = Terraria.Utils.ToTileCoordinates(pos);// 将世界坐标转换为瓷砖坐标

                        //召唤怪物
                        TSPlayer.Server.SpawnNPC(npc2.type, npc2.FullName, data.NpcStack,
                                                 NewPos.X, NewPos.Y, data.Range, data.Range);

                        // 设置冷却时间
                        Cooldowns[id] = data.Interval;
                        Stack++;
                        flag = true;
                    }
                }
                else
                {
                    // 减少冷却时间
                    Cooldowns[id] -= 1f;
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
