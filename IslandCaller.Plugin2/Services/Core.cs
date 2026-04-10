using System.ComponentModel;
using System.Security.Cryptography;
using System.Text.Json;
using IslandCaller.Models;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Services
{
    public class CoreService
    {
        private readonly ProfileService profileService;
        private readonly HistoryService historyService;
        private readonly ILogger<CoreService> logger;
        private readonly Status status;
        private const double MaxPacerWeightBoost = 1.15;
        private bool settingsCacheDirty = true;
        private GeneralSettingsSnapshot settingsSnapshot = GeneralSettingsSnapshot.Empty;

        public CoreService(ProfileService profileService, HistoryService historyService, ILogger<CoreService> logger, Status status)
        {
            this.profileService = profileService;
            this.historyService = historyService;
            this.logger = logger;
            this.status = status;
            Settings.Instance.General.PropertyChanged += OnGeneralSettingsChanged;
        }

        internal enum DrawType
        {
            Normal,
            Pacer,
            Guarantee
        }

        internal readonly record struct DrawResult(string Name, DrawType Type);

        internal class Person
        {
            internal int Id { get; set; }
            internal string Name { get; set; } = string.Empty;
            internal string NormalizedName { get; set; } = string.Empty;
            internal int Gender { get; set; }
            internal double ManualWeight { get; set; } = 1.0;
            internal double Weight { get; set; }
        }

        internal List<Person> Persons { get; } = [];

        public IEnumerable<string> StudentNames => Persons.Select(p => p.Name);

        internal void InitializeCore()
        {
            status.IsTimeStatusAvailable = false;
            logger.LogInformation("初始化 Core 模块，加载学生信息...");
            Persons.Clear();
            foreach (var person in profileService.Members)
            {
                Persons.Add(new Person
                {
                    Id = person.Id,
                    Name = person.Name,
                    NormalizedName = NormalizeName(person.Name),
                    Gender = person.Gender,
                    ManualWeight = person.ManualWeight,
                    Weight = 0.0
                });
            }

            ComputeWeightsForAllStudents();
            status.CoreServiceInitialized = true;
        }

        private double ComputeSingleWeight(double manualWeight, int lastHitStep, int nHist, double avgHist)
        {
            const double fMin = 0;
            const double beta = 0.54;

            int deltaS = lastHitStep;
            if (deltaS < 0)
            {
                deltaS = 15;
            }

            double sessionFactor = 1 - (1 - fMin) * Math.Exp(-beta * deltaS);

            const double eps = 1.0;
            const double gamma = 0.9;
            const double rMin = 0.6;
            const double rMax = 1.6;

            double ratio = (manualWeight * avgHist + eps) / (nHist + eps);
            double historyFactor = Math.Pow(ratio, gamma);
            historyFactor = Math.Max(rMin, Math.Min(rMax, historyFactor));

            return manualWeight * sessionFactor * historyFactor;
        }

        private void ComputeWeightsForAllStudents()
        {
            double avgHist = historyService.GetAverageLongTermCount();
            var settings = GetSettingsSnapshot();
            double dynamicGuaranteeBoost = settings.EnableGuarantee ? GetDynamicGuaranteeBoost(settings.GuaranteeThreshold) : 1.0;
            double dynamicPacerBoost = settings.EnableGuarantee ? GetDynamicPacerBoost(settings.PacerThreshold) : 1.0;

            logger.LogTrace("计算全班历史平均被点次数: {Average}", avgHist);
            foreach (var person in Persons)
            {
                int nHist = historyService.GetLongTermCount(person.Name);
                int lastHitStep = historyService.GetLastCallIndex(person.Name);
                double weight = ComputeSingleWeight(person.ManualWeight, lastHitStep, nHist, avgHist);

                if (settings.GuaranteeWeights.TryGetValue(person.NormalizedName, out var guaranteeWeight))
                {
                    weight *= guaranteeWeight * dynamicGuaranteeBoost;
                }
                else if (settings.PacerNames.Contains(person.NormalizedName))
                {
                    weight *= dynamicPacerBoost;
                }

                person.Weight = weight;
                logger.LogTrace("计算权重 - 学生: {Name}, ManualWeight: {ManualWeight}, LastHitStep: {LastHitStep}, nHist: {HistoryCount}, Weight: {Weight}",
                    person.Name, person.ManualWeight, lastHitStep, nHist, weight);
            }
        }

        private double GetDynamicGuaranteeBoost(int threshold)
        {
            int sessionCallCount = historyService.GetSessionCallCount();
            double boost = 1.0 + (double)sessionCallCount / threshold;
            return Math.Min(4.0, boost);
        }

        private double GetDynamicPacerBoost(int threshold)
        {
            int sessionCallCount = historyService.GetSessionCallCount();
            double progress = Math.Min(1.0, (double)sessionCallCount / threshold);
            return 1.0 + (MaxPacerWeightBoost - 1.0) * progress;
        }

        internal string GetRandomStudent()
        {
            return GetRandomStudentResult().Name;
        }

        internal DrawResult GetRandomStudentResult()
        {
            var settings = GetSettingsSnapshot();
            var guaranteed = GetGuaranteedCandidate(settings);
            if (guaranteed != null)
            {
                int sessionMissBeforeHit = historyService.GetSessionMissCount(guaranteed.Name);
                historyService.Add(guaranteed.Name);
                RestartGuaranteeCycleIfEarlyHit(guaranteed.Name, settings.GuaranteeWeights, settings.GuaranteeThreshold, sessionMissBeforeHit, settings.EnableGuarantee);
                logger.LogTrace("保底命中：{Name}, SessionMiss={SessionMiss}", guaranteed.Name, historyService.GetSessionMissCount(guaranteed.Name));
                ComputeWeightsForAllStudents();
                return new DrawResult(guaranteed.Name, DrawType.Guarantee);
            }

            var pacerGuaranteed = GetPacerGuaranteedCandidate(settings);
            if (pacerGuaranteed != null)
            {
                historyService.Add(pacerGuaranteed.Name);
                logger.LogTrace("陪跑保底命中：{Name}, SessionMiss={SessionMiss}", pacerGuaranteed.Name, historyService.GetSessionMissCount(pacerGuaranteed.Name));
                ComputeWeightsForAllStudents();
                return new DrawResult(pacerGuaranteed.Name, DrawType.Pacer);
            }

            double totalWeight = Persons.Sum(p => p.Weight);
            logger.LogTrace("计算权重总和: {TotalWeight}", totalWeight);
            if (totalWeight <= 0)
            {
                return new DrawResult("Error", DrawType.Normal);
            }

            double r = GetTrueRandomDouble() * totalWeight;
            logger.LogTrace("生成随机数: {Random} (范围: [0, {TotalWeight}))", r, totalWeight);

            double cumulative = 0;
            foreach (var person in Persons)
            {
                cumulative += person.Weight;
                if (r < cumulative)
                {
                    int sessionMissBeforeHit = historyService.GetSessionMissCount(person.Name);
                    historyService.Add(person.Name);
                    RestartGuaranteeCycleIfEarlyHit(person.Name, settings.GuaranteeWeights, settings.GuaranteeThreshold, sessionMissBeforeHit, settings.EnableGuarantee);
                    logger.LogTrace("抽取到学生：{Name}", person.Name);
                    ComputeWeightsForAllStudents();
                    DrawType type = GetDrawType(person.NormalizedName, settings.GuaranteeWeights, settings.PacerNames);
                    return new DrawResult(person.Name, type);
                }
            }

            logger.LogWarning("随机选择学生时发生了意外情况，权重总和: {TotalWeight}, 随机数: {Random}", totalWeight, r);
            return new DrawResult("Error", DrawType.Normal);
        }

        private static DrawType GetDrawType(string normalizedName, Dictionary<string, double> guaranteeWeightMap, HashSet<string> pacerNameSet)
        {
            if (guaranteeWeightMap.ContainsKey(normalizedName))
            {
                return DrawType.Guarantee;
            }

            if (pacerNameSet.Contains(normalizedName))
            {
                return DrawType.Pacer;
            }

            return DrawType.Normal;
        }

        private Person? GetGuaranteedCandidate(GeneralSettingsSnapshot settings)
        {
            if (!settings.EnableGuarantee || settings.GuaranteeWeights.Count == 0)
            {
                return null;
            }

            var candidates = new List<(Person Person, double Weight)>();
            int maxMiss = -1;
            double totalWeight = 0;

            foreach (var person in Persons)
            {
                if (!settings.GuaranteeWeights.TryGetValue(person.NormalizedName, out var guaranteeWeight))
                {
                    continue;
                }

                int missCount = historyService.GetSessionMissCount(person.Name);
                if (missCount < settings.GuaranteeThreshold)
                {
                    continue;
                }

                double candidateWeight = Math.Max(0.01, person.Weight * guaranteeWeight);
                if (missCount > maxMiss)
                {
                    maxMiss = missCount;
                    totalWeight = 0;
                    candidates.Clear();
                }

                if (missCount == maxMiss)
                {
                    candidates.Add((person, candidateWeight));
                    totalWeight += candidateWeight;
                }
            }

            return PickCandidate(candidates, totalWeight);
        }

        private Person? GetPacerGuaranteedCandidate(GeneralSettingsSnapshot settings)
        {
            if (!settings.EnableGuarantee || settings.PacerNames.Count == 0)
            {
                return null;
            }

            var candidates = new List<(Person Person, double Weight)>();
            int maxMiss = -1;
            double totalWeight = 0;
            double pacerBoost = GetDynamicPacerBoost(settings.PacerThreshold);

            foreach (var person in Persons)
            {
                if (!settings.PacerNames.Contains(person.NormalizedName))
                {
                    continue;
                }

                int missCount = historyService.GetSessionMissCount(person.Name);
                if (missCount < settings.PacerThreshold)
                {
                    continue;
                }

                double candidateWeight = Math.Max(0.01, person.Weight * pacerBoost);
                if (missCount > maxMiss)
                {
                    maxMiss = missCount;
                    totalWeight = 0;
                    candidates.Clear();
                }

                if (missCount == maxMiss)
                {
                    candidates.Add((person, candidateWeight));
                    totalWeight += candidateWeight;
                }
            }

            return PickCandidate(candidates, totalWeight);
        }

        private void RestartGuaranteeCycleIfEarlyHit(string selectedName, Dictionary<string, double> guaranteeWeightMap, int threshold, int? sessionMissBeforeHit, bool enableGuarantee)
        {
            if (!enableGuarantee || guaranteeWeightMap.Count == 0)
            {
                return;
            }

            string normalizedSelectedName = NormalizeName(selectedName);
            if (!guaranteeWeightMap.ContainsKey(normalizedSelectedName))
            {
                return;
            }

            int missBeforeHit = sessionMissBeforeHit ?? historyService.GetSessionMissCount(selectedName);
            if (missBeforeHit >= threshold)
            {
                return;
            }

            var guaranteeRawNames = Persons
                .Where(p => guaranteeWeightMap.ContainsKey(p.NormalizedName))
                .Select(p => p.Name)
                .ToList();

            historyService.ResetSessionMissCounts(guaranteeRawNames);
            logger.LogTrace("保底提前触发后重置计数：{Name}, MissBeforeHit={MissBeforeHit}, Threshold={Threshold}", selectedName, missBeforeHit, threshold);
        }

        private void OnGeneralSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(GeneralSetting.EnableGuarantee)
                or nameof(GeneralSetting.GuaranteeThreshold)
                or nameof(GeneralSetting.GuaranteeListText)
                or nameof(GeneralSetting.GuaranteeWeightListJson)
                or nameof(GeneralSetting.PacerListJson)
                or nameof(GeneralSetting.PacerThreshold))
            {
                settingsCacheDirty = true;
                ComputeWeightsForAllStudents();
            }
        }

        private GeneralSettingsSnapshot GetSettingsSnapshot()
        {
            if (!settingsCacheDirty)
            {
                return settingsSnapshot;
            }

            bool enableGuarantee = Settings.Instance.General.EnableGuarantee;
            var guaranteeWeights = enableGuarantee
                ? ParseGuaranteeWeightMap()
                : new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var pacerNames = enableGuarantee
                ? ParsePacerNameSet()
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (enableGuarantee && guaranteeWeights.Count > 0)
            {
                pacerNames.ExceptWith(guaranteeWeights.Keys);
            }

            settingsSnapshot = new GeneralSettingsSnapshot(
                enableGuarantee,
                Math.Max(1, Settings.Instance.General.GuaranteeThreshold),
                Math.Max(1, Settings.Instance.General.PacerThreshold),
                guaranteeWeights,
                pacerNames);
            settingsCacheDirty = false;
            return settingsSnapshot;
        }

        private static Dictionary<string, double> ParseGuaranteeWeightMap()
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<GuaranteeWeightItem>>(Settings.Instance.General.GuaranteeWeightListJson ?? "[]") ?? [];
                var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in items)
                {
                    string normalizedName = NormalizeName(item.Name);
                    if (string.IsNullOrWhiteSpace(normalizedName))
                    {
                        continue;
                    }

                    result[normalizedName] = Math.Max(0.01, item.Weight);
                }

                if (result.Count > 0)
                {
                    return result;
                }
            }
            catch
            {
                // Fallback to legacy plain-text list when json is invalid.
            }

            return ParseGuaranteeNames(Settings.Instance.General.GuaranteeListText)
                .ToDictionary(x => x, _ => 1.0, StringComparer.OrdinalIgnoreCase);
        }

        private static HashSet<string> ParsePacerNameSet()
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<string>>(Settings.Instance.General.PacerListJson ?? "[]") ?? [];
                return items
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(NormalizeName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return ParseGuaranteeNames(Settings.Instance.General.PacerListJson);
            }
        }

        private sealed class GuaranteeWeightItem
        {
            public string Name { get; set; } = string.Empty;
            public double Weight { get; set; } = 1.0;
        }

        private static HashSet<string> ParseGuaranteeNames(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return [];
            }

            return rawText
                .Split([',', '，', '\n', '\r', ' ', '\t', ';', '；', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizeName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeName(string? rawName)
        {
            return rawName?.Trim() ?? string.Empty;
        }

        private static Person? PickCandidate(List<(Person Person, double Weight)> candidates, double totalWeight)
        {
            if (candidates.Count == 0)
            {
                return null;
            }

            if (totalWeight <= 0)
            {
                return candidates.MinBy(x => x.Person.Id).Person;
            }

            double r = GetTrueRandomDouble() * totalWeight;
            double cumulative = 0;
            foreach (var candidate in candidates)
            {
                cumulative += candidate.Weight;
                if (r < cumulative)
                {
                    return candidate.Person;
                }
            }

            return candidates.MinBy(x => x.Person.Id).Person;
        }

        private sealed record GeneralSettingsSnapshot(
            bool EnableGuarantee,
            int GuaranteeThreshold,
            int PacerThreshold,
            Dictionary<string, double> GuaranteeWeights,
            HashSet<string> PacerNames)
        {
            internal static readonly GeneralSettingsSnapshot Empty =
                new(false, 1, 1, new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        private static double GetTrueRandomDouble()
        {
            Span<byte> bytes = stackalloc byte[8];
            RandomNumberGenerator.Fill(bytes);
            ulong value = BitConverter.ToUInt64(bytes);
            return (value >> 11) * (1.0 / 9007199254740992.0);
        }
    }
}
