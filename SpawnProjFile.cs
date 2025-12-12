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
public class SpawnProjFile
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

            // 新增：环形弹幕
            new ProjData
            {
                Name = "环形弹幕",
                Description = "一圈弹幕向四周发射",
                Projectiles = new List<SpawnProjData>
                {
                    new SpawnProjData()
                    {
                        Condition = "默认配置",
                        Type = 115,
                        Damage = 15,
                        Stack = 1,
                        CircleCnt = 12,
                        CircleRad = 3,
                        CircleAngInc = 30,
                        Velocity = 8,
                        Life = 120,
                        KnockBack = 6,
                        UpdProj = new List<string>(){ "螺旋追踪" }
                    }
                }
            },

            // 新增：扇形弹幕
            new ProjData
            {
                Name = "扇形弹幕",
                Description = "扇形范围弹幕",
                Projectiles = new List<SpawnProjData>
                {
                    new SpawnProjData()
                    {
                        Condition = "默认配置",
                        Type = 671,
                        Damage = 20,
                        Stack = 1,
                        SprCnt = 5,
                        SprAngInc = 15,
                        Velocity = 12,
                        Life = 150,
                        KnockBack = 8,
                        AngleCfg = "0",
                        UpdProj = new List<string>(){ "加速弹幕" }
                    }
                }
            },

            // 新增：线性弹幕
            new ProjData
            {
                Name = "线性弹幕",
                Description = "一排直线弹幕",
                Projectiles = new List<SpawnProjData>
                {
                    new SpawnProjData()
                    {
                        Condition = "默认配置",
                        Type = 664,
                        Damage = 18,
                        Stack = 1,
                        LineCnt = 5,
                        LineOff = "2,0",
                        Velocity = 10,
                        Life = 120,
                        KnockBack = 7,
                        UpdProj = new List<string>(){ "分裂弹幕" }
                    }
                }
            },

            // 新增：复合弹幕（圆形+扇形）
            new ProjData
            {
                Name = "复合弹幕",
                Description = "圆形+扇形组合弹幕",
                Projectiles = new List<SpawnProjData>
                {
                    new SpawnProjData()
                    {
                        Condition = "默认配置",
                        Type = 115,
                        Damage = 12,
                        Stack = 1,
                        CircleCnt = 8,
                        CircleRad = 4,
                        SprCnt = 3,
                        SprAngInc = 20,
                        Velocity = 6,
                        Life = 180,
                        KnockBack = 5,
                        UpdProj = new List<string>(){ "智能追踪", "分裂弹幕" }
                    }
                }
            },

            // 新增：螺旋弹幕
            new ProjData
            {
                Name = "螺旋弹幕",
                Description = "螺旋状发射弹幕",
                Projectiles = new List<SpawnProjData>
                {
                    new SpawnProjData()
                    {
                        Condition = "默认配置",
                        Type = 671,
                        Damage = 15,
                        Stack = 24,
                        Interval = 5,
                        AngleCfg = "0,360",
                        Velocity = 8,
                        Life = 200,
                        KnockBack = 6,
                        UpdProj = new List<string>(){ "螺旋追踪" }
                    }
                }
            },

            // 新增：追踪弹幕组
            new ProjData
            {
                Name = "追踪弹幕组",
                Description = "生成后自动追踪玩家",
                Projectiles = new List<SpawnProjData>
                {
                    new SpawnProjData()
                    {
                        Condition = "默认配置",
                        Type = 1,
                        Damage = 25,
                        Stack = 6,
                        Interval = 10,
                        AngleCfg = "-30,30",
                        Velocity = 4,
                        Life = 300,
                        KnockBack = 4,
                        UpdProj = new List<string>(){ "强效追踪" }
                    }
                }
            },

            // 新增：爆炸弹幕
            new ProjData
            {
                Name = "爆炸弹幕",
                Description = "到达目标后爆炸",
                Projectiles = new List<SpawnProjData>
                {
                    new SpawnProjData()
                    {
                        Condition = "默认配置",
                        Type = 77,
                        Damage = 30,
                        Stack = 3,
                        Interval = 20,
                        Velocity = 15,
                        Life = 90,
                        KnockBack = 10,
                        LockRange = 20,
                        LockSpd = 12,
                        UpdProj = new List<string>(){ "爆炸更新" }
                    }
                }
            },

            // 新增：反弹弹幕
            new ProjData
            {
                Name = "反弹弹幕",
                Description = "碰到墙壁反弹",
                Projectiles = new List<SpawnProjData>
                {
                    new SpawnProjData()
                    {
                        Condition = "默认配置",
                        Type = 454,
                        Damage = 22,
                        Stack = 8,
                        Interval = 15,
                        AngleCfg = "0,360",
                        Velocity = 7,
                        Life = 240,
                        KnockBack = 8,
                        UpdProj = new List<string>(){ "反弹更新" }
                    }
                }
            }

            };

            foreach (var proj in defaults)
            {
                SaveFile(proj);
            }

            TShock.Log.ConsoleInfo($"[怪物加速] 已创建 {defaults.Count} 个生成弹幕配置文件");
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
