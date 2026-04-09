using System.ComponentModel;
using System.Windows;
using TestMcAlgorithm.Services;
using TestMcAlgorithm.ViewModels;

namespace TestMcAlgorithm;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(
            new McAlgorithmService(),
            new ModbusTcpGatewayService());
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainViewModel viewModel &&
            viewModel.State.Connection.IsConnected)
        {
            e.Cancel = true;
            MessageBox.Show(
                "장비와 연결된 상태에서는 종료할 수 없습니다.\n먼저 Disconnect 후 다시 종료해 주세요.",
                "Connected",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (DataContext is MainViewModel shutdownViewModel)
        {
            shutdownViewModel.RequestShutdown();
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }
}
