using System.Text;
using Newtonsoft.Json;
using TShockAPI;

namespace MonsterSpeed;

// 触发条件数据结构
public class CondData
{
    [JsonProperty("条件名称")]
    public string Name { get; set; } = "未命名";
    [JsonProperty("条件描述")]
    public string Desc { get; set; } = "";
    [JsonProperty("触发条件")]
    public Conditions Cond { get; set; } = new Conditions() { NpcLift = "0,100"};
}

// 触发条件管理器
internal class ConditionFile
{
    public static readonly string Dir = Path.Combine(TShock.SavePath, "怪物加速", "触发条件");

    #region 初始化
    public static void Init()
    {
        try
        {
            if (!Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
            }

            if (Directory.GetFiles(Dir, "*.json").Length == 0)
            {
                CreateDefault();
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[怪物加速] 条件系统初始化失败: {ex.Message}");
        }
    }
    #endregion

    #region 创建默认条件
    private static void CreateDefault()
    {
        try
        {
            var defaults = new List<CondData>
            {
                new CondData { Name = "默认配置", Desc = "全阶段触发", Cond = new Conditions { NpcLift = "0,100" } },
                new CondData { Name = "半血", Desc = "0-50%血触发", Cond = new Conditions { NpcLift = "0,50" } },
                new CondData { Name = "低血", Desc = "0-30%血触发", Cond = new Conditions { NpcLift = "0,30" } },
                new CondData { Name = "中血", Desc = "31-70%血触发", Cond = new Conditions { NpcLift = "31,70" } },
                new CondData { Name = "高血", Desc = "71-100%血触发", Cond = new Conditions { NpcLift = "71,100" } },
                new CondData { Name = "濒死", Desc = "低于10%血触发", Cond = new Conditions { NpcLift = "0,10" } },
                new CondData { Name = "史王不在场", Desc = "击败史莱姆王后,史莱姆王不在场时触发", Cond = new Conditions 
                {
                    Progress = new List<string>(){ "史莱姆王" },
                    NpcLift = "0,30", // BOSS血量在0%-30%之间
                    Range = 84, // 与玩家距离在84格内
                    RangeMonsters =  new Conditions.RangeMonsterCondition() 
                    {
                        MstID = 50 , // 史莱姆王ID
                        Range = 84,
                        MatchCnt = 0
                    },

                    ExecuteCount = new Dictionary<int, int>()
                    {
                        { 0, 1 },  // 事件0至少执行过1次
                        { 1, 1 }   // 事件1至少执行过1次
                    },

                    MarkerConds = new Dictionary<string, string[]>()
                    {
                        { "已召唤史王", new string[] { "==0" } }
                    }
                }},

            };

            foreach (var cond in defaults)
            {
                SaveFile(cond);
            }

            TShock.Log.ConsoleInfo($"[怪物加速] 已创建 {defaults.Count} 个限制条件文件");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[怪物加速] 创建条件失败: {ex.Message}");
        }
    }
    #endregion

    #region 获取条件 - 直接读取文件
    public static CondData GetCond(string name)
    {
        try
        {
            if (!Directory.Exists(Dir))
                return new CondData();

            var filePath = Path.Combine(Dir, $"{name}.json");

            if (!File.Exists(filePath))
                return new CondData();

            var content = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<CondData>(content)!;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[怪物加速] 读取条件失败: {name}, 错误: {ex.Message}");
            return new CondData();
        }
    }

    public static Conditions GetCondData(string name)
    {
        var cond = GetCond(name);
        if (cond == null)
        {
            TShock.Log.ConsoleError($"[怪物加速] 条件 '{name}' 不存在，使用默认条件");
            return new Conditions { NpcLift = "0,100" };
        }
        return cond.Cond;
    }
    #endregion

    #region 保存条件
    public static void SaveFile(CondData cond)
    {
        try
        {
            var file = $"{cond.Name}.json";
            var path = Path.Combine(Dir, file);
            var json = JsonConvert.SerializeObject(cond, Formatting.Indented);
            File.WriteAllText(path, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[怪物加速] 保存条件失败: {cond.Name}, 错误: {ex.Message}");
        }
    }
    #endregion

    #region 重载所有配置
    public static void Reload()
    {
        try
        {
            if (!Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
                if (Directory.GetFiles(Dir, "*.json").Length == 0)
                    CreateDefault();
            }


            var all = new List<CondData>();
            var jsonFiles = Directory.GetFiles(Dir, "*.json");

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var content = File.ReadAllText(filePath);
                    var file = JsonConvert.DeserializeObject<CondData>(content);
                    if (file != null)
                        all.Add(file);
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError($"[怪物加速] 读取配置文件失败: " +
                        $"{Path.GetFileName(filePath)}, 错误:\n {ex.Message}");
                }
            }

            if (all.Count > 0)
                foreach (var FileData in all)
                    SaveFile(FileData);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[怪物加速] 重载条件配置失败: {ex.Message}");
        }
    }
    #endregion
}