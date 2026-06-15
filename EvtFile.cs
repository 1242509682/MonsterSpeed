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

        // 如果文件夹为空，创建两个默认事件文件
        if (Directory.GetFiles(Dir, "*.json").Length == 0)
        {
            CreateDefault("召唤噬魂怪", GetDeadEvents());
            CreateDefault("召唤猩红喀迈拉", GetHitEvents());
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

    private static List<TimerData> GetDeadEvents() => new()
    {
        new TimerData
        {
            CoolTime = 0,
            Condition = "默认配置",
            SpawnNPC = new List<SpawnNpcData>
            {
                new SpawnNpcData { NPCID = [ Terraria.ID.NPCID.EaterofSouls ] }
            }
        }
    };

    private static List<TimerData> GetHitEvents() => new()
    {
        new TimerData
        {
            CoolTime = 0,
            Condition = "默认配置",
            SpawnNPC = new List<SpawnNpcData>
            {
                new SpawnNpcData { NPCID = [ Terraria.ID.NPCID.BigCrimera ] }
            }
        }
    };

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