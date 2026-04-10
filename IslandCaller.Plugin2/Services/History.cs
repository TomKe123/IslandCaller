using IslandCaller.Models;
using System.Text;

namespace IslandCaller.Services
{
    public class HistoryService(ProfileService profileService, Status status)
    {
        public class HistorySnapshotItem
        {
            public string Name { get; set; } = string.Empty;
            public int LongTermCount { get; set; }
            public int SessionMissCount { get; set; }
            public int LastCallIndex { get; set; }
        }

        private readonly Dictionary<string, int> historyDict = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> top20List = [];
        private readonly Dictionary<string, int> recentCallIndex = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> lastSessionHitCallCount = new(StringComparer.OrdinalIgnoreCase);
        private int sessionCallCount;
        private int totalLongTermCallCount;
        private readonly ProfileService profileService = profileService;
        private readonly Status Status = status;

        private string GetBasePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "IslandCaller",
                "History"
            );
        }

        private string GetFilePath(Guid guid)
        {
            return Path.Combine(GetBasePath(), $"{guid}.txt");
        }

        // 载入长期历史（只加载 Dictionary）
        public void Load(Guid guid)
        {
            Status.HistoryServiceInitialized = false;
            historyDict.Clear();
            recentCallIndex.Clear();
            lastSessionHitCallCount.Clear();
            top20List.Clear();
            sessionCallCount = 0;
            totalLongTermCallCount = 0;

            string filePath = GetFilePath(guid);

            // 先构建一个 name → count 的临时字典，用于快速查找
            var csvDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(filePath))
            {
                var lines = File.ReadAllLines(filePath);

                foreach (var line in lines)
                {
                    var parts = line.Split(',');

                    if (parts.Length != 2)
                        continue;

                    string name = parts[0].Trim();

                    if (int.TryParse(parts[1], out int count))
                    {
                        // 只记录，不判断是否在 Members 中
                        csvDict[name] = count;
                    }
                }
            }

            // 以 Members 为基准加载
            foreach (var person in profileService.Members)
            {
                string name = person.Name;

                if (csvDict.TryGetValue(name, out int count))
                {
                    historyDict[name] = count;   // CSV 有 → 用 CSV 的值
                    totalLongTermCallCount += count;
                }
                else
                {
                    historyDict[name] = 0;       // CSV 没有 → 记为 0
                }

                lastSessionHitCallCount[name] = 0;
            }
            Status.HistoryServiceInitialized = true;
        }

        // 写入历史
        public void Add(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            EnsureMemberCounters();
            string normalizedName = name.Trim();

            // 1️ 更新 Dictionary（统计次数）
            if (historyDict.ContainsKey(normalizedName))
                historyDict[normalizedName]++;
            else
                historyDict[normalizedName] = 1;
            totalLongTermCallCount++;

            // 2️ 更新 top20（允许重复）
            sessionCallCount++;
            lastSessionHitCallCount[normalizedName] = sessionCallCount;
            top20List.Insert(0, normalizedName);

            if (top20List.Count > 20)
                top20List.RemoveAt(top20List.Count - 1);
            RebuildRecentCallIndex();

            // 3️自动保存
            Guid guid = Settings.Instance.Profile.DefaultProfile;
            Save(guid);
        }

        // 保存长期记录到本地
        private void Save(Guid guid)
        {
            string basePath = GetBasePath();
            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);

            string filePath = GetFilePath(guid);

            StringBuilder sb = new();

            foreach (var pair in historyDict)
            {
                sb.AppendLine($"{pair.Key},{pair.Value}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        // 获取长期记录中某个名字的出现次数
        public int GetLongTermCount(string name)
        {
            if (historyDict.TryGetValue(name, out int count))
                return count;

            return 0;
        }

        // 获取短期记录中的序号（1-based）
        // 不存在返回 -1
        public int GetLastCallIndex(string name)
        {
            return recentCallIndex.TryGetValue(name, out int index) ? index : -1;
        }

        public int GetSessionMissCount(string name)
        {
            EnsureMemberCounters();
            if (lastSessionHitCallCount.TryGetValue(name, out int lastHit))
                return sessionCallCount - lastHit;

            return 0;
        }

        public int GetSessionCallCount()
        {
            return sessionCallCount;
        }

        public void ResetSessionMissCounts(IEnumerable<string> names)
        {
            EnsureMemberCounters();

            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var key = name.Trim();
                if (lastSessionHitCallCount.ContainsKey(key))
                {
                    lastSessionHitCallCount[key] = sessionCallCount;
                }
            }
        }

        // 获取长期平均次数
        public double GetAverageLongTermCount()
        {
            if (historyDict.Count == 0)
                return 0;
            return (double)totalLongTermCallCount / historyDict.Count;
        }

        // 清空长期记录
        public void ClearLongTermHistory()
        {
            historyDict.Clear();
            totalLongTermCallCount = 0;
            foreach (var name in profileService.Members.Select(x => x.Name))
            {
                historyDict[name] = 0;
            }
            Save(Settings.Instance.Profile.DefaultProfile);
        }

        // 清空短期记录
        public void ClearThisLessonHistory()
        {
            top20List.Clear();
            recentCallIndex.Clear();
            lastSessionHitCallCount.Clear();
            sessionCallCount = 0;
            foreach (var name in profileService.Members.Select(x => x.Name))
            {
                lastSessionHitCallCount[name] = 0;
            }
        }

        public IReadOnlyList<HistorySnapshotItem> GetHistorySnapshot()
        {
            EnsureMemberCounters();

            return profileService.Members
                .OrderBy(x => x.Id)
                .Select(x => new HistorySnapshotItem
                {
                    Name = x.Name,
                    LongTermCount = GetLongTermCount(x.Name),
                    SessionMissCount = GetSessionMissCount(x.Name),
                    LastCallIndex = GetLastCallIndex(x.Name)
                })
                .ToList();
        }

        public int GetTotalLongTermCallCount()
        {
            return totalLongTermCallCount;
        }

        public IReadOnlyList<string> GetRecentCalls(int count)
        {
            if (count <= 0)
                return [];

            return top20List.Take(count).ToList();
        }

        private void EnsureMemberCounters()
        {
            foreach (var member in profileService.Members)
            {
                if (!historyDict.ContainsKey(member.Name))
                {
                    historyDict[member.Name] = 0;
                }

                if (!lastSessionHitCallCount.ContainsKey(member.Name))
                {
                    lastSessionHitCallCount[member.Name] = 0;
                }
            }
        }

        private void RebuildRecentCallIndex()
        {
            recentCallIndex.Clear();
            for (int i = 0; i < top20List.Count; i++)
            {
                string name = top20List[i];
                if (!recentCallIndex.ContainsKey(name))
                {
                    recentCallIndex[name] = i;
                }
            }
        }
    }
}
