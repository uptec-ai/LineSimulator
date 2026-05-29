using System.Collections.ObjectModel;
using System.Windows;
using TestMcAlgorithm.Models;
using TestMcAlgorithm.Services;
using TestMcAlgorithm.Views;

namespace TestMcAlgorithm.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private IpSettingsWindow? _ipSettingsWindow;
    private OcrSettingsWindow? _ocrSettingsWindow;
    private LogWindow? _logWindow;
    private DeviceDetailWindow? _deviceDetailWindow;
    private readonly ModbusTcpMonitoringServer _monitoringServer;
    private int _activeMonitoringClientCount;
    private bool _isMonitoringServerRunning;
    public LineSimulatorViewModel LineSimulator { get; }
    public MainScreenStateModel State => LineSimulator.State;

    public BusDiagram BusDiagram => LineSimulator.BusDiagram;

    public ObservableCollection<ModbusMonitoringClientModel> MonitoringClients { get; } = [];

    public string MonitoringServerEndpointText =>
        $"{ModbusProtocolDefinitions.ServerHost}:{ModbusProtocolDefinitions.ServerPort}";

    public int MonitoringMaxClientCount => ModbusProtocolDefinitions.MaxMonitoringClients;

    public int ActiveMonitoringClientCount
    {
        get => _activeMonitoringClientCount;
        private set
        {
            if (SetProperty(ref _activeMonitoringClientCount, value))
            {
                RaisePropertyChanged(nameof(MonitoringServerStatusText));
            }
        }
    }

    public bool IsMonitoringServerRunning
    {
        get => _isMonitoringServerRunning;
        private set
        {
            if (SetProperty(ref _isMonitoringServerRunning, value))
            {
                RaisePropertyChanged(nameof(MonitoringServerStatusText));
            }
        }
    }

    public string MonitoringServerStatusText =>
        IsMonitoringServerRunning
            ? $"Running / Clients {ActiveMonitoringClientCount}/{MonitoringMaxClientCount}"
            : "Stopped";

    public RelayCommand ShowIpSettingsWindowCommand { get; }
    public RelayCommand ShowOcrSettingsWindowCommand { get; }
    public RelayCommand ShowLogWindowCommand { get; }

    public MainViewModel(McAlgorithmService algorithmService, IModbusGatewayService modbusGatewayService)
    {
        LineSimulator = new LineSimulatorViewModel(algorithmService, modbusGatewayService);
        LineSimulator.DeviceDetailRequested += OnDeviceDetailRequested;
        _monitoringServer = new ModbusTcpMonitoringServer(LineSimulator.CreateMonitoringSnapshot);
        _monitoringServer.ClientStatusChanged += OnMonitoringClientStatusChanged;
        try
        {
            _monitoringServer.StartAsync().GetAwaiter().GetResult();
            IsMonitoringServerRunning = _monitoringServer.IsRunning;
        }
        catch (Exception ex)
        {
            IsMonitoringServerRunning = false;
            MessageBox.Show(
                $"Modbus monitoring server start failed: {ex.Message}",
                "Modbus Server",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        ShowIpSettingsWindowCommand = new RelayCommand(_ => ShowIpSettingsWindow());
        ShowOcrSettingsWindowCommand = new RelayCommand(_ => ShowOcrSettingsWindow());
        ShowLogWindowCommand = new RelayCommand(_ => ShowLogWindow());
    }

    private void ShowIpSettingsWindow()
    {
        if (_ipSettingsWindow is null || !_ipSettingsWindow.IsLoaded)
        {
            var viewModel = new IpSettingsWindowViewModel(this);
            _ipSettingsWindow = new IpSettingsWindow
            {
                Owner = Application.Current?.MainWindow,
                DataContext = viewModel
            };

            viewModel.CloseRequested += () => _ipSettingsWindow?.Close();
            _ipSettingsWindow.Closed += (_, _) => _ipSettingsWindow = null;
            _ipSettingsWindow.ShowDialog();
        }
        else
        {
            _ipSettingsWindow.Activate();
        }
    }
    private void ShowOcrSettingsWindow()
    {
        if (_ocrSettingsWindow is null || !_ocrSettingsWindow.IsLoaded)
        {
            _ocrSettingsWindow = new OcrSettingsWindow
            {
                Owner = Application.Current?.MainWindow
            };
            var viewModel = new OcrSettingsWindowViewModel(LineSimulator, _ocrSettingsWindow);
            _ocrSettingsWindow.DataContext = viewModel;

            viewModel.CloseRequested += () => _ocrSettingsWindow?.Close();
            _ocrSettingsWindow.Closed += (_, _) => _ocrSettingsWindow = null;
            _ocrSettingsWindow.ShowDialog();
        }
        else
        {
            _ocrSettingsWindow.Activate();
        }
    }

    private void ShowLogWindow()
    {
        if (_logWindow is null || !_logWindow.IsLoaded)
        {
            var viewModel = new LogWindowViewModel(LineSimulator);
            _logWindow = new LogWindow
            {
                Owner = Application.Current?.MainWindow,
                DataContext = viewModel
            };

            viewModel.CloseRequested += () => _logWindow?.Close();
            _logWindow.Closed += (_, _) => _logWindow = null;
            _logWindow.Show();
        }
        else
        {
            _logWindow.Activate();
        }
    }

    private void OnDeviceDetailRequested(string deviceKey)
    {
        var viewModel = LineSimulator.CreateDeviceDetailViewModel(deviceKey);

        if (_deviceDetailWindow is null || !_deviceDetailWindow.IsLoaded)
        {
            _deviceDetailWindow = new DeviceDetailWindow
            {
                Owner = Application.Current?.MainWindow,
                DataContext = viewModel
            };

            viewModel.CloseRequested += () => _deviceDetailWindow?.Close();
            _deviceDetailWindow.Closed += (_, _) => _deviceDetailWindow = null;
            _deviceDetailWindow.Show();
            return;
        }
        _deviceDetailWindow.DataContext = viewModel;
        viewModel.CloseRequested += () => _deviceDetailWindow?.Close();
        _deviceDetailWindow.Activate();
    }

    private void OnMonitoringClientStatusChanged(ModbusMonitoringClientStatus status)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ApplyMonitoringClientStatus(status);
            return;
        }

        dispatcher.InvokeAsync(() => ApplyMonitoringClientStatus(status));
    }

    private void ApplyMonitoringClientStatus(ModbusMonitoringClientStatus status)
    {
        var item = MonitoringClients.FirstOrDefault(client => client.ClientId == status.ClientId);
        if (item is null)
        {
            item = new ModbusMonitoringClientModel(status);
            MonitoringClients.Insert(0, item);
        }
        else
        {
            item.Apply(status);
        }

        ActiveMonitoringClientCount = MonitoringClients.Count(client => client.IsConnected);
    }

    public void RequestShutdown()
    {
        _ipSettingsWindow?.Close();
        _ocrSettingsWindow?.Close();
        _logWindow?.Close();
        _deviceDetailWindow?.Close();
        LineSimulator.RequestShutdown();
    }

    public async Task DisconnectLineSimulatorForCloseAsync()
    {
        await LineSimulator.DisconnectForCloseAsync();
    }

    public void Dispose()
    {
        LineSimulator.DeviceDetailRequested -= OnDeviceDetailRequested;
        _monitoringServer.ClientStatusChanged -= OnMonitoringClientStatusChanged;
        _monitoringServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _ipSettingsWindow?.Close();
        _ocrSettingsWindow?.Close();
        _logWindow?.Close();
        _deviceDetailWindow?.Close();
        LineSimulator.Dispose();
    }
}
