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

        private Dictionary<string, int> historyDict = new();
        private List<string> top20List = new();
        private Dictionary<string, int> sessionMissCount = new();
        private int sessionCallCount = 0;
        private ProfileService profileService = profileService;
        private Status Status = status;

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
            sessionMissCount.Clear();
            sessionCallCount = 0;

            string filePath = GetFilePath(guid);

            // 先构建一个 name → count 的临时字典，用于快速查找
            var csvDict = new Dictionary<string, int>();

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
                }
                else
                {
                    historyDict[name] = 0;       // CSV 没有 → 记为 0
                }

                sessionMissCount[name] = 0;
            }
            Status.HistoryServiceInitialized = true;
        }

        // 写入历史
        public void Add(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            EnsureMemberCounters();

            // 1️ 更新 Dictionary（统计次数）
            if (historyDict.ContainsKey(name))
                historyDict[name]++;
            else
                historyDict[name] = 1;

            foreach (var key in sessionMissCount.Keys.ToList())
            {
                sessionMissCount[key]++;
            }

            sessionMissCount[name] = 0;

            // 2️ 更新 top20（允许重复）
            top20List.Insert(0, name);
            sessionCallCount++;

            if (top20List.Count > 20)
                top20List.RemoveAt(top20List.Count - 1);

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
            int index = top20List.IndexOf(name);
            return index;
        }

        public int GetSessionMissCount(string name)
        {
            if (sessionMissCount.TryGetValue(name, out int count))
                return count;

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
                if (sessionMissCount.ContainsKey(key))
                {
                    sessionMissCount[key] = 0;
                }
            }
        }

        // 获取长期平均次数
        public double GetAverageLongTermCount()
        {
            if (historyDict.Count == 0)
                return 0;
            return historyDict.Values.Average();
        }

        // 清空长期记录
        public void ClearLongTermHistory()
        {
            historyDict.Clear();
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
            sessionMissCount.Clear();
            sessionCallCount = 0;
            foreach (var name in profileService.Members.Select(x => x.Name))
            {
                sessionMissCount[name] = 0;
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
            return historyDict.Values.Sum();
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

                if (!sessionMissCount.ContainsKey(member.Name))
                {
                    sessionMissCount[member.Name] = 0;
                }
            }
        }
    }
}