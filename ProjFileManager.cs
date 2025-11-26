using System.Text;
using Newtonsoft.Json;
using TShockAPI;

namespace MonsterSpeed;

// 弹幕文件数据结构
public class ProjectileFileData
{
    [JsonProperty("弹幕名称")]
    public string Name { get; set; } = "未命名弹幕";
    [JsonProperty("弹幕描述")]
    public string Description { get; set; } = "";
    [JsonProperty("弹幕列表")]
    public List<SpawnProjData> Projectiles { get; set; } = new List<SpawnProjData>();
}

// 弹幕文件管理器
internal class ProjFileManager
{
    #region 文件路径
    public static readonly string ProjDir = Path.Combine(TShock.SavePath, "怪物加速", "弹幕配置");
    #endregion

    #region 初始化
    public static void Init()
    {
        try
        {
            if (!Directory.Exists(ProjDir))
            {
                Directory.CreateDirectory(ProjDir);
            }

            if (Directory.GetFiles(ProjDir, "*.json").Length == 0)
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
            var defaults = new List<ProjectileFileData>
            {
                new ProjectileFileData
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
                            Stack = 5,
                            Interval = 10f,
                            KnockBack = 8,
                            Velocity = 100f,
                            Radius = 0f,
                            Angle = 15f,
                            Rotate = 2f,
                            AI = new Dictionary<int, float>(),
                            Life = 180,
                        }
                        
                    }
                },
                
                new ProjectileFileData
                {
                    Name = "多段更新",
                    Description = "多段式更新弹幕",
                    Projectiles = new List<SpawnProjData>
                    {
                        new SpawnProjData()
                        {
                            Condition = "默认配置",
                            Type = 115,
                            Damage = 10,
                            Stack = 15,
                            Interval = 15f,
                            KnockBack = 8,
                            Velocity = 5f,
                            Radius = 0f,
                            Angle = 0f,
                            Rotate = 0f,
                            AI = new Dictionary<int, float>() { { 0, 50f } },
                            Life = 180,
                            UpdateTime = 500,
                            UpdateProj = new List<string>(){ "二次更新" }
                        },
                    }
                },

                new ProjectileFileData
                {
                    Name = "散射弹幕",
                    Description = "多方向散射弹幕",
                    Projectiles = new List<SpawnProjData>
                    {
                        new SpawnProjData
                        {
                            Type = 115,
                            Stack = 8,
                            Interval = 60f,
                            Life = 180,
                            Damage = 25,
                            KnockBack = 3,
                            Velocity = 8f,
                            Angle = 45f
                        }
                    }
                }
            };

            foreach (var proj in defaults)
            {
                SaveFile(proj);
            }

            TShock.Log.ConsoleInfo($"已创建 {defaults.Count} 个弹幕配置文件");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"创建弹幕配置失败: {ex.Message}");
        }
    }
    #endregion

    #region 获取弹幕配置 - 直接读取文件
    public static ProjectileFileData GetFile(string name)
    {
        try
        {
            if (!Directory.Exists(ProjDir))
                return new ProjectileFileData();

            var filePath = Path.Combine(ProjDir, $"{name}.json");

            if (!File.Exists(filePath))
                return new ProjectileFileData();

            var content = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<ProjectileFileData>(content)!;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"读取弹幕配置失败: {name}, 错误: {ex.Message}");
            return new ProjectileFileData();
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
    public static void SaveFile(ProjectileFileData projFile)
    {
        try
        {
            var file = $"{projFile.Name}.json";
            var path = Path.Combine(ProjDir, file);
            var json = JsonConvert.SerializeObject(projFile, Formatting.Indented);
            File.WriteAllText(path, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"保存弹幕配置失败: {projFile.Name}, 错误: {ex.Message}");
        }
    }
    #endregion
}
