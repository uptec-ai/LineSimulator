using System.Windows;
using TestMcAlgorithm.Models;
using TestMcAlgorithm.Services;
using TestMcAlgorithm.Views;

namespace TestMcAlgorithm.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private IpSettingsWindow? _ipSettingsWindow;
    private DeviceDetailWindow? _deviceDetailWindow;

    public MainViewModel(McAlgorithmService algorithmService, IModbusGatewayService modbusGatewayService)
    {
        LineSimulator = new LineSimulatorViewModel(algorithmService, modbusGatewayService);
        LineSimulator.DeviceDetailRequested += OnDeviceDetailRequested;

        ShowIpSettingsWindowCommand = new RelayCommand(_ => ShowIpSettingsWindow());
    }

    public LineSimulatorViewModel LineSimulator { get; }

    public MainScreenStateModel State => LineSimulator.State;

    public BusDiagram BusDiagram => LineSimulator.BusDiagram;

    public RelayCommand ShowIpSettingsWindowCommand { get; }

    private void ShowIpSettingsWindow()
    {
        if (_ipSettingsWindow is null || !_ipSettingsWindow.IsLoaded)
        {
            var viewModel = new IpSettingsWindowViewModel(LineSimulator);
            _ipSettingsWindow = new IpSettingsWindow
            {
                Owner = Application.Current?.MainWindow,
                DataContext = viewModel
            };

            viewModel.CloseRequested += () => _ipSettingsWindow?.Close();
            _ipSettingsWindow.Closed += (_, _) => _ipSettingsWindow = null;
            _ipSettingsWindow.Show();
        }
        else
        {
            _ipSettingsWindow.Activate();
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

    public void RequestShutdown()
    {
        _ipSettingsWindow?.Close();
        _deviceDetailWindow?.Close();
        LineSimulator.RequestShutdown();
    }

    public void Dispose()
    {
        LineSimulator.DeviceDetailRequested -= OnDeviceDetailRequested;
        _ipSettingsWindow?.Close();
        _deviceDetailWindow?.Close();
        LineSimulator.Dispose();
    }
}
