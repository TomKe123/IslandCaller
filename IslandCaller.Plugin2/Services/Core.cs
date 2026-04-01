using Microsoft.Extensions.Logging;
using IslandCaller.Models;
using System.Text.Json;
using System.Security.Cryptography;

namespace IslandCaller.Services
{
    public class CoreService(ProfileService profileService, HistoryService historyService, ILogger<CoreService> logger, Status status)
    {
        private readonly ProfileService profileService = profileService;
        private readonly HistoryService historyService = historyService;
        private readonly ILogger<CoreService> logger = logger;
        private readonly Status status = status;
        internal class Person
        {
            internal int Id { get; set; }
            internal string Name { get; set; } = string.Empty;
            internal int Gender { get; set; }
            internal double ManualWeight { get; set; } = 1.0; // 教师设置的基础权重，默认为 1.0
            internal double Weight { get; set; }
        }
        // 计算学生被点名的权重
        internal List<Person> Persons { get; set; } = new List<Person>();

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
                    Gender = person.Gender,
                    ManualWeight = person.ManualWeight,
                    Weight = 0.0
                });
            }
            ComputeWeightsForAllStudents();
            status.CoreServiceInitialized = true;
        }

        private double ComputeSingleWeight(
                                double manualWeight,     // W_manual_i
                                int lastHitStep,         // s_i_last：该学生上次被点到的轮次（没点过可设为 -1）
                                int nHist,               // n_hist_i：历史被点次数
                                double avgHist)          // avg_hist：全班历史平均被点次数
        {
            // -----------------------------
            // 1. 本节课防重复因子（随时间恢复）
            // -----------------------------
            const double fMin = 0;     // 最低值
            const double beta = 0.54;    // 恢复系数

            int deltaS = lastHitStep;
            if (deltaS < 0) deltaS = 15;

            // F_session = 1 - (1 - fMin) * exp(-beta * Δs)
            double F_session = 1 - (1 - fMin) * Math.Exp(-beta * deltaS);

            // -----------------------------
            // 2. 历史均衡因子
            // -----------------------------
            const double eps = 1.0;      // 平滑项
            const double gamma = 0.9;    // 补偿强度
            const double rMin = 0.6;     // 最小补偿
            const double rMax = 1.6;     // 最大补偿

            // F_history = clip( ((manualWeight * avgHist + eps)/(nHist + eps))^gamma , rMin, rMax )
            double ratio = (manualWeight * avgHist + eps) / (nHist + eps);
            double F_history = Math.Pow(ratio, gamma);
            F_history = Math.Max(rMin, Math.Min(rMax, F_history));

            // -----------------------------
            // 3. 最终权重
            // -----------------------------
            return manualWeight * F_session * F_history;
        }

        private void ComputeWeightsForAllStudents()
        {
            // 计算全班历史平均被点次数
            double avgHist = historyService.GetAverageLongTermCount();
            logger.LogTrace($"计算全班历史平均被点次数: {avgHist}");
            foreach (var person in Persons)
            {
                int nHist = historyService.GetLongTermCount(person.Name);
                int lastHitStep = historyService.GetLastCallIndex(person.Name);
                double weight = ComputeSingleWeight(
                                    person.ManualWeight,
                                    lastHitStep,
                                    nHist,
                                    avgHist);
                person.Weight = weight;
                logger.LogTrace($"计算权重 - 学生: {person.Name}, ManualWeight: {person.ManualWeight}, LastHitStep: {lastHitStep}, nHist: {nHist}, Weight: {weight}");
            }
        }

        internal string GetRandomStudent()
        {
            var guaranteed = GetGuaranteedCandidate();
            if (guaranteed != null)
            {
                historyService.Add(guaranteed.Name);
                logger.LogTrace("保底命中：{Name}, SessionMiss={SessionMiss}", guaranteed.Name, historyService.GetSessionMissCount(guaranteed.Name));
                ComputeWeightsForAllStudents();
                return guaranteed.Name;
            }

            // 计算权重总和
            double totalWeight = Persons.Sum(p => p.Weight);
            logger.LogTrace($"计算权重总和: {totalWeight}");
            if (totalWeight <= 0) return "Error"; // 避免除以零
            // 生成一个 [0, totalWeight) 的随机数
            double r = GetTrueRandomDouble() * totalWeight;
            logger.LogTrace($"生成随机数: {r} (范围: [0, {totalWeight}))");
            // 根据权重选择学生
            double cumulative = 0;
            foreach (var person in Persons)
            {
                cumulative += person.Weight;
                if (r < cumulative)
                {
                    historyService.Add(person.Name);
                    logger.LogTrace($"抽取到学生：{person.Name}");
                    ComputeWeightsForAllStudents();
                    return person.Name;
                }
            }
            logger.LogWarning($"随机选择学生时发生了意外情况，权重总和: {totalWeight}, 随机数: {r}");
            return "Error"; // 理论上不应该到达这里
        }

        private Person? GetGuaranteedCandidate()
        {
            if (!Settings.Instance.General.EnableGuarantee)
            {
                return null;
            }

            int threshold = Math.Max(1, Settings.Instance.General.GuaranteeThreshold);
            var guaranteeWeights = LoadGuaranteeWeightMap();
            if (guaranteeWeights.Count == 0)
            {
                return null;
            }

            var candidates = Persons
                .Where(p => guaranteeWeights.ContainsKey(p.Name) && historyService.GetSessionMissCount(p.Name) >= threshold)
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            int maxMiss = candidates.Max(c => historyService.GetSessionMissCount(c.Name));
            var topMissCandidates = candidates
                .Where(c => historyService.GetSessionMissCount(c.Name) == maxMiss)
                .ToList();

            double totalWeight = topMissCandidates.Sum(c => Math.Max(0.01, c.Weight * guaranteeWeights[c.Name]));
            if (totalWeight <= 0)
            {
                return topMissCandidates.OrderBy(x => x.Id).FirstOrDefault();
            }

            double r = GetTrueRandomDouble() * totalWeight;
            double cumulative = 0;
            foreach (var person in topMissCandidates)
            {
                cumulative += Math.Max(0.01, person.Weight * guaranteeWeights[person.Name]);
                if (r < cumulative)
                {
                    return person;
                }
            }

            return topMissCandidates.OrderBy(x => x.Id).FirstOrDefault();
        }

        private Dictionary<string, double> LoadGuaranteeWeightMap()
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<GuaranteeWeightItem>>(Settings.Instance.General.GuaranteeWeightListJson ?? "[]") ?? [];
                var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in items)
                {
                    if (string.IsNullOrWhiteSpace(item.Name))
                    {
                        continue;
                    }

                    result[item.Name.Trim()] = Math.Max(0.01, item.Weight);
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

        private class GuaranteeWeightItem
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
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static double GetTrueRandomDouble()
        {
            // 使用加密随机数生成器获得真正的随机数
            // 获取0.0到1.0之间的均匀分布的双精度浮点数
            Span<byte> bytes = stackalloc byte[8];
            RandomNumberGenerator.Fill(bytes);
            // 转换为UInt64并归一化到[0, 1)范围内
            ulong value = BitConverter.ToUInt64(bytes);
            // 使用双精度浮点数的有效精度范围
            return (value >> 11) * (1.0 / 4503599627370496.0); // 1.0 / 2^52
        }
    }
}

