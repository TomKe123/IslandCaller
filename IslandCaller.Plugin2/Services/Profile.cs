using ClassIsland.Shared;
using Microsoft.Extensions.Logging;
using IslandCaller.Models;
using System.Text;

namespace IslandCaller.Services
{
    public class ProfileService
    {
        public class Person
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Gender { get; set; }
            public double ManualWeight { get; set; } = 1.0; // 手动权重，默认为 1.0
            public GachaRarity Rarity { get; set; } = GachaRarity.ThreeStar;
            public bool IsFeatured { get; set; }
        }
        // 名单存储
        public List<Person> Members { get; set; } = new List<Person>();

        private string GetBasePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "IslandCaller",
                "Profile"
            );
        }

        private string GetFilePath(Guid guid)
        {
            return Path.Combine(GetBasePath(), $"{guid}.csv");
        }

        // 读取名单
        public void LoadSelectedProfile(Guid guid)
        {
            var logger = IAppHost.GetService<ILogger<ProfileService>>();
            var status = IAppHost.GetService<Status>();
            status.ProfileServiceInitialized = false;
            // 构建路径

            string filePath = Path.Combine(GetFilePath(guid));

            // 如果文件不存在
            if (!File.Exists(filePath))
            {
                logger.LogError($"找不到对应的名单文件: {filePath}");
                throw new FileNotFoundException($"找不到对应的名单文件: {filePath}");
            }

            string[] lines = File.ReadAllLines(filePath);

            if (lines.Length == 0)
            {
                logger.LogError("CSV 文件为空");
                throw new Exception("CSV 文件为空");
            }

            // 检查标题
            string header = lines[0].Trim();
            if (header != "id,name,gender,manualweight" && header != "id,name,gender,manualweight,rarity,isfeatured")
            {
                logger.LogError("CSV 标题格式错误，必须为: id,name,gender,manualweight 或 id,name,gender,manualweight,rarity,isfeatured");
                throw new Exception("CSV 标题格式错误，必须为: id,name,gender,manualweight 或 id,name,gender,manualweight,rarity,isfeatured");
            }

            Members.Clear();

            // 读取数据
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split(',');

                if (parts.Length is not 4 and not 6)
                {
                    logger.LogWarning($"第 {i + 1} 行格式错误: {line}");
                    continue;
                }

                var rarity = ParseRarity(parts.Length >= 5 ? parts[4] : null);
                var isFeatured = ParseFeatured(parts.Length >= 6 ? parts[5] : null);

                Members.Add(new Person
                {
                    Id = Convert.ToInt32(parts[0]),
                    Name = parts[1],
                    Gender = Convert.ToInt32(parts[2]),
                    ManualWeight = Convert.ToDouble(parts[3]),
                    Rarity = rarity,
                    IsFeatured = isFeatured
                });
            }
            Members = Members.OrderBy(x => x.Id).ToList();
            status.ProfileServiceInitialized = true;
        }

        // 获取名单
        public List<Person> GetMembers(Guid guid)
        {
            var logger = IAppHost.GetService<ILogger<ProfileService>>();
            string filePath = GetFilePath(guid);

            if (!File.Exists(filePath))
            {
                logger.LogError($"找不到对应的名单文件: {filePath}");
                throw new FileNotFoundException($"找不到对应的名单文件: {filePath}");
            }

            string[] lines = File.ReadAllLines(filePath);

            if (lines.Length == 0)
            {
                logger.LogError("CSV 文件为空");
                throw new Exception("CSV 文件为空");
            }

            if (lines[0].Trim() is not "id,name,gender,manualweight" and not "id,name,gender,manualweight,rarity,isfeatured")
            {
                logger.LogError("CSV 标题格式错误，必须为: id,name,gender,manualweight 或 id,name,gender,manualweight,rarity,isfeatured");
                throw new Exception("CSV 标题格式错误，必须为: id,name,gender,manualweight 或 id,name,gender,manualweight,rarity,isfeatured");
            }

            List<Person> members = new List<Person>();

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split(',');

                if (parts.Length is not 4 and not 6)
                {
                    logger.LogWarning($"第 {i + 1} 行格式错误: {line}");
                    continue;
                }

                var rarity = ParseRarity(parts.Length >= 5 ? parts[4] : null);
                var isFeatured = ParseFeatured(parts.Length >= 6 ? parts[5] : null);

                members.Add(new Person
                {
                    Id = Convert.ToInt32(parts[0]),
                    Name = parts[1],
                    Gender = Convert.ToInt32(parts[2]),
                    ManualWeight = Convert.ToDouble(parts[3]),
                    Rarity = rarity,
                    IsFeatured = isFeatured
                });
            }
            members = members.OrderBy(x => x.Id).ToList();
            return members;
        }

        // 写入名单（覆盖或创建）
        public void SaveProfile(Guid guid, List<Person> members)
        {
            var logger = IAppHost.GetService<ILogger<ProfileService>>();
            if (!SettingsWriteGate.CanModifyProtectedSettings())
            {
                throw new InvalidOperationException("当前未通过U盘验证，无法修改档案配置。");
            }

            members = members.OrderBy(x => x.Id).ToList();
            string basePath = GetBasePath();

            // 如果目录不存在就创建
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            string filePath = GetFilePath(guid);

            StringBuilder sb = new StringBuilder();

            // 写标题
            sb.AppendLine("id,name,gender,manualweight,rarity,isfeatured");

            foreach (var person in members)
            {
                sb.AppendLine($"{person.Id},{person.Name},{person.Gender},{person.ManualWeight},{(int)person.Rarity},{person.IsFeatured}");
            }

            // 覆盖写入
            try
            {
                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"写入名单文件失败: {filePath}");
                throw new Exception($"写入名单文件失败: {filePath}", ex);
            }
        }

        public void CreateDemoProfile(Guid guid)
        { 
            List<Person> members = new List<Person>();
            members.Add(new Person
            {
                Id = 1,
                Gender = 0,
                Name = "小明",
                ManualWeight = 1.0,
                Rarity = GachaRarity.FiveStar,
                IsFeatured = true
            });
            members.Add(new Person
            {
                Id = 2,
                Gender = 0,
                Name = "李明",
                ManualWeight = 1.0,
                Rarity = GachaRarity.FourStar,
                IsFeatured = true
            });
            members.Add(new Person
            {
                Id = 3,
                Gender = 1,
                Name = "李华",
                ManualWeight = 1.0,
                Rarity = GachaRarity.ThreeStar,
                IsFeatured = false
            });
            SaveProfile(guid, members);
        }

        private static GachaRarity ParseRarity(string? raw)
        {
            if (int.TryParse(raw, out int value) && Enum.IsDefined(typeof(GachaRarity), value))
            {
                return (GachaRarity)value;
            }

            return GachaRarity.ThreeStar;
        }

        private static bool ParseFeatured(string? raw)
        {
            if (bool.TryParse(raw, out bool value))
            {
                return value;
            }

            return false;
        }
    }
}
