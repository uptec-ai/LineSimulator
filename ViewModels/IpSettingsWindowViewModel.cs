using TestMcAlgorithm.Models;

namespace TestMcAlgorithm.ViewModels;

public sealed class IpSettingsWindowViewModel
{
    public IpSettingsWindowViewModel(LineSimulatorViewModel lineSimulator)
    {
        LineSimulator = lineSimulator;
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke());
    }

    public event Action? CloseRequested;

    public LineSimulatorViewModel LineSimulator { get; }

    public MainScreenStateModel State => LineSimulator.State;

    public AsyncRelayCommand ConnectCommand => LineSimulator.ConnectCommand;

    public AsyncRelayCommand DisconnectCommand => LineSimulator.DisconnectCommand;

    public AsyncRelayCommand ApplyOvrSettingsCommand => LineSimulator.ApplyOvrSettingsCommand;

    public RelayCommand CloseCommand { get; }
}
