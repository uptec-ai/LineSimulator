using TestMcAlgorithm.Models;

namespace TestMcAlgorithm.ViewModels;

public sealed class IpSettingsWindowViewModel
{
    public IpSettingsWindowViewModel(LineSimulatorViewModel lineSimulator)
    {
        LineSimulator = lineSimulator;
        CheckAllIdleUseCommand = new RelayCommand(_ => CheckAllIdleUse());
        UncheckAllEnabledUseCommand = new RelayCommand(_ => UncheckAllEnabledUse());
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke());
    }

    public event Action? CloseRequested;

    public LineSimulatorViewModel LineSimulator { get; }

    public MainScreenStateModel State => LineSimulator.State;

    public AsyncRelayCommand ConnectCommand => LineSimulator.ConnectCommand;

    public AsyncRelayCommand DisconnectCommand => LineSimulator.DisconnectCommand;

    public AsyncRelayCommand ApplyOvrSettingsCommand => LineSimulator.ApplyOvrSettingsCommand;

    public RelayCommand CheckAllIdleUseCommand { get; }

    public RelayCommand UncheckAllEnabledUseCommand { get; }

    public RelayCommand CloseCommand { get; }

    private void CheckAllIdleUse()
    {
        foreach (var endpoint in State.OvrSettings.Endpoints.Where(endpoint =>
                     endpoint.Status == OvrEndpointSettingsModel.EndpointStatus.Idle))
        {
            endpoint.PendingIsEnabled = true;
        }
    }

    private void UncheckAllEnabledUse()
    {
        foreach (var endpoint in State.OvrSettings.Endpoints.Where(endpoint =>
                     endpoint.Status == OvrEndpointSettingsModel.EndpointStatus.Enable))
        {
            endpoint.PendingIsEnabled = false;
        }
    }
}
