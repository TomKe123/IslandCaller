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
        private GeneralSettingsSnapshot generalSettingsSnapshot = GeneralSettingsSnapshot.Empty;
        private GachaSettingsSnapshot gachaSettingsSnapshot = GachaSettingsSnapshot.Empty;

        public CoreService(ProfileService profileService, HistoryService historyService, ILogger<CoreService> logger, Status status)
        {
            this.profileService = profileService;
            this.historyService = historyService;
            this.logger = logger;
            this.status = status;
            Settings.Instance.General.PropertyChanged += OnSettingsChanged;
            Settings.Instance.Gacha.PropertyChanged += OnSettingsChanged;
        }

        internal enum DrawType
        {
            Normal,
            Pacer,
            Guarantee,
            ThreeStar,
            FourStar,
            FeaturedFourStar,
            FiveStar,
            FeaturedFiveStar,
            CapturedRadiance
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
            internal GachaRarity Rarity { get; set; } = GachaRarity.ThreeStar;
            internal bool IsFeatured { get; set; }
        }

        internal List<Person> Persons { get; } = [];

        public IEnumerable<string> StudentNames => Persons.Select(p => p.Name);

        internal void InitializeCore()
        {
            status.IsTimeStatusAvailable = false;
            logger.LogInformation("Initializing Core service");
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
                    Weight = 0.0,
                    Rarity = person.Rarity,
                    IsFeatured = person.IsFeatured
                });
            }

            ComputeWeightsForAllStudents();
            status.CoreServiceInitialized = true;
        }

        internal string GetRandomStudent()
        {
            return GetRandomStudentResult().Name;
        }

        internal DrawResult GetRandomStudentResult()
        {
            if (GetGachaSettingsSnapshot().Enabled)
            {
                return GetRandomCharacterPoolResult();
            }

            return GetClassicRandomStudentResult();
        }

        private DrawResult GetClassicRandomStudentResult()
        {
            var settings = GetGeneralSettingsSnapshot();
            var guaranteed = GetGuaranteedCandidate(settings);
            if (guaranteed != null)
            {
                int sessionMissBeforeHit = historyService.GetSessionMissCount(guaranteed.Name);
                historyService.Add(guaranteed.Name);
                RestartGuaranteeCycleIfEarlyHit(guaranteed.Name, settings.GuaranteeWeights, settings.GuaranteeThreshold, sessionMissBeforeHit, settings.EnableGuarantee);
                ComputeWeightsForAllStudents();
                return new DrawResult(guaranteed.Name, DrawType.Guarantee);
            }

            var pacerGuaranteed = GetPacerGuaranteedCandidate(settings);
            if (pacerGuaranteed != null)
            {
                historyService.Add(pacerGuaranteed.Name);
                ComputeWeightsForAllStudents();
                return new DrawResult(pacerGuaranteed.Name, DrawType.Pacer);
            }

            double totalWeight = Persons.Sum(p => p.Weight);
            if (totalWeight <= 0)
            {
                return new DrawResult("Error", DrawType.Normal);
            }

            double r = GetTrueRandomDouble() * totalWeight;
            double cumulative = 0;
            foreach (var person in Persons)
            {
                cumulative += person.Weight;
                if (r < cumulative)
                {
                    int sessionMissBeforeHit = historyService.GetSessionMissCount(person.Name);
                    historyService.Add(person.Name);
                    RestartGuaranteeCycleIfEarlyHit(person.Name, settings.GuaranteeWeights, settings.GuaranteeThreshold, sessionMissBeforeHit, settings.EnableGuarantee);
                    ComputeWeightsForAllStudents();
                    return new DrawResult(person.Name, GetClassicDrawType(person.NormalizedName, settings.GuaranteeWeights, settings.PacerNames));
                }
            }

            logger.LogWarning("Classic draw failed to resolve a student");
            return new DrawResult("Error", DrawType.Normal);
        }

        private DrawResult GetRandomCharacterPoolResult()
        {
            var settings = GetGachaSettingsSnapshot();
            if (Persons.Count == 0)
            {
                return new DrawResult("Error", DrawType.Normal);
            }

            var pityState = historyService.GetGachaPityState();
            pityState.TotalDrawCount++;

            bool isFiveStar = RollByPity(pityState.FiveStarPity, settings.FiveStarBaseRate, settings.FiveStarSoftPityStart, settings.FiveStarHardPity, settings.FiveStarSoftPityStep);
            if (isFiveStar)
            {
                pityState.FiveStarPity = 0;
                pityState.FourStarPity = 0;
                bool capturedRadianceHit = pityState.IsFiveStarFeaturedGuaranteed;
                bool featuredHit = capturedRadianceHit || RollChance(settings.FiveStarFeaturedRate);
                var person = PickGachaPerson(GachaRarity.FiveStar, featuredHit);
                bool selectedIsFeatured = person?.IsFeatured ?? false;
                pityState.IsFiveStarFeaturedGuaranteed = !selectedIsFeatured;
                historyService.UpdateGachaPityState(pityState);
                DrawType fiveStarType = selectedIsFeatured
                    ? (capturedRadianceHit ? DrawType.CapturedRadiance : DrawType.FeaturedFiveStar)
                    : DrawType.FiveStar;
                return FinalizeDraw(person, fiveStarType);
            }

            bool isFourStar = RollByPity(pityState.FourStarPity, settings.FourStarBaseRate, settings.FourStarSoftPityStart, settings.FourStarHardPity, settings.FourStarSoftPityStep);
            if (isFourStar)
            {
                pityState.FiveStarPity++;
                pityState.FourStarPity = 0;
                bool featuredHit = pityState.IsFourStarFeaturedGuaranteed || RollChance(settings.FourStarFeaturedRate);
                var person = PickGachaPerson(GachaRarity.FourStar, featuredHit);
                bool selectedIsFeatured = person?.IsFeatured ?? false;
                pityState.IsFourStarFeaturedGuaranteed = !selectedIsFeatured;
                historyService.UpdateGachaPityState(pityState);
                return FinalizeDraw(person, selectedIsFeatured ? DrawType.FeaturedFourStar : DrawType.FourStar);
            }

            pityState.FiveStarPity++;
            pityState.FourStarPity++;
            historyService.UpdateGachaPityState(pityState);
            return FinalizeDraw(PickGachaPerson(GachaRarity.ThreeStar, preferFeatured: false), DrawType.ThreeStar);
        }

        private DrawResult FinalizeDraw(Person? person, DrawType fallbackType)
        {
            if (person == null)
            {
                return new DrawResult("Error", DrawType.Normal);
            }

            historyService.Add(person.Name);
            return new DrawResult(person.Name, GetGachaDrawType(person, fallbackType));
        }

        private Person? PickGachaPerson(GachaRarity rarity, bool preferFeatured)
        {
            IEnumerable<Person> matching = Persons.Where(p => p.Rarity == rarity && p.IsFeatured == preferFeatured);
            var person = PickWeightedPerson(matching.ToList());
            if (person != null)
            {
                return person;
            }

            matching = Persons.Where(p => p.Rarity == rarity);
            person = PickWeightedPerson(matching.ToList());
            if (person != null)
            {
                return person;
            }

            return PickWeightedPerson(Persons);
        }

        private Person? PickWeightedPerson(IReadOnlyList<Person> persons)
        {
            if (persons.Count == 0)
            {
                return null;
            }

            double totalWeight = persons.Sum(x => Math.Max(0.01, x.ManualWeight));
            if (totalWeight <= 0)
            {
                return persons.OrderBy(x => x.Id).FirstOrDefault();
            }

            double r = GetTrueRandomDouble() * totalWeight;
            double cumulative = 0;
            foreach (var person in persons)
            {
                cumulative += Math.Max(0.01, person.ManualWeight);
                if (r < cumulative)
                {
                    return person;
                }
            }

            return persons.OrderBy(x => x.Id).FirstOrDefault();
        }

        private static DrawType GetGachaDrawType(Person person, DrawType fallbackType)
        {
            return person.Rarity switch
            {
                GachaRarity.FiveStar when person.IsFeatured => DrawType.FeaturedFiveStar,
                GachaRarity.FiveStar => DrawType.FiveStar,
                GachaRarity.FourStar when person.IsFeatured => DrawType.FeaturedFourStar,
                GachaRarity.FourStar => DrawType.FourStar,
                GachaRarity.ThreeStar => DrawType.ThreeStar,
                _ => fallbackType
            };
        }

        private static bool RollByPity(int pityCount, double baseRate, int softPityStart, int hardPity, double softPityStep)
        {
            int nextPullNumber = pityCount + 1;
            if (nextPullNumber >= hardPity)
            {
                return true;
            }

            double rate = baseRate;
            if (nextPullNumber >= softPityStart)
            {
                rate += (nextPullNumber - softPityStart + 1) * softPityStep;
            }

            rate = Math.Clamp(rate, 0.0, 1.0);
            return RollChance(rate);
        }

        private static bool RollChance(double chance)
        {
            return GetTrueRandomDouble() < Math.Clamp(chance, 0.0, 1.0);
        }

        private double ComputeSingleWeight(double manualWeight, int lastHitStep, int nHist, double avgHist)
        {
            const double fMin = 0;
            const double beta = 0.54;

            int deltaS = lastHitStep < 0 ? 15 : lastHitStep;
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
            if (GetGachaSettingsSnapshot().Enabled)
            {
                foreach (var person in Persons)
                {
                    person.Weight = Math.Max(0.01, person.ManualWeight);
                }
                return;
            }

            double avgHist = historyService.GetAverageLongTermCount();
            var settings = GetGeneralSettingsSnapshot();
            double dynamicGuaranteeBoost = settings.EnableGuarantee ? GetDynamicGuaranteeBoost(settings.GuaranteeThreshold) : 1.0;
            double dynamicPacerBoost = settings.EnableGuarantee ? GetDynamicPacerBoost(settings.PacerThreshold) : 1.0;

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

        private static DrawType GetClassicDrawType(string normalizedName, Dictionary<string, double> guaranteeWeightMap, HashSet<string> pacerNameSet)
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
        }

        private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            settingsCacheDirty = true;
            ComputeWeightsForAllStudents();
        }

        private GeneralSettingsSnapshot GetGeneralSettingsSnapshot()
        {
            EnsureSettingsSnapshots();
            return generalSettingsSnapshot;
        }

        private GachaSettingsSnapshot GetGachaSettingsSnapshot()
        {
            EnsureSettingsSnapshots();
            return gachaSettingsSnapshot;
        }

        private void EnsureSettingsSnapshots()
        {
            if (!settingsCacheDirty)
            {
                return;
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

            generalSettingsSnapshot = new GeneralSettingsSnapshot(
                enableGuarantee,
                Math.Max(1, Settings.Instance.General.GuaranteeThreshold),
                Math.Max(1, Settings.Instance.General.PacerThreshold),
                guaranteeWeights,
                pacerNames);

            gachaSettingsSnapshot = new GachaSettingsSnapshot(
                Settings.Instance.Gacha.Enabled,
                Math.Clamp(Settings.Instance.Gacha.FiveStarBaseRate, 0.0, 1.0),
                Math.Max(1, Settings.Instance.Gacha.FiveStarSoftPityStart),
                Math.Max(1, Settings.Instance.Gacha.FiveStarHardPity),
                Math.Max(0.0, Settings.Instance.Gacha.FiveStarSoftPityStep),
                Math.Clamp(Settings.Instance.Gacha.FourStarBaseRate, 0.0, 1.0),
                Math.Max(1, Settings.Instance.Gacha.FourStarSoftPityStart),
                Math.Max(1, Settings.Instance.Gacha.FourStarHardPity),
                Math.Max(0.0, Settings.Instance.Gacha.FourStarSoftPityStep),
                Math.Clamp(Settings.Instance.Gacha.FiveStarFeaturedRate, 0.0, 1.0),
                Math.Clamp(Settings.Instance.Gacha.FourStarFeaturedRate, 0.0, 1.0));

            settingsCacheDirty = false;
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

        private sealed record GachaSettingsSnapshot(
            bool Enabled,
            double FiveStarBaseRate,
            int FiveStarSoftPityStart,
            int FiveStarHardPity,
            double FiveStarSoftPityStep,
            double FourStarBaseRate,
            int FourStarSoftPityStart,
            int FourStarHardPity,
            double FourStarSoftPityStep,
            double FiveStarFeaturedRate,
            double FourStarFeaturedRate)
        {
            internal static readonly GachaSettingsSnapshot Empty =
                new(false, 0.006, 74, 90, 0.06, 0.051, 9, 10, 0.225, 0.5, 0.5);
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
