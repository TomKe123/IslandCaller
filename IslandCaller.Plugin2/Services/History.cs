using System.Text;
using System.Text.Json;
using IslandCaller.Models;

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

        public sealed class CallRecordItem
        {
            public int Index { get; init; }
            public string Name { get; init; } = string.Empty;
            public DateTime OccurredAt { get; init; }
        }

        private readonly Dictionary<string, int> historyDict = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> top20List = [];
        private readonly Dictionary<string, int> recentCallIndex = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> lastSessionHitCallCount = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<CallRecordItem> allCallRecords = [];
        private int sessionCallCount;
        private int totalLongTermCallCount;
        private GachaPityState gachaPityState = new();
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

        private string GetGachaStatePath(Guid guid)
        {
            return Path.Combine(GetBasePath(), $"{guid}.gacha.json");
        }

        private string GetCallRecordsPath(Guid guid)
        {
            return Path.Combine(GetBasePath(), $"{guid}.records.json");
        }

        public void Load(Guid guid)
        {
            Status.HistoryServiceInitialized = false;
            historyDict.Clear();
            recentCallIndex.Clear();
            lastSessionHitCallCount.Clear();
            top20List.Clear();
            allCallRecords.Clear();
            sessionCallCount = 0;
            totalLongTermCallCount = 0;
            gachaPityState = LoadGachaState(guid);

            string filePath = GetFilePath(guid);
            var csvDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(filePath))
            {
                foreach (var line in File.ReadAllLines(filePath))
                {
                    var parts = line.Split(',');
                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    string name = parts[0].Trim();
                    if (int.TryParse(parts[1], out int count))
                    {
                        csvDict[name] = count;
                    }
                }
            }

            foreach (var person in profileService.Members)
            {
                string name = person.Name;
                if (csvDict.TryGetValue(name, out int count))
                {
                    historyDict[name] = count;
                    totalLongTermCallCount += count;
                }
                else
                {
                    historyDict[name] = 0;
                }

                lastSessionHitCallCount[name] = 0;
            }

            allCallRecords.AddRange(LoadCallRecords(guid));
            Status.HistoryServiceInitialized = true;
        }

        public void Add(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            EnsureMemberCounters();
            string normalizedName = name.Trim();

            if (historyDict.ContainsKey(normalizedName))
            {
                historyDict[normalizedName]++;
            }
            else
            {
                historyDict[normalizedName] = 1;
            }

            totalLongTermCallCount++;
            sessionCallCount++;
            lastSessionHitCallCount[normalizedName] = sessionCallCount;
            top20List.Insert(0, normalizedName);
            allCallRecords.Add(new CallRecordItem
            {
                Index = allCallRecords.Count + 1,
                Name = normalizedName,
                OccurredAt = DateTime.Now
            });

            if (top20List.Count > 20)
            {
                top20List.RemoveAt(top20List.Count - 1);
            }

            RebuildRecentCallIndex();
            Save(Settings.Instance.Profile.DefaultProfile);
        }

        private void Save(Guid guid)
        {
            string basePath = GetBasePath();
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            string filePath = GetFilePath(guid);
            StringBuilder sb = new();
            foreach (var pair in historyDict)
            {
                sb.AppendLine($"{pair.Key},{pair.Value}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            SaveGachaState(guid);
            SaveCallRecords(guid);
        }

        public int GetLongTermCount(string name)
        {
            return historyDict.TryGetValue(name, out int count) ? count : 0;
        }

        public int GetLastCallIndex(string name)
        {
            return recentCallIndex.TryGetValue(name, out int index) ? index : -1;
        }

        public int GetSessionMissCount(string name)
        {
            EnsureMemberCounters();
            return lastSessionHitCallCount.TryGetValue(name, out int lastHit)
                ? sessionCallCount - lastHit
                : 0;
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

                string key = name.Trim();
                if (lastSessionHitCallCount.ContainsKey(key))
                {
                    lastSessionHitCallCount[key] = sessionCallCount;
                }
            }
        }

        public double GetAverageLongTermCount()
        {
            return historyDict.Count == 0 ? 0 : (double)totalLongTermCallCount / historyDict.Count;
        }

        public void ClearLongTermHistory()
        {
            historyDict.Clear();
            totalLongTermCallCount = 0;
            gachaPityState = new GachaPityState();
            allCallRecords.Clear();
            foreach (var name in profileService.Members.Select(x => x.Name))
            {
                historyDict[name] = 0;
            }

            Save(Settings.Instance.Profile.DefaultProfile);
        }

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
            return count <= 0 ? [] : top20List.Take(count).ToList();
        }

        public IReadOnlyList<CallRecordItem> GetAllCallRecords()
        {
            return allCallRecords.ToList();
        }

        public GachaPityState GetGachaPityState()
        {
            return new GachaPityState
            {
                FiveStarPity = gachaPityState.FiveStarPity,
                FourStarPity = gachaPityState.FourStarPity,
                IsFiveStarFeaturedGuaranteed = gachaPityState.IsFiveStarFeaturedGuaranteed,
                IsFourStarFeaturedGuaranteed = gachaPityState.IsFourStarFeaturedGuaranteed,
                CapturedRadianceCount = gachaPityState.CapturedRadianceCount,
                TotalDrawCount = gachaPityState.TotalDrawCount,
                BannerDate = gachaPityState.BannerDate,
                FeaturedFiveStarName = gachaPityState.FeaturedFiveStarName,
                FeaturedFourStarNames = [.. gachaPityState.FeaturedFourStarNames]
            };
        }

        public void UpdateGachaPityState(GachaPityState state)
        {
            gachaPityState = new GachaPityState
            {
                FiveStarPity = Math.Max(0, state.FiveStarPity),
                FourStarPity = Math.Max(0, state.FourStarPity),
                IsFiveStarFeaturedGuaranteed = state.IsFiveStarFeaturedGuaranteed,
                IsFourStarFeaturedGuaranteed = state.IsFourStarFeaturedGuaranteed,
                CapturedRadianceCount = Math.Max(0, state.CapturedRadianceCount),
                TotalDrawCount = Math.Max(0, state.TotalDrawCount),
                BannerDate = state.BannerDate ?? string.Empty,
                FeaturedFiveStarName = state.FeaturedFiveStarName ?? string.Empty,
                FeaturedFourStarNames = state.FeaturedFourStarNames?
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? []
            };
            SaveGachaState(Settings.Instance.Profile.DefaultProfile);
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

        private GachaPityState LoadGachaState(Guid guid)
        {
            string path = GetGachaStatePath(guid);
            if (!File.Exists(path))
            {
                return new GachaPityState();
            }

            try
            {
                return JsonSerializer.Deserialize<GachaPityState>(File.ReadAllText(path, Encoding.UTF8)) ?? new GachaPityState();
            }
            catch
            {
                return new GachaPityState();
            }
        }

        private void SaveGachaState(Guid guid)
        {
            string basePath = GetBasePath();
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            string path = GetGachaStatePath(guid);
            File.WriteAllText(path, JsonSerializer.Serialize(gachaPityState), Encoding.UTF8);
        }

        private IReadOnlyList<CallRecordItem> LoadCallRecords(Guid guid)
        {
            string path = GetCallRecordsPath(guid);
            if (!File.Exists(path))
            {
                return [];
            }

            try
            {
                var records = JsonSerializer.Deserialize<List<CallRecordItem>>(File.ReadAllText(path, Encoding.UTF8)) ?? [];
                return records
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                    .Select((x, index) => new CallRecordItem
                    {
                        Index = index + 1,
                        Name = x.Name.Trim(),
                        OccurredAt = x.OccurredAt
                    })
                    .ToList();
            }
            catch
            {
                return [];
            }
        }

        private void SaveCallRecords(Guid guid)
        {
            string basePath = GetBasePath();
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            string path = GetCallRecordsPath(guid);
            File.WriteAllText(path, JsonSerializer.Serialize(allCallRecords), Encoding.UTF8);
        }
    }
}
