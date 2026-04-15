namespace TestMcAlgorithm.Models;

public sealed record KDefinition(
    int Number,
    string Code,
    int SourceMcNumber,
    string TargetBus,
    string Family,
    double? ImpedanceMohm,
    ushort CoilAddress,
    ushort FeedbackAddress)
{
    public string ImpedanceText => ImpedanceMohm is null ? "-" : $"{ImpedanceMohm:F1} mΩ";
    public string IdentityText => $"MC{SourceMcNumber} · {TargetBus}";
}

public static class KCatalog
{
    public static IReadOnlyList<KDefinition> All { get; } =
    [
        // MC1
        new(1, "K1", 1, "BUS1", "A", 1443.7171, 0, 0),
        new(2, "K2", 1, "BUS2", "A", 1443.7171, 1, 1),
        new(3, "K3", 1, "BUS3", "A", 1443.7171, 2, 2),
        // MC2
        new(4, "K4", 2, "BUS1", "A", 1443.7171, 3, 3),
        // MC3
        new(5, "K5", 3, "BUS1", "A", 1443.7171, 4, 4),
        new(6, "K6", 3, "BUS2", "A", 1443.7171, 5, 5),
        new(7, "K7", 3, "BUS3", "A", 1443.7171, 6, 6),
        // MC4
        new(8, "K8", 4, "BUS1", "B", 824.9812, 7, 7),
        // MC5
        new(9, "K9", 5, "BUS1", "A", 1443.7171, 8, 8),
        // MC6
        new(10, "K10", 6, "BUS1", "A", 1443.7171, 9, 9),
        // MC7
        new(11, "K11", 7, "BUS1", "B", 824.9812, 10, 10),
        // MC8
        new(12, "K12", 8, "BUS1", "C", 962.5249, 11, 11),
        new(13, "K13", 8, "BUS2", "C", 962.5249, 12, 12),
        new(14, "K14", 8, "BUS3", "C", 962.5249, 13, 13),
        // MC9
        new(15, "K15", 9, "BUS1", "D", 577.5150, 14, 14),
        new(16, "K16", 9, "BUS2", "D", 577.5150, 15, 15),
        new(17, "K17", 9, "BUS3", "D", 577.5150, 16, 16),
        // MC10
        new(28, "K28", 10, "BUS2", "A", 1443.7171, 17, 17),
        new(29, "K29", 10, "BUS3", "A", 1443.7171, 18, 18),

        // NMC 1
        new(18, "K18", 11, "NBUS1", "N1", null, 19, 19),
        // NMC 2
        new(19, "K19", 12, "NBUS1", "N2", null, 20, 20),
        new(20, "K20", 12, "NBUS2", "N2", null, 21, 21),
        // NMC 3
        new(21, "K21", 13, "NBUS1", "N3", null, 22, 22),
        new(22, "K22", 13, "NBUS2", "N3", null, 23, 23),
        new(23, "K23", 13, "NBUS3", "N3", null, 24, 24),
        // NMC 4
        new(24, "K24", 14, "NBUS2", "N4", null, 25, 25),
        new(25, "K25", 14, "NBUS3", "N4", null, 26, 26),
    ];

    public static IReadOnlyDictionary<(int SourceMcNumber, string TargetBus), KDefinition> ByMcBus { get; } =
        All.ToDictionary(item => (item.SourceMcNumber, item.TargetBus), item => item);
}
