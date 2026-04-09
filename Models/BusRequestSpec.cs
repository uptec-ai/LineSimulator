namespace TestMcAlgorithm.Models;

public readonly record struct BusRequestSpec(string BusName, double RatedKva, double Scr)
{
    public double TargetGridMohm => (380.0 * 380.0) / (RatedKva * Scr);
}
