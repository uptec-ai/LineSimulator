using System.Windows.Media;
using TestMcAlgorithm.Models;

namespace TestMcAlgorithm.ViewModels;

public sealed class KContactViewModel : ObservableObject
{
    private static readonly Brush DefaultBusBrush = CreateBrush("#E2E8F0");
    private static readonly Brush Bus1Brush = CreateBrush("#5DE2A5");
    private static readonly Brush Bus2Brush = CreateBrush("#FCD34D");
    private static readonly Brush Bus3Brush = CreateBrush("#93C5FD");

    private bool _isOn;
    private string _displayTargetBus = "-";

    public KContactViewModel(KDefinition definition)
    {
        Definition = definition;
    }

    public KDefinition Definition { get; }
    public int Number => Definition.Number;
    public string Code => Definition.Code;
    public string TargetBus => Definition.TargetBus;
    public string DisplayTargetBus
    {
        get => _displayTargetBus;
        set
        {
            if (SetProperty(ref _displayTargetBus, value))
            {
                RaisePropertyChanged(nameof(BusForeground));
            }
        }
    }

    public string IdentityText => Definition.IdentityText;
    public string ImpedanceText => Definition.ImpedanceText;

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (SetProperty(ref _isOn, value))
            {
                RaisePropertyChanged(nameof(StateText));
            }
        }
    }

    public string StateText => IsOn ? "ON" : "OFF";

    public Brush BusForeground => DisplayTargetBus switch
    {
        "BUS1" => Bus1Brush,
        "BUS2" => Bus2Brush,
        "BUS3" => Bus3Brush,
        "NBUS1" => Bus1Brush,
        "NBUS2" => Bus2Brush,
        "NBUS3" => Bus3Brush,
        _ => DefaultBusBrush,
    };

    private static Brush CreateBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
