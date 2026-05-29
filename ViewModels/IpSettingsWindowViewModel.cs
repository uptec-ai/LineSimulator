using System.Collections.ObjectModel;
using TestMcAlgorithm.Models;

namespace TestMcAlgorithm.ViewModels;

public sealed class IpSettingsWindowViewModel
{
    public IpSettingsWindowViewModel(MainViewModel mainViewModel)
    {
        Main = mainViewModel;
        LineSimulator = mainViewModel.LineSimulator;
        CheckAllIdleUseCommand = new RelayCommand(_ => CheckAllIdleUse());
        UncheckAllEnabledUseCommand = new RelayCommand(_ => UncheckAllEnabledUse());
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke());
    }

    public event Action? CloseRequested;

    public MainViewModel Main { get; }

    public LineSimulatorViewModel LineSimulator { get; }

    public MainScreenStateModel State => LineSimulator.State;

    public ObservableCollection<ModbusMonitoringClientModel> MonitoringClients => Main.MonitoringClients;

    public string MonitoringServerEndpointText => Main.MonitoringServerEndpointText;

    public string MonitoringServerStatusText => Main.MonitoringServerStatusText;

    public int MonitoringMaxClientCount => Main.MonitoringMaxClientCount;

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
