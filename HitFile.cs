using Newtonsoft.Json;
using System.Text;
using TShockAPI;
using Terraria.ID;
using static MonsterSpeed.MonsterSpeed;

namespace MonsterSpeed;

public static class HitFile
{
    public static readonly string Dir = Path.Combine(TShock.SavePath, "怪物加速", "弹幕命中事件");

    public static void Init()
    {
        if (!Directory.Exists(Dir))
            Directory.CreateDirectory(Dir);

        if (Directory.GetFiles(Dir, "*.json").Length == 0)
            CreateDefault();
    }

    private static void CreateDefault()
    {
        var sample = new EventFileData
        {
            EventName = "召唤猩红喀迈拉",
            TimerEvents = new List<TimerData>
            {
                new TimerData
                {
                    CoolTime = 0,
                    Condition = "默认配置",
                    SpawnNPC = new List<SpawnNpcData>()
                    {
                       new SpawnNpcData()
                       {
                           NPCID = [ NPCID.BigCrimera]
                       }
                    },
                    MarkerList = new List<MstMarkerMod>()
                }
            }
        };
        SaveFile("召唤猩红喀迈拉", sample);
    }

    public static List<TimerData> Load(string name)
    {
        var path = Path.Combine(Dir, $"{name}.json");
        if (!File.Exists(path))
        {
            TShock.Log.ConsoleError($"{LogName} 命中事件文件不存在: {name}");
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
            TShock.Log.ConsoleError($"{LogName} 读取命中事件失败 {name}: {ex.Message}");
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