using Newtonsoft.Json;
using System.Text;
using TShockAPI;
using static MonsterSpeed.MonsterSpeed;
using static MonsterSpeed.Configuration;

namespace MonsterSpeed;

public static class EvtFile
{
    public static readonly string Dir = Path.Combine(Paths, "时间事件");

    public static void Init()
    {
        if (!Directory.Exists(Dir))
            Directory.CreateDirectory(Dir);


        // 如果文件夹为空，创建三个默认事件文件
        if (Directory.GetFiles(Dir, "*.json").Length == 0)
        {
            CreateDefault("召唤噬魂怪",
            [
                new TimerData
                {
                    SpawnNPC = [ new SpawnNpcData { NPCID = [Terraria.ID.NPCID.EaterofSouls] }]
                }
            ]);

            CreateDefault("召唤猩红喀迈拉",
            [
                new TimerData
                { 
                    SpawnNPC = [ new SpawnNpcData { NPCID = [Terraria.ID.NPCID.BigCrimera] }]
                }
            ]);

            CreateDefault("召唤魔唾液",
            [
                new TimerData
                {
                    SpawnNPC = [ new SpawnNpcData { NPCID = [Terraria.ID.NPCID.VileSpit], Interval = 120, }]
                }
            ]);
        }
    }

    private static void CreateDefault(string name, List<TimerData> events)
    {
        var data = new EventFileData
        {
            EventName = name,
            TimerEvents = events
        };
        SaveFile(name, data);
    }

    public static List<TimerData> Load(string name)
    {
        var path = Path.Combine(Dir, $"{name}.json");
        if (!File.Exists(path))
        {
            TShock.Log.ConsoleError($"{LogName} 事件文件不存在: {name}");
            return new List<TimerData>();
        }
        try
        {
            var json = File.ReadAllText(path);
            var data = JsonConvert.DeserializeObject<EventFileData>(json);
            return data?.TimerEvents ?? new List<TimerData>();
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"{LogName} 读取事件文件失败 {name}: {ex.Message}");
            return new List<TimerData>();
        }
    }

    public static void SaveFile(string name, EventFileData data)
    {
        var path = Path.Combine(Dir, $"{name}.json");
        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(path, json, Encoding.UTF8);
    }
}