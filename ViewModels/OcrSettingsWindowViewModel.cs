using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using TestMcAlgorithm.Models;

namespace TestMcAlgorithm.ViewModels;

public sealed class OcrSettingsWindowViewModel : ObservableObject
{
    private const ushort NetworkWriteAccessAddress = 484;
    private const ushort OcfdAddress = 556;
    private const ushort OcftAddress = 557;
    private const ushort IpAddressStartAddress = 3000;
    private const ushort IpAddressRegisterCount = 4;

    private readonly LineSimulatorViewModel _lineSimulator;
    private readonly Window? _ownerWindow;
    private OvrEndpointSettingsModel? _selectedOcrEndpoint;
    private string _ipAddressText = string.Empty;
    private bool _networkWriteAccess;
    private string _ocfdText = string.Empty;
    private string _ocftText = string.Empty;
    private string _statusMessage = "활성화된 OCR을 선택하면 현재 설정을 불러옵니다.";
    private bool _isLoadingSettings;

    public OcrSettingsWindowViewModel(LineSimulatorViewModel lineSimulator, Window? ownerWindow = null)
    {
        _lineSimulator = lineSimulator;
        _ownerWindow = ownerWindow;

        EnabledOcrEndpoints = [];
        EnabledPmEndpoints = [];

        RefreshEnabledEndpointsCommand = new AsyncRelayCommand(_ => RefreshEnabledEndpointsAsync());
        ReloadSelectedOcrSettingsCommand = new AsyncRelayCommand(_ => LoadSelectedOcrSettingsAsync());
        ApplyIpAddressCommand = new AsyncRelayCommand(_ => ApplyIpAddressAsync());
        ApplyOcfdCommand = new AsyncRelayCommand(_ => ApplyOcfdAsync());
        ApplyOcftCommand = new AsyncRelayCommand(_ => ApplyOcftAsync());
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke());

        RefreshEnabledEndpoints();
        if (SelectedOcrEndpoint is not null)
        {
            _ = LoadSelectedOcrSettingsAsync();
        }
    }

    public event Action? CloseRequested;

    public ObservableCollection<OvrEndpointSettingsModel> EnabledOcrEndpoints { get; }

    public ObservableCollection<OvrEndpointSettingsModel> EnabledPmEndpoints { get; }

    public OvrEndpointSettingsModel? SelectedOcrEndpoint
    {
        get => _selectedOcrEndpoint;
        set
        {
            if (!SetProperty(ref _selectedOcrEndpoint, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(CanEditSelectedOcr));
            RaisePropertyChanged(nameof(SelectedOcrName));
            RaisePropertyChanged(nameof(SelectedOcrDeviceKey));
            RaisePropertyChanged(nameof(SelectedOcrEndpointAddress));

            if (_selectedOcrEndpoint is null)
            {
                ClearLoadedSettings();
                StatusMessage = "활성화된 OCR이 없습니다.";
                return;
            }

            _ = LoadSelectedOcrSettingsAsync();
        }
    }

    public string IpAddressText
    {
        get => _ipAddressText;
        set => SetProperty(ref _ipAddressText, value);
    }

    public bool NetworkWriteAccess
    {
        get => _networkWriteAccess;
        set
        {
            if (!SetProperty(ref _networkWriteAccess, value))
            {
                return;
            }

            if (_isLoadingSettings || SelectedOcrEndpoint is null)
            {
                return;
            }

            _ = UpdateNetworkWriteAccessAsync(value);
        }
    }

    public string OcfdText
    {
        get => _ocfdText;
        set => SetProperty(ref _ocfdText, value);
    }

    public string OcftText
    {
        get => _ocftText;
        set => SetProperty(ref _ocftText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool CanEditSelectedOcr => SelectedOcrEndpoint is not null;

    public string SelectedOcrName => SelectedOcrEndpoint?.Name ?? "-";

    public string SelectedOcrDeviceKey => SelectedOcrEndpoint?.DeviceKey ?? "-";

    public string SelectedOcrEndpointAddress =>
        SelectedOcrEndpoint is null
            ? "-"
            : $"{SelectedOcrEndpoint.IpAddress}:{SelectedOcrEndpoint.Port} / Slave {SelectedOcrEndpoint.SlaveId}";

    public string EnabledOcrSummary =>
        EnabledOcrEndpoints.Count == 0
            ? "활성화된 OCR이 없습니다."
            : string.Join(", ", EnabledOcrEndpoints.Select(endpoint => endpoint.DeviceKey));

    public string EnabledPmSummary =>
        EnabledPmEndpoints.Count == 0
            ? "활성화된 PM이 없습니다."
            : string.Join(", ", EnabledPmEndpoints.Select(endpoint => endpoint.DeviceKey));

    public AsyncRelayCommand RefreshEnabledEndpointsCommand { get; }

    public AsyncRelayCommand ReloadSelectedOcrSettingsCommand { get; }

    public AsyncRelayCommand ApplyIpAddressCommand { get; }

    public AsyncRelayCommand ApplyOcfdCommand { get; }

    public AsyncRelayCommand ApplyOcftCommand { get; }

    public RelayCommand CloseCommand { get; }

    private async Task RefreshEnabledEndpointsAsync()
    {
        RefreshEnabledEndpoints();
        if (SelectedOcrEndpoint is not null)
        {
            await LoadSelectedOcrSettingsAsync();
        }
    }

    private void RefreshEnabledEndpoints()
    {
        var previousSelectedKey = SelectedOcrEndpoint?.DeviceKey;

        EnabledOcrEndpoints.Clear();
        foreach (var endpoint in _lineSimulator.State.OvrSettings.Endpoints.Where(endpoint => endpoint.IsEnabled && endpoint.IsOvr))
        {
            EnabledOcrEndpoints.Add(endpoint);
        }

        EnabledPmEndpoints.Clear();
        foreach (var endpoint in _lineSimulator.State.OvrSettings.Endpoints.Where(endpoint => endpoint.IsEnabled && !endpoint.IsOvr))
        {
            EnabledPmEndpoints.Add(endpoint);
        }

        RaisePropertyChanged(nameof(EnabledOcrSummary));
        RaisePropertyChanged(nameof(EnabledPmSummary));

        SelectedOcrEndpoint =
            EnabledOcrEndpoints.FirstOrDefault(endpoint => endpoint.DeviceKey == previousSelectedKey) ??
            EnabledOcrEndpoints.FirstOrDefault();

        if (SelectedOcrEndpoint is null)
        {
            ClearLoadedSettings();
        }
    }

    private async Task LoadSelectedOcrSettingsAsync()
    {
        if (SelectedOcrEndpoint is null)
        {
            StatusMessage = "활성화된 OCR이 없습니다.";
            return;
        }

        try
        {
            var endpointName = SelectedOcrEndpoint.DeviceKey;
            StatusMessage = $"{endpointName} 설정을 읽는 중...";

            var ipRegisters = await _lineSimulator.ReadOvrEndpointHoldingRegistersAsync(endpointName, IpAddressStartAddress, IpAddressRegisterCount);
            var accessRegister = await _lineSimulator.ReadOvrEndpointHoldingRegistersAsync(endpointName, NetworkWriteAccessAddress, 1);
            var protectionRegisters = await _lineSimulator.ReadOvrEndpointHoldingRegistersAsync(endpointName, OcfdAddress, 2);

            _isLoadingSettings = true;
            try
            {
                IpAddressText = string.Join(".", ipRegisters.Select(value => value.ToString()));
                NetworkWriteAccess = accessRegister[0] == 1;
                OcfdText = protectionRegisters[0].ToString();
                OcftText = protectionRegisters[1].ToString();
            }
            finally
            {
                _isLoadingSettings = false;
            }

            StatusMessage = $"{endpointName} 설정을 불러왔습니다.";
        }
        catch (Exception ex)
        {
            _isLoadingSettings = false;
            StatusMessage = $"설정 읽기 실패: {ex.Message}";
            ShowMessageBox(
                $"{SelectedOcrEndpoint.DeviceKey} 설정을 읽는 중 오류가 발생했습니다.\n{ex.Message}",
                "OCR Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task ApplyIpAddressAsync()
    {
        if (SelectedOcrEndpoint is null)
        {
            return;
        }

        if (!TryParseIpAddress(IpAddressText, out var values))
        {
            ShowMessageBox(
                "IP 주소 형식이 올바르지 않습니다.\n예: 192.168.0.10",
                "IP 변경",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var endpointName = SelectedOcrEndpoint.DeviceKey;
        var message = $"{endpointName}의 IP 주소를 {string.Join(".", values)} 로 변경하시겠습니까?";
        if (ShowMessageBox(message, "IP 변경", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        for (var index = 0; index < values.Length; index++)
        {
            await _lineSimulator.WriteOvrEndpointRegisterAsync(
                endpointName,
                (ushort)(IpAddressStartAddress + index),
                values[index]);
        }
        await LoadSelectedOcrSettingsAsync();

        var updatedIp = string.Join(".", values.Select(value => value.ToString()));
        if (string.Equals(IpAddressText, updatedIp, StringComparison.Ordinal))
        {
            ShowMessageBox("IP 주소를 변경했습니다.", "IP 변경", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ShowMessageBox(
            "IP 주소 쓰기 후 재조회 값이 요청한 값과 다릅니다.",
            "IP 변경",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private async Task UpdateNetworkWriteAccessAsync(bool value)
    {
        if (SelectedOcrEndpoint is null)
        {
            return;
        }

        try
        {
            var endpointName = SelectedOcrEndpoint.DeviceKey;
            await _lineSimulator.WriteOvrEndpointRegisterAsync(endpointName, NetworkWriteAccessAddress, value ? (ushort)1 : (ushort)0);

            var verify = await _lineSimulator.ReadOvrEndpointHoldingRegistersAsync(endpointName, NetworkWriteAccessAddress, 1);
            var actualValue = verify[0] == 1;
            if (actualValue == value)
            {
                StatusMessage = $"{endpointName} Network Write Access = {(value ? "True" : "False")}";
                return;
            }

            _isLoadingSettings = true;
            try
            {
                NetworkWriteAccess = actualValue;
            }
            finally
            {
                _isLoadingSettings = false;
            }

            ShowMessageBox(
                "Network Write Access 쓰기 후 재조회 값이 요청한 값과 다릅니다.",
                "Network Write Access",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            await LoadSelectedOcrSettingsAsync();
            ShowMessageBox(
                $"Network Write Access 변경 중 오류가 발생했습니다.\n{ex.Message}",
                "Network Write Access",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task ApplyOcfdAsync()
    {
        await ApplyProtectionSettingAsync(
            registerAddress: OcfdAddress,
            registerName: "OCFD",
            inputText: OcfdText,
            minValue: 20,
            maxValue: 12000,
            successGetter: () => OcfdText);
    }

    private async Task ApplyOcftAsync()
    {
        await ApplyProtectionSettingAsync(
            registerAddress: OcftAddress,
            registerName: "OCFT",
            inputText: OcftText,
            minValue: 5,
            maxValue: 1000,
            successGetter: () => OcftText);
    }

    private async Task ApplyProtectionSettingAsync(
        ushort registerAddress,
        string registerName,
        string inputText,
        ushort minValue,
        ushort maxValue,
        Func<string> successGetter)
    {
        if (SelectedOcrEndpoint is null)
        {
            return;
        }

        if (!ushort.TryParse(inputText, out var value) || value < minValue || value > maxValue)
        {
            ShowMessageBox(
                $"{registerName} 값 범위가 올바르지 않습니다.\n허용 범위: {minValue} ~ {maxValue}",
                $"{registerName} 변경",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var endpointName = SelectedOcrEndpoint.DeviceKey;
        var message = $"{endpointName}의 {registerName} 값을 {value} 로 변경하시겠습니까?";
        if (ShowMessageBox(message, $"{registerName} 변경", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        await _lineSimulator.WriteOvrEndpointRegisterAsync(endpointName, registerAddress, value);
        await LoadSelectedOcrSettingsAsync();

        if (string.Equals(successGetter(), value.ToString(), StringComparison.Ordinal))
        {
            ShowMessageBox($"{registerName} 값을 변경했습니다.", $"{registerName} 변경", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ShowMessageBox(
            $"{registerName} 쓰기 후 재조회 값이 요청한 값과 다릅니다.",
            $"{registerName} 변경",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private MessageBoxResult ShowMessageBox(
        string message,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon)
    {
        return _ownerWindow is null
            ? MessageBox.Show(message, caption, button, icon)
            : MessageBox.Show(_ownerWindow, message, caption, button, icon);
    }

    private void ClearLoadedSettings()
    {
        _isLoadingSettings = true;
        try
        {
            IpAddressText = string.Empty;
            NetworkWriteAccess = false;
            OcfdText = string.Empty;
            OcftText = string.Empty;
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private static bool TryParseIpAddress(string text, out ushort[] values)
    {
        values = [];

        var tokens = text
            .Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length != 4)
        {
            return false;
        }

        var parsed = new ushort[4];
        for (var index = 0; index < tokens.Length; index++)
        {
            if (!byte.TryParse(tokens[index], out var octet))
            {
                return false;
            }

            parsed[index] = octet;
        }

        values = parsed;
        return true;
    }
}
