using TestMcAlgorithm.Models;

namespace TestMcAlgorithm.Services;

public sealed class McAlgorithmService
{
    private const double ToleranceMohm = 0.3;
    private static readonly int[] SharedPool = [1, 3, 8, 9, 10];
    private static readonly HashSet<int> SharedPoolSet = [1, 3, 8, 9, 10];
    private static readonly Dictionary<int, int> Bus1PriorityIndex = new()
    {
        [2] = 0,
        [4] = 1,
        [5] = 2,
        [6] = 3,
        [7] = 4,
        [1] = 5,
        [3] = 6,
        [8] = 7,
        [9] = 8,
    };

    // BUS1 우선순위:
    // 1) 단상 MC 우선 -> 2,4,5,6,7
    // 2) 공유 MC는 앞번호 우선 -> 1,3,8,9
    // 3) MC10은 BUS2/3 전용
    private static readonly FamilyDefinition[] Bus1Families =
    [
        new("A", 1443.7171, [2, 5, 6, 1, 3]),
        new("B", 824.9812, [4, 7]),
        new("C", 962.5249, [8]),
        new("D", 577.5150, [9]),
    ];

    public IReadOnlyList<double> SupportedRatedKva { get; } = [250, 200, 150, 100, 50];
    public IReadOnlyList<double> SupportedScr { get; } = [5, 4, 3.5, 3, 2];

    public AlgorithmPlan BuildPlan(BusRequestSpec bus1, BusRequestSpec? bus2, BusRequestSpec? bus3)
    {
        if (bus3 is not null && bus2 is null)
        {
            throw new InvalidOperationException("BUS3 is not allowed without BUS2.");
        }

        var bus1Candidate = SelectBus1Candidate(bus1);
        var remainingShared = BuildRemainingShared(bus1Candidate);

        var bus2Result = bus2 is null ? null : SelectSharedBus(bus2.Value, remainingShared);
        if (bus2Result is { IsAssigned: true })
        {
            foreach (var number in bus2Result.McNumbers)
            {
                remainingShared.Remove(number);
            }
        }

        var bus3Result = bus3 is null
            ? null
            : bus2Result is { IsAssigned: true }
                ? SelectSharedBus(bus3.Value, remainingShared)
                : BusSelectionResult.Unassigned(bus3.Value, "BUS3 requires BUS2.");

        var orderedNumbers = bus1Candidate.McNumbers
            .Concat(bus2Result?.McNumbers ?? [])
            .Concat(bus3Result?.McNumbers ?? [])
            .Distinct()
            .OrderBy(number => number)
            .ToArray();

        var explanation = BuildExplanation(bus1Candidate, bus2Result, bus3Result, remainingShared);

        return new AlgorithmPlan(
            bus1Candidate,
            bus2Result,
            bus3Result,
            orderedNumbers,
            remainingShared.OrderBy(number => number).ToArray(),
            explanation);
    }

    public bool CanBuildBus1(BusRequestSpec bus1)
    {
        return BuildBus1Candidates(bus1).Any();
    }

    public IReadOnlyList<BusRequestSpec> GetAvailableBus2Requests(BusRequestSpec bus1)
    {
        var bus1Candidate = SelectBus1Candidate(bus1);
        var remainingShared = BuildRemainingShared(bus1Candidate);
        return BuildAvailableRequests("BUS2", remainingShared);
    }

    public IReadOnlyList<BusRequestSpec> GetAvailableBus3Requests(BusRequestSpec bus1, BusRequestSpec bus2)
    {
        var bus1Candidate = SelectBus1Candidate(bus1);
        var remainingShared = BuildRemainingShared(bus1Candidate);
        var bus2Result = SelectSharedBus(bus2, remainingShared);
        if (!bus2Result.IsAssigned)
        {
            return [];
        }

        foreach (var number in bus2Result.McNumbers)
        {
            remainingShared.Remove(number);
        }

        return BuildAvailableRequests("BUS3", remainingShared);
    }

    private BusSelectionResult SelectBus1Candidate(BusRequestSpec request)
    {
        var candidates = BuildBus1Candidates(request).ToList();
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException($"No BUS1 candidate for {request.RatedKva:g}kVA / SCR {request.Scr:g}.");
        }

        var best = candidates[0];
        foreach (var candidate in candidates.Skip(1))
        {
            if (IsBetterBus1Candidate(candidate, best))
            {
                best = candidate;
            }
        }

        return best;
    }

    private IReadOnlyList<BusRequestSpec> BuildAvailableRequests(string busName, IReadOnlyList<int> availableShared)
    {
        var results = new List<BusRequestSpec>();

        foreach (var ratedKva in SupportedRatedKva)
        {
            foreach (var scr in SupportedScr)
            {
                var request = new BusRequestSpec(busName, ratedKva, scr);
                if (SelectSharedBus(request, availableShared).IsAssigned)
                {
                    results.Add(request);
                }
            }
        }

        return results;
    }

    private static List<int> BuildRemainingShared(BusSelectionResult bus1Candidate)
    {
        return SharedPool
            .Where(number => !bus1Candidate.McNumbers.Contains(number))
            .ToList();
    }

    private static IEnumerable<BusSelectionResult> BuildBus1Candidates(BusRequestSpec request)
    {
        for (var a = 0; a <= 5; a++)
        {
            for (var b = 0; b <= 2; b++)
            {
                for (var c = 0; c <= 1; c++)
                {
                    for (var d = 0; d <= 1; d++)
                    {
                        if (a + b + c + d == 0)
                        {
                            continue;
                        }

                        var impedances = Repeat(Bus1Families[0].ImpedanceMohm, a)
                            .Concat(Repeat(Bus1Families[1].ImpedanceMohm, b))
                            .Concat(Repeat(Bus1Families[2].ImpedanceMohm, c))
                            .Concat(Repeat(Bus1Families[3].ImpedanceMohm, d))
                            .ToArray();

                        var zeq = EquivalentParallelMohm(impedances);
                        var error = zeq - request.TargetGridMohm;
                        if (Math.Abs(error) > ToleranceMohm)
                        {
                            continue;
                        }

                        var mcNumbers = Bus1Families[0].McNumbers.Take(a)
                            .Concat(Bus1Families[1].McNumbers.Take(b))
                            .Concat(Bus1Families[2].McNumbers.Take(c))
                            .Concat(Bus1Families[3].McNumbers.Take(d))
                            .OrderBy(number => number)
                            .ToArray();

                        var familyCounts = new Dictionary<string, int>
                        {
                            ["A"] = a,
                            ["B"] = b,
                            ["C"] = c,
                            ["D"] = d,
                        };

                        yield return new BusSelectionResult(
                            request.BusName,
                            request,
                            mcNumbers,
                            familyCounts,
                            zeq,
                            error,
                            true,
                            "BUS1 family search");
                    }
                }
            }
        }
    }

    private static BusSelectionResult SelectSharedBus(BusRequestSpec request, IReadOnlyList<int> availableShared)
    {
        SharedCandidate? best = null;
        var items = availableShared.OrderBy(number => number).ToArray();

        foreach (var combo in EnumerateCombinations(items))
        {
            var impedances = combo.Select(number => McCatalog.ByNumber[number].ImpedanceMohm!.Value).ToArray();
            var zeq = EquivalentParallelMohm(impedances);
            var error = zeq - request.TargetGridMohm;
            if (Math.Abs(error) > ToleranceMohm)
            {
                continue;
            }

            var candidate = new SharedCandidate(combo, zeq, error);
            if (best is null || CompareShared(candidate, best.Value) < 0)
            {
                best = candidate;
            }
        }

        if (best is null)
        {
            return BusSelectionResult.Unassigned(request, "No remaining shared MC matched the target.");
        }

        return new BusSelectionResult(
            request.BusName,
            request,
            best.Value.McNumbers,
            BuildFamilyCounts(best.Value.McNumbers),
            best.Value.ZeqMohm,
            best.Value.ErrorMohm,
            true,
            "Shared pool search");
    }

    private static bool IsBetterBus1Candidate(BusSelectionResult candidate, BusSelectionResult current)
    {
        var candidateSharedBus1 = CountShared(candidate.McNumbers);
        var currentSharedBus1 = CountShared(current.McNumbers);
        if (candidateSharedBus1 != currentSharedBus1)
        {
            return candidateSharedBus1 < currentSharedBus1;
        }

        var compareBus1Priority = CompareByPriority(candidate.McNumbers, current.McNumbers, Bus1PriorityIndex);
        if (compareBus1Priority != 0)
        {
            return compareBus1Priority < 0;
        }

        var errorCompare = Math.Abs(candidate.ErrorMohm).CompareTo(Math.Abs(current.ErrorMohm));
        if (errorCompare != 0)
        {
            return errorCompare < 0;
        }

        return CompareMcLists(candidate.McNumbers, current.McNumbers) < 0;
    }

    private static int CompareShared(SharedCandidate left, SharedCandidate right)
    {
        if (left.McNumbers.Count != right.McNumbers.Count)
        {
            return left.McNumbers.Count.CompareTo(right.McNumbers.Count);
        }

        var errorCompare = Math.Abs(left.ErrorMohm).CompareTo(Math.Abs(right.ErrorMohm));
        if (errorCompare != 0)
        {
            return errorCompare;
        }

        return CompareMcLists(left.McNumbers, right.McNumbers);
    }

    private static int CompareMcLists(IReadOnlyList<int> left, IReadOnlyList<int> right)
    {
        var min = Math.Min(left.Count, right.Count);
        for (var i = 0; i < min; i++)
        {
            var compare = left[i].CompareTo(right[i]);
            if (compare != 0)
            {
                return compare;
            }
        }

        return left.Count.CompareTo(right.Count);
    }

    private static int CompareByPriority(
        IReadOnlyList<int> left,
        IReadOnlyList<int> right,
        IReadOnlyDictionary<int, int> priorityIndex)
    {
        var orderedLeft = left.OrderBy(number => priorityIndex.GetValueOrDefault(number, int.MaxValue)).ToArray();
        var orderedRight = right.OrderBy(number => priorityIndex.GetValueOrDefault(number, int.MaxValue)).ToArray();
        return CompareMcLists(orderedLeft, orderedRight);
    }

    private static int CountShared(IEnumerable<int> mcNumbers) =>
        mcNumbers.Count(number => SharedPoolSet.Contains(number));

    private static IReadOnlyDictionary<string, int> BuildFamilyCounts(IEnumerable<int> mcNumbers)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 0,
            ["B"] = 0,
            ["C"] = 0,
            ["D"] = 0,
        };

        foreach (var number in mcNumbers)
        {
            var family = McCatalog.ByNumber[number].Family;
            if (result.ContainsKey(family))
            {
                result[family]++;
            }
        }

        return result;
    }

    private static string BuildExplanation(
        BusSelectionResult bus1,
        BusSelectionResult? bus2,
        BusSelectionResult? bus3,
        IReadOnlyList<int> remainingShared)
    {
        var lines = new List<string>
        {
            $"1. BUS1 target Zgrid = {bus1.Request.TargetGridMohm:F4} mΩ",
            $"2. BUS1 family count result = {bus1.FamilySummary}",
            $"3. BUS1 assigned MC = {string.Join(", ", bus1.McNumbers.Select(number => $"MC{number}"))}",
            "4. BUS1 priority = MC2, MC4, MC5, MC6, MC7 -> MC1, MC3, MC8, MC9",
            $"5. Remaining shared after BUS1 = {(remainingShared.Count == 0 ? "-" : string.Join(", ", remainingShared.Select(number => $"MC{number}")))}"
        };

        if (bus2 is not null)
        {
            lines.Add(bus2.IsAssigned
                ? $"6. BUS2 selection = {string.Join(", ", bus2.McNumbers.Select(number => $"MC{number}"))}"
                : $"6. BUS2 = X ({bus2.Message})");
        }

        if (bus3 is not null)
        {
            lines.Add(bus3.IsAssigned
                ? $"7. BUS3 selection = {string.Join(", ", bus3.McNumbers.Select(number => $"MC{number}"))}"
                : $"7. BUS3 = X ({bus3.Message})");
        }

        lines.Add("8. Apply command turns on selected MC one-by-one every 1 second in ascending order.");
        return string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<double> Repeat(double value, int count)
    {
        for (var i = 0; i < count; i++)
        {
            yield return value;
        }
    }

    private static IEnumerable<IReadOnlyList<int>> EnumerateCombinations(IReadOnlyList<int> items)
    {
        var limit = 1 << items.Count;
        for (var mask = 1; mask < limit; mask++)
        {
            var selection = new List<int>();
            for (var index = 0; index < items.Count; index++)
            {
                if ((mask & (1 << index)) != 0)
                {
                    selection.Add(items[index]);
                }
            }

            yield return selection;
        }
    }

    private static double EquivalentParallelMohm(IEnumerable<double> impedancesMohm)
    {
        var reciprocalSum = impedancesMohm.Sum(value => 1.0 / value);
        return 1.0 / reciprocalSum;
    }

    private readonly record struct FamilyDefinition(string Family, double ImpedanceMohm, IReadOnlyList<int> McNumbers);
    private readonly record struct SharedCandidate(IReadOnlyList<int> McNumbers, double ZeqMohm, double ErrorMohm);
}
