using TestMcAlgorithm.Models;

namespace TestMcAlgorithm.ViewModels;

public sealed class McButtonViewModel : ObservableObject
{
    private bool _isOn;
    private string _assignedBus = "-";

    public McButtonViewModel(McDefinition definition)
    {
        Definition = definition;
    }

    public McDefinition Definition { get; }
    public int Number => Definition.Number;
    public string Code => Definition.Code;
    public string Family => Definition.Family;
    public string ImpedanceText => Definition.ImpedanceText;
    public bool IsAlgorithmManaged => Definition.IsAlgorithmManaged;

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

    public string AssignedBus
    {
        get => _assignedBus;
        set => SetProperty(ref _assignedBus, value);
    }

    public string StateText => IsOn ? "ON" : "OFF";
}
