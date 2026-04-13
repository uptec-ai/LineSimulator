using System.Collections.ObjectModel;
using TestMcAlgorithm.ViewModels;

namespace TestMcAlgorithm.Models;

public sealed class ConnectionSettingsModel : ObservableObject
{
    private string _ipAddress = "127.0.0.1";
    private int _port = 502;
    private int _unitId = 1;
    private bool _isConnected;

    public string IpAddress
    {
        get => _ipAddress;
        set => SetProperty(ref _ipAddress, value);
    }

    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public int UnitId
    {
        get => _unitId;
        set => SetProperty(ref _unitId, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }
}

public sealed class BusSelectionModel : ObservableObject
{
    private bool _isEnabled;
    private bool _isApplied;
    private bool _isConfigurationLocked;
    private double _ratedKva;
    private double _scr;
    private string _summary;

    public BusSelectionModel(string busName, bool isEnabled, double ratedKva, double scr)
    {
        BusName = busName;
        _isEnabled = isEnabled;
        _ratedKva = ratedKva;
        _scr = scr;
        _summary = $"{busName}: -";
    }

    public string BusName { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public bool IsApplied
    {
        get => _isApplied;
        set => SetProperty(ref _isApplied, value);
    }

    public bool IsConfigurationLocked
    {
        get => _isConfigurationLocked;
        set => SetProperty(ref _isConfigurationLocked, value);
    }

    public double RatedKva
    {
        get => _ratedKva;
        set => SetProperty(ref _ratedKva, value);
    }

    public double Scr
    {
        get => _scr;
        set => SetProperty(ref _scr, value);
    }

    public string Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }
}

public sealed class MainScreenStateModel : ObservableObject
{
    private string _algorithmSummary = "계산 전";

    public ConnectionSettingsModel Connection { get; } = new();
    public OvrSettingsDialogModel OvrSettings { get; } = new();
    public DeviceDetailDialogModel DeviceDetail { get; } = new();
    public BusSelectionModel Bus1 { get; } = new("BUS1", true, 250, 3);
    public BusSelectionModel Bus2 { get; } = new("BUS2", true, 100, 5);
    public BusSelectionModel Bus3 { get; } = new("BUS3", true, 50, 2);
    public ManualBusSelectionModel ManualBus { get; } = new();
    public OperationModeModel OperationMode { get; } = new();

    public string AlgorithmSummary
    {
        get => _algorithmSummary;
        set => SetProperty(ref _algorithmSummary, value);
    }
}

public sealed class DeviceDetailDialogModel : ObservableObject
{
    private bool _isVisible;
    private string _title = "Device Detail";
    private string _subtitle = "-";
    private string _currentText = "-";
    private string _statusText = "-";
    private string _infoText = "-";

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

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

    public ObservableCollection<DeviceRegisterRowModel> Registers { get; } = [];
}

public sealed class DeviceRegisterRowModel
{
    public DeviceRegisterRowModel(int index, ushort address, ushort value)
    {
        Index = index;
        Address = address;
        Value = value;
    }

    public int Index { get; }

    public ushort Address { get; }

    public ushort Value { get; }
}

public sealed class OperationModeModel : ObservableObject
{
    private bool _isManualMode;

    public bool IsManualMode
    {
        get => _isManualMode;
        set
        {
            if (SetProperty(ref _isManualMode, value))
            {
                RaisePropertyChanged(nameof(IsAutoMode));
            }
        }
    }

    public bool IsAutoMode => !_isManualMode;
}

public sealed class ManualBusSelectionModel : ObservableObject
{
    private bool _isBus1Selected = true;
    private bool _isBus2Selected;
    private bool _isBus3Selected;
    private bool _isNBus1Selected = true;
    private bool _isNBus2Selected;
    private bool _isNBus3Selected;

    public bool IsBus1Selected
    {
        get => _isBus1Selected;
        set
        {
            if (!SetProperty(ref _isBus1Selected, value))
            {
                return;
            }

            if (value)
            {
                SetExclusiveSelection(nameof(IsBus1Selected));
            }
            else if (!_isBus2Selected && !_isBus3Selected)
            {
                _isBus1Selected = true;
                RaisePropertyChanged();
            }

            RaisePropertyChanged(nameof(SelectedBusName));
        }
    }

    public bool IsBus2Selected
    {
        get => _isBus2Selected;
        set
        {
            if (!SetProperty(ref _isBus2Selected, value))
            {
                return;
            }

            if (value)
            {
                SetExclusiveSelection(nameof(IsBus2Selected));
            }
            else if (!_isBus1Selected && !_isBus3Selected)
            {
                _isBus2Selected = true;
                RaisePropertyChanged();
            }

            RaisePropertyChanged(nameof(SelectedBusName));
        }
    }

    public bool IsBus3Selected
    {
        get => _isBus3Selected;
        set
        {
            if (!SetProperty(ref _isBus3Selected, value))
            {
                return;
            }

            if (value)
            {
                SetExclusiveSelection(nameof(IsBus3Selected));
            }
            else if (!_isBus1Selected && !_isBus2Selected)
            {
                _isBus3Selected = true;
                RaisePropertyChanged();
            }

            RaisePropertyChanged(nameof(SelectedBusName));
        }
    }

    public string SelectedBusName =>
        _isBus1Selected ? "BUS1" :
        _isBus2Selected ? "BUS2" :
        "BUS3";

    public bool IsNBus1Selected
    {
        get => _isNBus1Selected;
        set
        {
            if (!SetProperty(ref _isNBus1Selected, value))
            {
                return;
            }

            if (value)
            {
                SetExclusiveNBusSelection(nameof(IsNBus1Selected));
            }
            else if (!_isNBus2Selected && !_isNBus3Selected)
            {
                _isNBus1Selected = true;
                RaisePropertyChanged();
            }

            RaisePropertyChanged(nameof(SelectedNBusName));
        }
    }

    public bool IsNBus2Selected
    {
        get => _isNBus2Selected;
        set
        {
            if (!SetProperty(ref _isNBus2Selected, value))
            {
                return;
            }

            if (value)
            {
                SetExclusiveNBusSelection(nameof(IsNBus2Selected));
            }
            else if (!_isNBus1Selected && !_isNBus3Selected)
            {
                _isNBus2Selected = true;
                RaisePropertyChanged();
            }

            RaisePropertyChanged(nameof(SelectedNBusName));
        }
    }

    public bool IsNBus3Selected
    {
        get => _isNBus3Selected;
        set
        {
            if (!SetProperty(ref _isNBus3Selected, value))
            {
                return;
            }

            if (value)
            {
                SetExclusiveNBusSelection(nameof(IsNBus3Selected));
            }
            else if (!_isNBus1Selected && !_isNBus2Selected)
            {
                _isNBus3Selected = true;
                RaisePropertyChanged();
            }

            RaisePropertyChanged(nameof(SelectedNBusName));
        }
    }

    public string SelectedNBusName =>
        _isNBus1Selected ? "NBUS1" :
        _isNBus2Selected ? "NBUS2" :
        "NBUS3";

    private void SetExclusiveSelection(string selectedPropertyName)
    {
        if (selectedPropertyName != nameof(IsBus1Selected) && _isBus1Selected)
        {
            _isBus1Selected = false;
            RaisePropertyChanged(nameof(IsBus1Selected));
        }

        if (selectedPropertyName != nameof(IsBus2Selected) && _isBus2Selected)
        {
            _isBus2Selected = false;
            RaisePropertyChanged(nameof(IsBus2Selected));
        }

        if (selectedPropertyName != nameof(IsBus3Selected) && _isBus3Selected)
        {
            _isBus3Selected = false;
            RaisePropertyChanged(nameof(IsBus3Selected));
        }
    }

    private void SetExclusiveNBusSelection(string selectedPropertyName)
    {
        if (selectedPropertyName != nameof(IsNBus1Selected) && _isNBus1Selected)
        {
            _isNBus1Selected = false;
            RaisePropertyChanged(nameof(IsNBus1Selected));
        }

        if (selectedPropertyName != nameof(IsNBus2Selected) && _isNBus2Selected)
        {
            _isNBus2Selected = false;
            RaisePropertyChanged(nameof(IsNBus2Selected));
        }

        if (selectedPropertyName != nameof(IsNBus3Selected) && _isNBus3Selected)
        {
            _isNBus3Selected = false;
            RaisePropertyChanged(nameof(IsNBus3Selected));
        }
    }
}
