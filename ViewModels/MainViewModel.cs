using System.Windows;
using TestMcAlgorithm.Models;
using TestMcAlgorithm.Services;
using TestMcAlgorithm.Views;

namespace TestMcAlgorithm.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private IpSettingsWindow? _ipSettingsWindow;
    private LogWindow? _logWindow;
    private DeviceDetailWindow? _deviceDetailWindow;
    public LineSimulatorViewModel LineSimulator { get; }
    public MainScreenStateModel State => LineSimulator.State;

    public BusDiagram BusDiagram => LineSimulator.BusDiagram;

    public RelayCommand ShowIpSettingsWindowCommand { get; }
    public RelayCommand ShowOcrSettingsWindowCommand { get; }
    public RelayCommand ShowLogWindowCommand { get; }

    public MainViewModel(McAlgorithmService algorithmService, IModbusGatewayService modbusGatewayService)
    {
        LineSimulator = new LineSimulatorViewModel(algorithmService, modbusGatewayService);
        LineSimulator.DeviceDetailRequested += OnDeviceDetailRequested;

        ShowIpSettingsWindowCommand = new RelayCommand(_ => ShowIpSettingsWindow());
        //ShowOcrSettingsWindowCommand = new RelayCommand(_ => ShowOcrSettingsWindow());
        ShowLogWindowCommand = new RelayCommand(_ => ShowLogWindow());
    }

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
    //private void ShowOcrSettingsWindow()
    //{
    //    var viewModel = new OcrSettingsWindowViewModel(LineSimulator);
    //    var ocrSettingsWindow = new OcrSettingsWindow
    //    {
    //        Owner = Application.Current?.MainWindow,
    //        DataContext = viewModel
    //    };
    //    viewModel.CloseRequested += () => ocrSettingsWindow.Close();
    //    ocrSettingsWindow.ShowDialog();
    //}

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

    public void RequestShutdown()
    {
        _ipSettingsWindow?.Close();
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
        _ipSettingsWindow?.Close();
        _logWindow?.Close();
        _deviceDetailWindow?.Close();
        LineSimulator.Dispose();
    }
}
