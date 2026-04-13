using System.Windows;
using TestMcAlgorithm.ViewModels;

namespace TestMcAlgorithm.Views;

public partial class DeviceDetailWindow : Window
{
    public DeviceDetailWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is DeviceDetailWindowViewModel oldViewModel)
        {
            oldViewModel.CloseRequested -= HandleCloseRequested;
        }

        if (e.NewValue is DeviceDetailWindowViewModel newViewModel)
        {
            newViewModel.CloseRequested += HandleCloseRequested;
        }
    }

    private void HandleCloseRequested()
    {
        Close();
    }
}
