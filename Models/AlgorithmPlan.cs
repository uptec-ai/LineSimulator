namespace TestMcAlgorithm.Models;

public sealed record BusSelectionResult(
    string BusName,
    BusRequestSpec Request,
    IReadOnlyList<int> McNumbers,
    IReadOnlyDictionary<string, int> FamilyCounts,
    double ZeqMohm,
    double ErrorMohm,
    bool IsAssigned,
    string Message)
{
    public string Summary =>
        !IsAssigned
            ? $"{BusName}: X ({Message})"
            : $"{BusName}: {Request.RatedKva:g}kVA / SCR {Request.Scr:g} -> {FamilySummary} -> {string.Join(", ", McNumbers.Select(number => $"MC{number}"))} -> Zeq {ZeqMohm:F4} mΩ";

    public string FamilySummary =>
        string.Join(", ",
            FamilyCounts
                .Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Key)
                .Select(pair => $"{pair.Key}={pair.Value}"));

    public static BusSelectionResult Unassigned(BusRequestSpec request, string message) =>
        new(request.BusName, request, [], new Dictionary<string, int>(), 0.0, 0.0, false, message);
}

public sealed record AlgorithmPlan(
    BusSelectionResult Bus1,
    BusSelectionResult? Bus2,
    BusSelectionResult? Bus3,
    IReadOnlyList<int> OrderedTurnOnNumbers,
    IReadOnlyList<int> RemainingSharedNumbers,
    string Explanation)
{
    public int ActiveBusCount =>
        (Bus1.IsAssigned ? 1 : 0) +
        ((Bus2?.IsAssigned ?? false) ? 1 : 0) +
        ((Bus3?.IsAssigned ?? false) ? 1 : 0);

    public double TotalAbsoluteErrorMohm =>
        Math.Abs(Bus1.ErrorMohm) +
        Math.Abs(Bus2?.ErrorMohm ?? 0.0) +
        Math.Abs(Bus3?.ErrorMohm ?? 0.0);
}
