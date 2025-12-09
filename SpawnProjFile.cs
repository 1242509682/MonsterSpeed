using System.Text;
using Newtonsoft.Json;
using TShockAPI;

namespace MonsterSpeed;

// 弹幕文件数据结构
public class ProjData
{
    [JsonProperty("弹幕名称")]
    public string Name { get; set; } = "未命名弹幕";
    [JsonProperty("弹幕描述")]
    public string Description { get; set; } = "";
    [JsonProperty("弹幕列表")]
    public List<SpawnProjData> Projectiles { get; set; } = new List<SpawnProjData>();
}

// 弹幕文件管理器
internal class SpawnProjFile
{
    public static readonly string Dir = Path.Combine(TShock.SavePath, "怪物加速", "弹幕配置");

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
            TShock.Log.ConsoleError($"弹幕系统初始化失败: {ex.Message}");
        }
    }
    #endregion

    #region 创建默认弹幕配置
    private static void CreateDefault()
    {
        try
        {
            var defaults = new List<ProjData>
            {
                new ProjData
                {
                    Name = "默认弹幕",
                    Description = "基础弹幕配置",
                    Projectiles = new List<SpawnProjData>
                    {
                        new SpawnProjData()
                        {
                            Condition = "默认配置",
                            Type = 671,
                            Damage = 10,
                            Stack = 10,
                            Interval = 60,
                            KnockBack = 8,
                            Velocity = 10,
                            AI = new Dictionary<int, float>(),
                            Life = 120,
                            UpdProj = new List<string> { "追踪弹幕" }
                        }

                    }
                },

                new ProjData
                {
                    Name = "克眼二更弹幕",
                    Description = "克苏鲁之眼二次更新弹幕",
                    Projectiles = new List<SpawnProjData>
                    {
                        new SpawnProjData()
                        {
                            Condition = "默认配置",
                            Type = 115,
                            Damage = 10,
                            Stack = 30,
                            Interval = 15f,
                            KnockBack = 8,
                            Velocity = 5f,
                            AI = new Dictionary<int, float>() { { 0, 50f } },
                            Life = 180,
                            UpdProj = new List<string>(){ "多段更新" }
                        },
                    }
                },

            };

            foreach (var proj in defaults)
            {
                SaveFile(proj);
            }

            TShock.Log.ConsoleInfo($"已创建 {defaults.Count} 个生成弹幕配置文件");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"创建弹幕配置失败: {ex.Message}");
        }
    }
    #endregion

    #region 获取弹幕配置 - 直接读取文件
    public static ProjData GetFile(string name)
    {
        try
        {
            if (!Directory.Exists(Dir))
                return new ProjData();

            var filePath = Path.Combine(Dir, $"{name}.json");

            if (!File.Exists(filePath))
                return new ProjData();

            var content = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<ProjData>(content)!;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"读取弹幕配置失败: {name}, 错误: {ex.Message}");
            return new ProjData();
        }
    }

    public static List<SpawnProjData> GetData(string name)
    {
        var file = GetFile(name);
        if (file == null || file.Projectiles == null || file.Projectiles.Count == 0)
        {
            TShock.Log.ConsoleError($"弹幕配置 '{name}' 不存在或为空");
            return new List<SpawnProjData>();
        }
        return file.Projectiles;
    }
    #endregion

    #region 保存弹幕配置
    public static void SaveFile(ProjData projFile)
    {
        try
        {
            var file = $"{projFile.Name}.json";
            var path = Path.Combine(Dir, file);
            var json = JsonConvert.SerializeObject(projFile, Formatting.Indented);
            File.WriteAllText(path, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"保存弹幕配置失败: {projFile.Name}, 错误: {ex.Message}");
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

            var all = new List<ProjData>();
            var jsonFiles = Directory.GetFiles(Dir, "*.json");

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var content = File.ReadAllText(filePath);
                    var file = JsonConvert.DeserializeObject<ProjData>(content);
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
