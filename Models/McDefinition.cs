namespace TestMcAlgorithm.Models;

public sealed record McDefinition(
    int Number,
    string Code,
    string Family,
    double? ImpedanceMohm,
    bool IsAlgorithmManaged,
    bool IsShared)
{
    public string ImpedanceText => ImpedanceMohm is null ? "-" : $"{ImpedanceMohm:F1} mΩ";
}

public static class McCatalog
{
    public static IReadOnlyList<McDefinition> All { get; } =
    [
        new(1, "MC1", "A", 1443.7171, true, true),
        new(2, "MC2", "A", 1443.7171, true, false),
        new(3, "MC3", "A", 1443.7171, true, true),
        new(4, "MC4", "B", 824.9812, true, false),
        new(5, "MC5", "A", 1443.7171, true, false),
        new(6, "MC6", "A", 1443.7171, true, false),
        new(7, "MC7", "B", 824.9812, true, false),
        new(8, "MC8", "C", 962.5249, true, true),
        new(9, "MC9", "D", 577.5150, true, true),
        new(10, "MC10", "A", 1443.7171, true, true),
        new(11, "MC11", "RESERVE", null, false, false),
        new(12, "MC12", "RESERVE", null, false, false),
        new(13, "MC13", "RESERVE", null, false, false),
        new(14, "MC14", "RESERVE", null, false, false),
        new(15, "MC15", "RESERVE", null, false, false),
        new(16, "MC16", "RESERVE", null, false, false),
        new(17, "MC17", "RESERVE", null, false, false),
        new(18, "MC18", "RESERVE", null, false, false),
        new(19, "MC19", "RESERVE", null, false, false),
    ];

    public static IReadOnlyDictionary<int, McDefinition> ByNumber { get; } =
        All.ToDictionary(item => item.Number);
}
