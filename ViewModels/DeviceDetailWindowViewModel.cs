using System.Collections.ObjectModel;
using TestMcAlgorithm.Models;

namespace TestMcAlgorithm.ViewModels;

public sealed class DeviceDetailWindowViewModel : ObservableObject
{
    private string _title;
    private string _subtitle;
    private string _currentText;
    private string _statusText;
    private string _infoText;

    public DeviceDetailWindowViewModel(OvrEndpointSettingsModel endpoint, ushort startAddress)
    {
        _title = $"{endpoint.DeviceKey} Detail";
        _subtitle = $"{endpoint.Name} / {endpoint.IpAddress}:{endpoint.Port}";
        _currentText = endpoint.CurrentValueText;
        _statusText = endpoint.Status.ToString();
        _infoText = string.IsNullOrWhiteSpace(endpoint.Info) ? "-" : endpoint.Info;
        Registers = new ObservableCollection<DeviceRegisterRowModel>(
            endpoint.RegisterSnapshot.Select((value, index) => new DeviceRegisterRowModel(index, (ushort)(startAddress + index), value)));

        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke());
    }

    public event Action? CloseRequested;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Subtitle
    {
        get => _subtitle;
        set => SetProperty(ref _subtitle, value);
    }

    public string CurrentText
    {
        get => _currentText;
        set => SetProperty(ref _currentText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string InfoText
    {
        get => _infoText;
        set => SetProperty(ref _infoText, value);
    }

    public ObservableCollection<DeviceRegisterRowModel> Registers { get; }

    public RelayCommand CloseCommand { get; }
}
