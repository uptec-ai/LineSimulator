using System.Windows;
using TestMcAlgorithm.ViewModels;

namespace TestMcAlgorithm.Views;

public partial class LogWindow : Window
{
    public LogWindow()
    {
        InitializeComponent();
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is LogWindowViewModel viewModel)
        {
            viewModel.Dispose();
        }
    }
}
