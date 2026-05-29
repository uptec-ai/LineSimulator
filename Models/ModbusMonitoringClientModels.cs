using TestMcAlgorithm.ViewModels;

namespace TestMcAlgorithm.Models;

public sealed record ModbusMonitoringClientStatus(
    string ClientId,
    string RemoteEndPoint,
    DateTime ConnectedAt,
    DateTime LastSeenAt,
    byte? LastFunctionCode,
    ushort? LastStartAddress,
    ushort? LastPointCount,
    int RequestCount,
    bool IsConnected);

public sealed class ModbusMonitoringClientModel : ObservableObject
{
    private string _remoteEndPoint = string.Empty;
    private DateTime _connectedAt;
    private DateTime _lastSeenAt;
    private byte? _lastFunctionCode;
    private ushort? _lastStartAddress;
    private ushort? _lastPointCount;
    private int _requestCount;
    private bool _isConnected;

    public ModbusMonitoringClientModel(ModbusMonitoringClientStatus status)
    {
        ClientId = status.ClientId;
        Apply(status);
    }

    public string ClientId { get; }

    public string RemoteEndPoint
    {
        get => _remoteEndPoint;
        private set => SetProperty(ref _remoteEndPoint, value);
    }

    public DateTime ConnectedAt
    {
        get => _connectedAt;
        private set => SetProperty(ref _connectedAt, value);
    }

    public DateTime LastSeenAt
    {
        get => _lastSeenAt;
        private set => SetProperty(ref _lastSeenAt, value);
    }

    public byte? LastFunctionCode
    {
        get => _lastFunctionCode;
        private set
        {
            if (SetProperty(ref _lastFunctionCode, value))
            {
                RaisePropertyChanged(nameof(LastRequestText));
            }
        }
    }

    public ushort? LastStartAddress
    {
        get => _lastStartAddress;
        private set
        {
            if (SetProperty(ref _lastStartAddress, value))
            {
                RaisePropertyChanged(nameof(LastRequestText));
            }
        }
    }

    public ushort? LastPointCount
    {
        get => _lastPointCount;
        private set
        {
            if (SetProperty(ref _lastPointCount, value))
            {
                RaisePropertyChanged(nameof(LastRequestText));
            }
        }
    }

    public int RequestCount
    {
        get => _requestCount;
        private set => SetProperty(ref _requestCount, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetProperty(ref _isConnected, value))
            {
                RaisePropertyChanged(nameof(StatusText));
            }
        }
    }

    public string StatusText => IsConnected ? "Connected" : "Disconnected";

    public string LastRequestText =>
        LastFunctionCode is null
            ? "-"
            : $"FC{LastFunctionCode:00} / {LastStartAddress} / {LastPointCount}";

    public void Apply(ModbusMonitoringClientStatus status)
    {
        RemoteEndPoint = status.RemoteEndPoint;
        ConnectedAt = status.ConnectedAt;
        LastSeenAt = status.LastSeenAt;
        LastFunctionCode = status.LastFunctionCode;
        LastStartAddress = status.LastStartAddress;
        LastPointCount = status.LastPointCount;
        RequestCount = status.RequestCount;
        IsConnected = status.IsConnected;
    }
}
