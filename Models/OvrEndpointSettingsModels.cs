using System.Collections.ObjectModel;
using System.Linq;
using TestMcAlgorithm.Services;
using TestMcAlgorithm.ViewModels;

namespace TestMcAlgorithm.Models;

public enum EndpointRegisterAccess
{
    ReadOnly,
    WriteOnly,
    ReadWrite,
}

public enum EndpointValueFormat
{
    UInt16,
    UInt32,
    Ieee754Float32,
    IpAddress,
}

public sealed record EndpointRegisterDefinition(
    int Address,
    ushort ModbusStartAddress,
    ushort ByteLength,
    string Name,
    string Range,
    string Unit,
    EndpointRegisterAccess Access = EndpointRegisterAccess.ReadOnly,
    EndpointValueFormat Format = EndpointValueFormat.UInt16,
    bool IsExModelOnly = false,
    string Description = "");

public sealed record EndpointProtocolProfile(
    string ModelName,
    bool ReadFromInputRegisters,
    ushort ReadStartAddress,
    ushort ReadRegisterCount,
    int PrimaryDisplayAddress,
    double PrimaryDisplayScale,
    IReadOnlyList<EndpointRegisterDefinition> Registers);

public sealed record PowerMeterMeasurementSnapshot(
    double? AverageVoltage,
    double? AverageCurrent,
    double? PowerFactor,
    double? TotalActivePower,
    double? Frequency);

public sealed class PowerMeterMeasurementsModel : ObservableObject
{
    private double? _averageVoltage;
    private double? _averageCurrent;
    private double? _powerFactor;
    private double? _totalActivePower;
    private double? _frequency;

    public double? AverageVoltage
    {
        get => _averageVoltage;
        private set
        {
            if (SetProperty(ref _averageVoltage, value))
            {
                RaisePropertyChanged(nameof(AverageVoltageText));
            }
        }
    }

    public double? AverageCurrent
    {
        get => _averageCurrent;
        private set
        {
            if (SetProperty(ref _averageCurrent, value))
            {
                RaisePropertyChanged(nameof(AverageCurrentText));
            }
        }
    }

    public double? PowerFactor
    {
        get => _powerFactor;
        private set
        {
            if (SetProperty(ref _powerFactor, value))
            {
                RaisePropertyChanged(nameof(PowerFactorText));
            }
        }
    }

    public double? TotalActivePower
    {
        get => _totalActivePower;
        private set
        {
            if (SetProperty(ref _totalActivePower, value))
            {
                RaisePropertyChanged(nameof(TotalActivePowerText));
            }
        }
    }

    public double? Frequency
    {
        get => _frequency;
        private set
        {
            if (SetProperty(ref _frequency, value))
            {
                RaisePropertyChanged(nameof(FrequencyText));
            }
        }
    }

    public string AverageVoltageText => Format(AverageVoltage, "0.0");
    public string AverageCurrentText => Format(AverageCurrent, "0.0");
    public string PowerFactorText => Format(PowerFactor, "0.000");
    public string TotalActivePowerText => Format(TotalActivePower, "0.0");
    public string FrequencyText => Format(Frequency, "0.00");

    public void Apply(PowerMeterMeasurementSnapshot snapshot)
    {
        AverageVoltage = snapshot.AverageVoltage;
        AverageCurrent = snapshot.AverageCurrent;
        PowerFactor = snapshot.PowerFactor;
        TotalActivePower = snapshot.TotalActivePower;
        Frequency = snapshot.Frequency;
    }

    public void Clear()
    {
        AverageVoltage = null;
        AverageCurrent = null;
        PowerFactor = null;
        TotalActivePower = null;
        Frequency = null;
    }

    private static string Format(double? value, string format)
    {
        return value.HasValue ? value.Value.ToString(format) : "-";
    }
}

public static class OvrEndpointProtocolCatalog
{
    public static EndpointProtocolProfile EocrIsem2 { get; } =
        new(
            "EOCR-iSEM2",
            ReadFromInputRegisters: false,
            ReadStartAddress: 476,
            ReadRegisterCount: 26,
            PrimaryDisplayAddress: 500,
            PrimaryDisplayScale: 0.01,
            Registers: []);

    public static EndpointProtocolProfile Gimac1000 { get; } =
        new(
            "GIMAC1000",
            ReadFromInputRegisters: true,
            ReadStartAddress: 0,
            ReadRegisterCount: 102,
            PrimaryDisplayAddress: 30003,
            PrimaryDisplayScale: 1.0,
            Registers:
            [
                new(30001, 0, 4, "평균 전압", "-", "V", Format: EndpointValueFormat.Ieee754Float32),
                new(30003, 2, 4, "평균 전류", "-", "A", Format: EndpointValueFormat.Ieee754Float32),
                new(30023, 22, 4, "역률", "-1.0 ~ 1.0", string.Empty, Format: EndpointValueFormat.Ieee754Float32),
                new(30025, 24, 4, "유효전력 합", "-", "kW", Format: EndpointValueFormat.Ieee754Float32, Description: "화면의 total 전력 기본 표시값"),
                new(30027, 26, 4, "무효전력 합", "-", "kvar", Format: EndpointValueFormat.Ieee754Float32),
                new(30029, 28, 4, "피상전력 합", "-", "kVA", Format: EndpointValueFormat.Ieee754Float32),
                new(30031, 30, 4, "주파수", "-", "Hz", Format: EndpointValueFormat.Ieee754Float32),
                new(30011, 10, 4, "A상 전압", "-", "V", Format: EndpointValueFormat.Ieee754Float32),
                new(30013, 12, 4, "B상 전압", "-", "V", Format: EndpointValueFormat.Ieee754Float32),
                new(30015, 14, 4, "C상 전압", "-", "V", Format: EndpointValueFormat.Ieee754Float32),
                new(30017, 16, 4, "AB 선간전압", "-", "V", Format: EndpointValueFormat.Ieee754Float32),
                new(30019, 18, 4, "BC 선간전압", "-", "V", Format: EndpointValueFormat.Ieee754Float32),
                new(30021, 20, 4, "CA 선간전압", "-", "V", Format: EndpointValueFormat.Ieee754Float32),
                new(30097, 96, 4, "A상 전류 THD", "-", "%", Format: EndpointValueFormat.Ieee754Float32),
                new(30099, 98, 4, "B상 전류 THD", "-", "%", Format: EndpointValueFormat.Ieee754Float32),
                new(30101, 100, 4, "C상 전류 THD", "-", "%", Format: EndpointValueFormat.Ieee754Float32),
                new(30045, 44, 4, "역방향 유효전력량", "-", "Wh", Format: EndpointValueFormat.Ieee754Float32, IsExModelOnly: true, Description: "EX 모델 전용"),
                new(30047, 46, 4, "역방향 무효전력량", "-", "Varh", Format: EndpointValueFormat.Ieee754Float32, IsExModelOnly: true, Description: "EX 모델 전용"),
            ]);
}

public sealed class OvrEndpointSettingsModel : ObservableObject, IAsyncDisposable
{
    public enum EndpointStatus
    {
        Idle = 1,
        Enable = 2,
        Disable = 3,
    }
    private static readonly ushort[] EmptyRegisters = [];

    private bool _isEnabled;
    private bool _pendingIsEnabled;
    private string _ipAddress = "127.0.0.1";
    private int _port = 502;
    private int _unitId = 1;
    private int _slaveId = 1;
    private int _currentRegisterAddress;
    private double _currentScale = 1.0;
    private bool _readFromInputRegisters = true;
    private bool _isConnected;
    private EndpointStatus _statusText = EndpointStatus.Disable;
    private string _info = string.Empty;
    private double? _currentValue;
    private IReadOnlyList<ushort> _registerSnapshot = EmptyRegisters;
    private EndpointProtocolProfile _protocolProfile;

    public OvrEndpointSettingsModel(string name, string deviceKey, bool isEnabled = false)
    {
        Name = name;
        DeviceKey = deviceKey;
        _isEnabled = isEnabled;
        _pendingIsEnabled = isEnabled;
        _protocolProfile = ResolveDefaultProtocolProfile(deviceKey);
        ApplyProtocolDefaults(_protocolProfile);
    }

    public string Name { get; }

    public string DeviceKey { get; }

    public ModbusTcpEndpointClient Socket { get; } = new();

    public SemaphoreSlim IoLock { get; } = new(1, 1); // 통신 작업을 한 번에 하나만 하게 막는 잠금장치 : 최대 1개 작업만 들어올 수 있음

    public bool IsOvr => DeviceKey.StartsWith("OCR", StringComparison.OrdinalIgnoreCase);

    public bool IsPowerMeter => DeviceKey.StartsWith("PM", StringComparison.OrdinalIgnoreCase);

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (!SetProperty(ref _isEnabled, value))
            {
                return;
            }

            if (_pendingIsEnabled == value)
            {
                return;
            }

            _pendingIsEnabled = value;
            RaisePropertyChanged(nameof(PendingIsEnabled));
        }
    }

    public bool PendingIsEnabled
    {
        get => _pendingIsEnabled;
        set => SetProperty(ref _pendingIsEnabled, value);
    }

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

    public int SlaveId
    {
        get => _slaveId;
        set => SetProperty(ref _slaveId, value);
    }

    public int CurrentRegisterAddress
    {
        get => _currentRegisterAddress;
        set => SetProperty(ref _currentRegisterAddress, value);
    }

    public double CurrentScale
    {
        get => _currentScale;
        set => SetProperty(ref _currentScale, value);
    }

    public bool ReadFromInputRegisters
    {
        get => _readFromInputRegisters;
        set => SetProperty(ref _readFromInputRegisters, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public EndpointProtocolProfile ProtocolProfile
    {
        get => _protocolProfile;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (SetProperty(ref _protocolProfile, value))
            {
                ApplyProtocolDefaults(value);
                RaisePropertyChanged(nameof(ProtocolModelName));
                RaisePropertyChanged(nameof(RegisterDefinitions));
                RaisePropertyChanged(nameof(HasExModelRegisters));
            }
        }
    }

    public string ProtocolModelName => ProtocolProfile.ModelName;

    public IReadOnlyList<EndpointRegisterDefinition> RegisterDefinitions => ProtocolProfile.Registers;

    public bool HasExModelRegisters => ProtocolProfile.Registers.Any(register => register.IsExModelOnly);

    public PowerMeterMeasurementsModel PowerMeterMeasurements { get; } = new();

    public double? CurrentValue
    {
        get => _currentValue;
        set
        {
            if (SetProperty(ref _currentValue, value))
            {
                RaisePropertyChanged(nameof(CurrentValueText));
            }
        }
    }

    public IReadOnlyList<ushort> RegisterSnapshot
    {
        get => _registerSnapshot;
        private set => SetProperty(ref _registerSnapshot, value);
    }

    public EndpointStatus Status
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string Info
    {
        get => _info;
        set => SetProperty(ref _info, value);
    }

    public string CurrentValueText => CurrentValue.HasValue ? $"{CurrentValue.Value:0.0} A" : "-";

    public void ApplyReadResult(bool isConnected, double? currentValue, EndpointStatus statusText, IReadOnlyList<ushort>? registers = null)
    {
        IsConnected = isConnected;
        CurrentValue = currentValue;
        Status = statusText;
        UpdateRegisterSnapshot(registers ?? EmptyRegisters);
    }

    public void ApplyPowerMeterMeasurements(PowerMeterMeasurementSnapshot? snapshot)
    {
        if (!IsPowerMeter)
        {
            return;
        }

        if (snapshot is null)
        {
            PowerMeterMeasurements.Clear();
            return;
        }

        PowerMeterMeasurements.Apply(snapshot);
    }

    private void UpdateRegisterSnapshot(IReadOnlyList<ushort> registers)
    {
        if (HasSameRegisterSnapshot(registers))
        {
            return;
        }

        RegisterSnapshot = registers;
    }

    private bool HasSameRegisterSnapshot(IReadOnlyList<ushort> registers)
    {
        return _registerSnapshot.SequenceEqual(registers);
    }

    public async ValueTask DisposeAsync()
    {
        await Socket.DisposeAsync();
        IoLock.Dispose();
    }

    private static EndpointProtocolProfile ResolveDefaultProtocolProfile(string deviceKey)
    {
        return deviceKey.StartsWith("PM", StringComparison.OrdinalIgnoreCase)
            ? OvrEndpointProtocolCatalog.Gimac1000
            : OvrEndpointProtocolCatalog.EocrIsem2;
    }

    private void ApplyProtocolDefaults(EndpointProtocolProfile protocolProfile)
    {
        ReadFromInputRegisters = protocolProfile.ReadFromInputRegisters;
        CurrentRegisterAddress = protocolProfile.PrimaryDisplayAddress;
        CurrentScale = protocolProfile.PrimaryDisplayScale;
    }
}

public sealed class OvrSettingsDialogModel : ObservableObject, IAsyncDisposable
{
    private bool _isVisible;

    public OvrSettingsDialogModel()
    {
        Endpoints = new ObservableCollection<OvrEndpointSettingsModel>(
        [
            new("ISEM2-WHRUH_01", "OCR1"), // Over Current Relay1
            new("ISEM2-WHRUH_02", "OCR2"), // Over Current Relay2
            new("ISEM2-WHRUH_03", "OCR3"), // Over Current Relay3
            new("ISEM2-WHRUH_04", "OCR4"), // Over Current Relay4
            new("ISEM2-WHRUH_05", "OCR5"), // Over Current Relay5
            new("ISEM2-WHRUH_06", "OCR6"), // Over Current Relay6
            new("ISEM2-WHRUH_07", "OCR7"), // Over Current Relay7
            new("ISEM2-WHRUH_08", "OCR8"), // Over Current Relay8
            new("ISEM2-WHRUH_09", "OCR9"), // Over Current Relay9
            new("ISEM2-WHRUH_10", "OCR10"), // Over Current Relay10
            new("GIMAC1000", "PM1"),      // bus in 미터계
            new("GIMAC1000", "PM2"),      // bus out1 미터계
            new("GIMAC1000", "PM3"),      // bus out2 미터계
            new("GIMAC1000", "PM4"),      // bus out3 미터계 
        ]);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public ObservableCollection<OvrEndpointSettingsModel> Endpoints { get; }

    public async ValueTask DisposeAsync()
    {
        foreach (var endpoint in Endpoints)
        {
            await endpoint.DisposeAsync();
        }
    }
}
