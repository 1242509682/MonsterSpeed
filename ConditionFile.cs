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
internal class CondFileManager
{
    #region 文件路径
    public static readonly string CondDir = Path.Combine(TShock.SavePath, "怪物加速", "触发条件");
    #endregion

    #region 初始化
    public static void Init()
    {
        try
        {
            if (!Directory.Exists(CondDir))
            {
                Directory.CreateDirectory(CondDir);
            }

            if (Directory.GetFiles(CondDir, "*.json").Length == 0)
            {
                CreateDefault();
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"条件系统初始化失败: {ex.Message}");
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
                new CondData { Name = "濒死", Desc = "低于10%血触发", Cond = new Conditions { NpcLift = "0,10" } }
            };

            foreach (var cond in defaults)
            {
                SaveCond(cond);
            }

            TShock.Log.ConsoleInfo($"已创建 {defaults.Count} 个条件文件");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"创建条件失败: {ex.Message}");
        }
    }
    #endregion

    #region 获取条件 - 直接读取文件
    public static CondData GetCond(string name)
    {
        try
        {
            if (!Directory.Exists(CondDir))
                return new CondData();

            var filePath = Path.Combine(CondDir, $"{name}.json");

            if (!File.Exists(filePath))
                return new CondData();

            var content = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<CondData>(content)!;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"读取条件失败: {name}, 错误: {ex.Message}");
            return new CondData();
        }
    }

    public static Conditions GetCondData(string name)
    {
        var cond = GetCond(name);
        if (cond == null)
        {
            TShock.Log.ConsoleError($"条件 '{name}' 不存在，使用默认条件");
            return new Conditions { NpcLift = "0,100" };
        }
        return cond.Cond;
    }
    #endregion

    #region 保存条件
    public static void SaveCond(CondData cond)
    {
        try
        {
            var file = $"{cond.Name}.json";
            var path = Path.Combine(CondDir, file);
            var json = JsonConvert.SerializeObject(cond, Formatting.Indented);
            File.WriteAllText(path, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"保存条件失败: {cond.Name}, 错误: {ex.Message}");
        }
    }
    #endregion
}