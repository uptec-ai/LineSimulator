using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using TestMcAlgorithm.Services;
using TestMcAlgorithm.ViewModels;

namespace TestMcAlgorithm;

public partial class MainWindow : Window
{
    private bool _isClosingAfterDisconnect;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(
            new McAlgorithmService(),
            new ModbusTcpGatewayService());
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_isClosingAfterDisconnect)
        {
            if (DataContext is MainViewModel shutdownViewModel)
            {
                shutdownViewModel.RequestShutdown();
            }

            base.OnClosing(e);
            return;
        }

        if (DataContext is MainViewModel viewModel &&
            viewModel.State.Connection.IsConnected)
        {
            e.Cancel = true;
            var result = MessageBox.Show(
                "LineSimulator와 연결된 상태에서는 종료할 수 없습니다.\n연결을 끊고 종료하시겠습니까?",
                "Connected",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.OK)
            {
                return;
            }

            try
            {
                await viewModel.DisconnectLineSimulatorForCloseAsync();
                _isClosingAfterDisconnect = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Line Simulator 연결 해제 중 오류가 발생했습니다.\n{ex.Message}",
                    "Disconnect Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            return;
        }

        if (DataContext is MainViewModel finalShutdownViewModel)
        {
            finalShutdownViewModel.RequestShutdown();
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        DataContext = null;
        base.OnClosed(e);

        if (Application.Current?.ShutdownMode != ShutdownMode.OnExplicitShutdown)
        {
            Application.Current?.Shutdown();
        }

        Application.Current?.Shutdown();
    }
}
