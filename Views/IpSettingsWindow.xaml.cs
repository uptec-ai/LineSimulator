using System.Windows;
using TestMcAlgorithm.ViewModels;

namespace TestMcAlgorithm.Views;

public partial class IpSettingsWindow : Window
{
    public IpSettingsWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is IpSettingsWindowViewModel oldViewModel)
        {
            oldViewModel.CloseRequested -= HandleCloseRequested;
        }

        if (e.NewValue is IpSettingsWindowViewModel newViewModel)
        {
            newViewModel.CloseRequested += HandleCloseRequested;
        }
    }

    private void HandleCloseRequested()
    {
        Close();
    }
}
