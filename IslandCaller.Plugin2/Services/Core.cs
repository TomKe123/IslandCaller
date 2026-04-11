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
        private readonly UsbAuthService usbAuthService;
        private readonly ILogger<CoreService> logger;
        private readonly Status status;
        private const double CapturedRadianceChanceOnFeaturedMiss = 0.1;
        private bool settingsCacheDirty = true;
        private GachaSettingsSnapshot gachaSettingsSnapshot = GachaSettingsSnapshot.Empty;

        public CoreService(ProfileService profileService, HistoryService historyService, UsbAuthService usbAuthService, ILogger<CoreService> logger, Status status)
        {
            this.profileService = profileService;
            this.historyService = historyService;
            this.usbAuthService = usbAuthService;
            this.logger = logger;
            this.status = status;
            Settings.Instance.General.PropertyChanged += OnSettingsChanged;
            Settings.Instance.Gacha.PropertyChanged += OnSettingsChanged;
        }

        internal enum DrawType
        {
            Normal,
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
            if (IsGachaModeActive())
            {
                return GetRandomCharacterPoolResult();
            }

            return GetClassicRandomStudentResult();
        }

        private DrawResult GetClassicRandomStudentResult()
        {
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
                    historyService.Add(person.Name);
                    ComputeWeightsForAllStudents();
                    return new DrawResult(person.Name, DrawType.Normal);
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
            RefreshDailyFeaturedPool(pityState);
            pityState.TotalDrawCount++;

            bool isFiveStar = RollByPity(pityState.FiveStarPity, settings.FiveStarBaseRate, settings.FiveStarSoftPityStart, settings.FiveStarHardPity, settings.FiveStarSoftPityStep);
            if (isFiveStar)
            {
                pityState.FiveStarPity = 0;
                pityState.FourStarPity = 0;
                bool guaranteedFeaturedHit = pityState.IsFiveStarFeaturedGuaranteed;
                bool normalFeaturedHit = guaranteedFeaturedHit || RollChance(settings.FiveStarFeaturedRate);
                bool capturedRadianceHit = !normalFeaturedHit
                    && !guaranteedFeaturedHit
                    && RollChance(CapturedRadianceChanceOnFeaturedMiss);
                bool featuredHit = normalFeaturedHit || capturedRadianceHit;
                var person = PickFiveStarPerson(pityState, featuredHit);
                bool selectedIsFeatured = person != null && string.Equals(person.Name, pityState.FeaturedFiveStarName, StringComparison.OrdinalIgnoreCase);
                if (capturedRadianceHit && selectedIsFeatured)
                {
                    pityState.CapturedRadianceCount++;
                }

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
                var person = PickFourStarPerson(pityState, featuredHit);
                bool selectedIsFeatured = person != null && pityState.FeaturedFourStarNames.Contains(person.Name, StringComparer.OrdinalIgnoreCase);
                pityState.IsFourStarFeaturedGuaranteed = !selectedIsFeatured;
                historyService.UpdateGachaPityState(pityState);
                return FinalizeDraw(person, selectedIsFeatured ? DrawType.FeaturedFourStar : DrawType.FourStar);
            }

            pityState.FiveStarPity++;
            pityState.FourStarPity++;
            historyService.UpdateGachaPityState(pityState);
            return FinalizeDraw(PickThreeStarPerson(pityState), DrawType.ThreeStar);
        }

        private DrawResult FinalizeDraw(Person? person, DrawType fallbackType)
        {
            if (person == null)
            {
                return new DrawResult("Error", DrawType.Normal);
            }

            historyService.Add(person.Name);
            return new DrawResult(person.Name, fallbackType);
        }

        private Person? PickFiveStarPerson(GachaPityState pityState, bool preferFeatured)
        {
            var fiveStars = Persons.Where(p => p.Rarity == GachaRarity.FiveStar).ToList();
            if (fiveStars.Count == 0)
            {
                return PickWeightedPerson(Persons);
            }

            if (preferFeatured && !string.IsNullOrWhiteSpace(pityState.FeaturedFiveStarName))
            {
                var featured = fiveStars.FirstOrDefault(p => string.Equals(p.Name, pityState.FeaturedFiveStarName, StringComparison.OrdinalIgnoreCase));
                if (featured != null)
                {
                    return featured;
                }
            }

            var nonFeatured = fiveStars
                .Where(p => !string.Equals(p.Name, pityState.FeaturedFiveStarName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var person = PickWeightedPerson(nonFeatured);
            if (person != null)
            {
                return person;
            }

            return PickWeightedPerson(fiveStars);
        }

        private Person? PickFourStarPerson(GachaPityState pityState, bool preferFeatured)
        {
            var nonFiveStars = Persons.Where(p => p.Rarity != GachaRarity.FiveStar).ToList();
            if (nonFiveStars.Count == 0)
            {
                return PickWeightedPerson(Persons);
            }

            var featuredNames = pityState.FeaturedFourStarNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var featuredFourStars = nonFiveStars.Where(p => featuredNames.Contains(p.Name)).ToList();
            if (preferFeatured)
            {
                var person = PickWeightedPerson(featuredFourStars);
                if (person != null)
                {
                    return person;
                }
            }

            var standardFourStars = nonFiveStars.Where(p => !featuredNames.Contains(p.Name)).ToList();
            var fallback = PickWeightedPerson(standardFourStars);
            if (fallback != null)
            {
                return fallback;
            }

            return PickWeightedPerson(nonFiveStars);
        }

        private Person? PickThreeStarPerson(GachaPityState pityState)
        {
            var featuredNames = pityState.FeaturedFourStarNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var pool = Persons
                .Where(p => p.Rarity != GachaRarity.FiveStar && !featuredNames.Contains(p.Name))
                .ToList();
            if (pool.Count == 0)
            {
                pool = Persons.Where(p => p.Rarity != GachaRarity.FiveStar).ToList();
            }

            return PickWeightedPerson(pool);
        }

        private void RefreshDailyFeaturedPool(GachaPityState pityState)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (pityState.BannerDate == today)
            {
                return;
            }

            var fiveStarCandidates = Persons.Where(p => p.Rarity == GachaRarity.FiveStar).ToList();
            var fourStarCandidates = Persons.Where(p => p.Rarity != GachaRarity.FiveStar).ToList();

            pityState.BannerDate = today;
            pityState.FeaturedFiveStarName = PickDailyFeaturedName(fiveStarCandidates);
            pityState.FeaturedFourStarNames = PickDailyFeaturedNames(fourStarCandidates, 3);
            historyService.UpdateGachaPityState(pityState);
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

        private static string PickDailyFeaturedName(IReadOnlyList<Person> persons)
        {
            if (persons.Count == 0)
            {
                return string.Empty;
            }

            int index = RandomNumberGenerator.GetInt32(persons.Count);
            return persons[index].Name;
        }

        private static List<string> PickDailyFeaturedNames(IReadOnlyList<Person> persons, int count)
        {
            if (persons.Count == 0 || count <= 0)
            {
                return [];
            }

            var list = persons.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }

            return list.Take(Math.Min(count, list.Count)).ToList();
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
            if (IsGachaModeActive())
            {
                foreach (var person in Persons)
                {
                    person.Weight = Math.Max(0.01, person.ManualWeight);
                }
                return;
            }

            double avgHist = historyService.GetAverageLongTermCount();

            foreach (var person in Persons)
            {
                int nHist = historyService.GetLongTermCount(person.Name);
                int lastHitStep = historyService.GetLastCallIndex(person.Name);
                double weight = ComputeSingleWeight(person.ManualWeight, lastHitStep, nHist, avgHist);
                person.Weight = weight;
            }
        }

        private bool IsGachaModeActive()
        {
            return GetGachaSettingsSnapshot().Enabled && usbAuthService.CanUseGachaMode();
        }

        private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            settingsCacheDirty = true;
            ComputeWeightsForAllStudents();
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

        private static string NormalizeName(string? rawName)
        {
            return rawName?.Trim() ?? string.Empty;
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
