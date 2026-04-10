using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Windows;
using TestMcAlgorithm.Models;
using TestMcAlgorithm.Services;
using static TestMcAlgorithm.Models.OvrEndpointSettingsModel;

namespace TestMcAlgorithm.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    public double SampleValue { get; set; } = 123.0;

    private const ushort LineRegisterStartAddress = 0;
    private const ushort LineRegisterCount = 10;
    private const ushort EndpointRegisterStartAddress = 476;
    private const ushort EndpointRegisterCount = 26;

    public BusDiagram BusDiagram { get; }
    private const int MaxApplyFeedbackRetryCount = 3;
    private const ushort AlarmCoilAddress = 100;

    private readonly McAlgorithmService _algorithmService;
    private readonly IModbusGatewayService _modbusGatewayService;
    private readonly SemaphoreSlim _modbusIoLock = new(1, 1);
    private bool _isRefreshingSelections;
    private bool _isBusOperationRunning;
    private CancellationTokenSource? _feedbackPollingCts;
    private Task? _feedbackPollingTask;
    private CancellationTokenSource? _ovrPollingCts;
    private Task? _ovrPollingTask;
    private CancellationTokenSource? _idleMonitorCts;
    private Task? _idleMonitorTask;
    private bool _isShuttingDown;
    private bool _feedbackReadFailed;

    private IReadOnlyList<BusRequestSpec> _availableBus2Requests = [];
    private IReadOnlyList<BusRequestSpec> _availableBus3Requests = [];
    private AlgorithmPlan? _currentPlan;

    public MainViewModel(McAlgorithmService algorithmService, IModbusGatewayService modbusGatewayService)
    {
        _algorithmService = algorithmService;
        _modbusGatewayService = modbusGatewayService;
        BusDiagram = new BusDiagram();
        BusDiagram.KBusClickRequestedAsync = HandleDiagramKBusClickAsync;
        State = new MainScreenStateModel();

        McItems = new ObservableCollection<McButtonViewModel>(McCatalog.All.Select(definition => new McButtonViewModel(definition)));
        KItems = new ObservableCollection<KContactViewModel>(KCatalog.All.OrderBy(definition => definition.Number).Select(definition => new KContactViewModel(definition)));
        Logs = new ObservableCollection<string>();

        Bus1RatedOptions = new ObservableCollection<double>(_algorithmService.SupportedRatedKva);
        Bus1ScrOptions = new ObservableCollection<double>(_algorithmService.SupportedScr);
        Bus2RatedOptions = new ObservableCollection<double>();
        Bus2ScrOptions = new ObservableCollection<double>();
        Bus3RatedOptions = new ObservableCollection<double>();
        Bus3ScrOptions = new ObservableCollection<double>();

        ConnectCommand = new AsyncRelayCommand(_ => ConnectAsync(), _ => !State.Connection.IsConnected);
        DisconnectCommand = new AsyncRelayCommand(_ => DisconnectAsync(), _ => State.Connection.IsConnected);
        ClearLogsCommand = new RelayCommand(_ => Logs.Clear());
        ShowOvrSettingsCommand = new RelayCommand(_ => State.OvrSettings.IsVisible = true);
        CloseOvrSettingsCommand = new RelayCommand(_ => State.OvrSettings.IsVisible = false);
        ApplyOvrSettingsCommand = new AsyncRelayCommand(_ => ApplyOvrSettingsAsync());
        Bus1ApplyCommand = new AsyncRelayCommand(_ => TurnBusOnAsync("BUS1"), _ => CanApplyBus("BUS1"));
        Bus2ApplyCommand = new AsyncRelayCommand(_ => TurnBusOnAsync("BUS2"), _ => CanApplyBus("BUS2"));
        Bus3ApplyCommand = new AsyncRelayCommand(_ => TurnBusOnAsync("BUS3"), _ => CanApplyBus("BUS3"));
        Bus1OffCommand = new AsyncRelayCommand(_ => TurnBusOffAsync("BUS1"), _ => CanOffBus("BUS1"));
        Bus2OffCommand = new AsyncRelayCommand(_ => TurnBusOffAsync("BUS2"), _ => CanOffBus("BUS2"));
        Bus3OffCommand = new AsyncRelayCommand(_ => TurnBusOffAsync("BUS3"), _ => CanOffBus("BUS3"));

        SubscribeStateEvents();
        RefreshBusAvailability();
        SyncBusDiagramFeedback();
        InitializeEndpointDefaults();
        AutoCalculatePlan();
        StartIdleMonitor();
    }

    private void InitializeEndpointDefaults()
    {

        State.OvrSettings.Endpoints[0].IpAddress = "192.168.1.10"; // ocr1
        State.OvrSettings.Endpoints[1].IpAddress = "192.168.1.11"; // ocr2
        State.OvrSettings.Endpoints[2].IpAddress = "192.168.1.12"; // ocr3
        State.OvrSettings.Endpoints[3].IpAddress = "192.168.1.13"; // ocr4
        State.OvrSettings.Endpoints[4].IpAddress = "192.168.1.14"; // ocr5
        State.OvrSettings.Endpoints[5].IpAddress = "192.168.1.15"; // ocr6
        State.OvrSettings.Endpoints[6].IpAddress = "192.168.1.16"; // ocr7
        State.OvrSettings.Endpoints[7].IpAddress = "192.168.1.17"; // ocr8
        State.OvrSettings.Endpoints[8].IpAddress = "192.168.1.18"; // ocr9
        State.OvrSettings.Endpoints[9].IpAddress = "192.168.1.19"; // ocr10

        State.OvrSettings.Endpoints[10].IpAddress = "192.168.1.20";// bus in Meter계
        State.OvrSettings.Endpoints[11].IpAddress = "192.168.1.21";// bus out1 Meter계
        State.OvrSettings.Endpoints[12].IpAddress = "192.168.1.22";// bus out2 Meter계
        State.OvrSettings.Endpoints[13].IpAddress = "192.168.1.23";// bus out3 Meter계

        foreach (var endpoint in State.OvrSettings.Endpoints)
        {
            endpoint.CurrentRegisterAddress = 500; // 기본적으로 전류값이 읽히는 레지스터 주소
            endpoint.CurrentScale = 0.01; // 기본적으로 전류값이 읽히는 레지스터 주소
        }
    }
    public MainScreenStateModel State { get; }
    public ObservableCollection<McButtonViewModel> McItems { get; }
    public ObservableCollection<KContactViewModel> KItems { get; }
    public ObservableCollection<string> Logs { get; }
    public ObservableCollection<double> Bus1RatedOptions { get; }
    public ObservableCollection<double> Bus1ScrOptions { get; }
    public ObservableCollection<double> Bus2RatedOptions { get; }
    public ObservableCollection<double> Bus2ScrOptions { get; }
    public ObservableCollection<double> Bus3RatedOptions { get; }
    public ObservableCollection<double> Bus3ScrOptions { get; }

    public AsyncRelayCommand ConnectCommand { get; }
    public AsyncRelayCommand DisconnectCommand { get; }
    public RelayCommand ClearLogsCommand { get; }
    public RelayCommand ShowOvrSettingsCommand { get; }
    public RelayCommand CloseOvrSettingsCommand { get; }
    public AsyncRelayCommand ApplyOvrSettingsCommand { get; }
    public AsyncRelayCommand Bus1ApplyCommand { get; }
    public AsyncRelayCommand Bus2ApplyCommand { get; }
    public AsyncRelayCommand Bus3ApplyCommand { get; }
    public AsyncRelayCommand Bus1OffCommand { get; }
    public AsyncRelayCommand Bus2OffCommand { get; }
    public AsyncRelayCommand Bus3OffCommand { get; }

    private static ushort FeedbackStartAddress => KCatalog.All.Min(item => item.FeedbackAddress);
    private static ushort FeedbackCount => (ushort)(KCatalog.All.Max(item => item.FeedbackAddress) - FeedbackStartAddress + 1);

    private void SubscribeStateEvents()
    {
        State.Connection.PropertyChanged += OnConnectionPropertyChanged;
        State.Bus1.PropertyChanged += OnBus1PropertyChanged;
        State.Bus2.PropertyChanged += OnBus2PropertyChanged;
        State.Bus3.PropertyChanged += OnBus3PropertyChanged;
    }

    private void UnsubscribeStateEvents()
    {
        State.Connection.PropertyChanged -= OnConnectionPropertyChanged;
        State.Bus1.PropertyChanged -= OnBus1PropertyChanged;
        State.Bus2.PropertyChanged -= OnBus2PropertyChanged;
        State.Bus3.PropertyChanged -= OnBus3PropertyChanged;
    }

    private void OnConnectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectionSettingsModel.IsConnected))
        {
            ConnectCommand.RaiseCanExecuteChanged();
            DisconnectCommand.RaiseCanExecuteChanged();
        }
    }

    private void OnBus1PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BusSelectionModel.IsApplied))
        {
            RaiseBusCommandCanExecuteChanged();
        }

        if (_isRefreshingSelections)
        {
            return;
        }

        if (e.PropertyName is nameof(BusSelectionModel.RatedKva) or nameof(BusSelectionModel.Scr))
        {
            _currentPlan = null;
            RefreshBusAvailability();
            AutoCalculatePlan();
        }
    }

    private void OnBus2PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isRefreshingSelections)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(BusSelectionModel.IsEnabled):
                _currentPlan = null;
                if (!State.Bus2.IsEnabled && State.Bus3.IsEnabled)
                {
                    State.Bus3.IsEnabled = false;
                }

                RefreshBus3Availability();
                RaiseBusCommandCanExecuteChanged();
                AutoCalculatePlan();
                break;

            case nameof(BusSelectionModel.IsApplied):
                RaiseBusCommandCanExecuteChanged();
                break;

            case nameof(BusSelectionModel.RatedKva):
                _currentPlan = null;
                RefreshBus2ScrOptionsOnly();
                RefreshBus3Availability();
                AutoCalculatePlan();
                break;

            case nameof(BusSelectionModel.Scr):
                _currentPlan = null;
                RefreshBus3Availability();
                AutoCalculatePlan();
                break;
        }
    }

    private void OnBus3PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isRefreshingSelections)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(BusSelectionModel.IsEnabled):
                if (State.Bus3.IsEnabled && !State.Bus2.IsEnabled)
                {
                    State.Bus3.IsEnabled = false;
                    return;
                }

                _currentPlan = null;
                RaiseBusCommandCanExecuteChanged();
                AutoCalculatePlan();
                break;

            case nameof(BusSelectionModel.IsApplied):
                RaiseBusCommandCanExecuteChanged();
                break;

            case nameof(BusSelectionModel.RatedKva):
                _currentPlan = null;
                RefreshBus3ScrOptionsOnly();
                AutoCalculatePlan();
                break;

            case nameof(BusSelectionModel.Scr):
                _currentPlan = null;
                AutoCalculatePlan();
                break;
        }
    }

    private async Task ConnectAsync()
    {
        try
        {
            await StopFeedbackPollingAsync();
            await _modbusGatewayService.ConnectAsync(State.Connection.IpAddress, State.Connection.Port, CancellationToken.None); // line simulator 
            State.Connection.IsConnected = true;
            _feedbackReadFailed = false;
            AddLog($"Connected to {State.Connection.IpAddress}:{State.Connection.Port}");

            try
            {
                var lineRegisters = await ReadLineSimulatorRegistersAsync(CancellationToken.None);
                AddLog($"Line simulator register read ready: {LineRegisterStartAddress}~{LineRegisterStartAddress + LineRegisterCount - 1} ({lineRegisters.Length} words)");
            }
            catch (Exception ex)
            {
                AddLog($"Line simulator register read failed: {ex.Message}");
            }

            StartFeedbackPolling();
        }
        catch (Exception ex)
        {
            AddLog($"Connect failed: {ex.Message}");
        }
    }

    private async Task DisconnectAsync()
    {
        await DisconnectCoreAsync("Disconnected", stopPolling: true);
    }

    private void CalculatePlan(bool writeLog = true)
    {
        try
        {
            _currentPlan = BuildPlan();
            ApplyPlanToView(_currentPlan);
            if (writeLog)
            {
                AddLog("Algorithm plan calculated.");
            }
        }
        catch (Exception ex)
        {
            State.Bus1.Summary = BuildBusSummary("BUS1", null, isEnabled: true);
            State.Bus2.Summary = BuildBusSummary("BUS2", null, State.Bus2.IsEnabled);
            State.Bus3.Summary = BuildBusSummary("BUS3", null, State.Bus3.IsEnabled);
            State.AlgorithmSummary = ex.Message;
            if (writeLog)
            {
                AddLog($"Calculate failed: {ex.Message}");
            }
        }
    }

    private void AutoCalculatePlan()
    {
        if (_isRefreshingSelections)
        {
            return;
        }

        CalculatePlan(writeLog: false);
    }

    private async Task TurnBusOnAsync(string busName)
    {
        if (!TryBeginBusOperation()) return;

        try
        {
            try
            {
                _currentPlan ??= BuildPlan();
            }
            catch (Exception ex)
            {
                AddLog($"Apply aborted: {ex.Message}");
                return;
            }

            var previousAssignments = McItems
                .Where(item => item.IsAlgorithmManaged && item.AssignedBus == busName)
                .Select(item => item.Number)
                .ToHashSet();

            ApplyPlanToView(_currentPlan); // 새 계획을 뷰에 먼저 적용하여, 사용자에게 변경 내용을 명확히 보여주고, 이후 실제 ON/OFF 작업에서는 이 계획을 기준으로 피드백을 확인하도록 함
            var target = GetBusResult(_currentPlan, busName);
            if (target is null || !target.IsAssigned)
            {
                AddLog($"{busName} apply skipped: valid selection 없음");
                return;
            }

            var selected = target.McNumbers.ToHashSet();
            var selectedByAnyBus = _currentPlan.OrderedTurnOnNumbers.ToHashSet();

            foreach (var number in previousAssignments.Where(number => !selected.Contains(number) && !selectedByAnyBus.Contains(number)).OrderByDescending(number => number)) // 현재 버스에 할당되어 있지만, 새 계획에서 어떤 버스에도 할당되지 않은 MC는 먼저 OFF
            {
                var kItem = ResolveKItem(number, busName);
                var cleared = await TurnKOffWithFeedbackCheckAsync(kItem, $"Algorithm clear {kItem.Code}");
                if (!cleared)
                {
                    AddLog($"{busName} apply aborted: {kItem.Code} clear feedback not confirmed.");
                    return;
                }
            }

            foreach (var number in target.McNumbers.OrderBy(number => number)) // 새 계획에서 할당된 MC는 번호 순서대로 ON (일반적으로 낮은 번호가 높은 우선순위이므로)
            {
                var kItem = ResolveKItem(number, busName);
                var applied = await ApplyKWithFeedbackCheckAsync(kItem, busName);
                if (!applied)
                {
                    AddLog($"{busName} apply aborted: {kItem.Code} feedback not confirmed.");
                    return;
                }
            }

            SetBusApplied(busName, true);
            AddLog($"{busName} apply sequence completed.");
        }
        finally
        {
            EndBusOperation();
        }
    }

    private static BusSelectionResult? GetBusResult(AlgorithmPlan plan, string busName)
    {
        return busName switch
        {
            "BUS1" => plan.Bus1,
            "BUS2" => plan.Bus2,
            "BUS3" => plan.Bus3,
            _ => null,
        };
    }

    private async Task TurnBusOffAsync(string busName)
    {
        if (!TryBeginBusOperation())
        {
            return;
        }

        try
        {
            var busItems = McItems
                .Where(item => item.AssignedBus == busName)
                .OrderByDescending(item => item.Number)
                .ToArray();

            foreach (var mc in busItems)
            {
                var kItem = ResolveKItem(mc.Number, busName);
                var turnedOff = await TurnKOffWithFeedbackCheckAsync(kItem, $"{busName} off");
                if (!turnedOff)
                {
                    AddLog($"{busName} off aborted: {kItem.Code} feedback not confirmed.");
                    return;
                }
            }

            foreach (var mc in busItems)
            {
                mc.AssignedBus = "-";
            }

            _currentPlan = null;
            SetBusApplied(busName, false);
            AddLog($"{busName} off sequence completed.");
        }
        finally
        {
            EndBusOperation();
        }
    }

    private async Task HandleDiagramKBusClickAsync(string kCode)
    {
        if (!TryBeginBusOperation())
        {
            AddLog($"Manual control skipped: another operation is running.");
            return;
        }

        try
        {
            var kItem = KItems.FirstOrDefault(item => string.Equals(item.Code, kCode, StringComparison.OrdinalIgnoreCase));
            if (kItem is null)
            {
                AddLog($"{kCode} manual control unavailable: K mapping not found.");
                return;
            }

            var operationBus = kItem.TargetBus;

            if (kItem.IsOn)
            {
                var turnedOff = await TurnKOffWithFeedbackCheckAsync(kItem, $"{operationBus} manual off");
                if (turnedOff)
                {
                    AddLog($"{kCode} manual off completed.");
                }

                return;
            }

            var interlockedPeer = FindInterlockedPeer(kItem);
            if (interlockedPeer is not null)
            {
                AddLog($"{kCode} manual on blocked: {interlockedPeer.Code} is already ON for MC{interlockedPeer.Definition.SourceMcNumber}.");
                return;
            }

            var applied = await ApplyKWithFeedbackCheckAsync(kItem, $"{operationBus} manual");
            if (applied)
            {
                AddLog($"{kCode} manual on completed.");
            }
        }
        finally
        {
            EndBusOperation();
        }
    }

    private AlgorithmPlan BuildPlan()
    {
        var bus1 = new BusRequestSpec("BUS1", State.Bus1.RatedKva, State.Bus1.Scr);
        if (!_algorithmService.CanBuildBus1(bus1))
        {
            throw new InvalidOperationException($"BUS1 조합 없음: {State.Bus1.RatedKva:g}kVA / SCR {State.Bus1.Scr:g}");
        }

        BusRequestSpec? bus2 = State.Bus2.IsEnabled && HasValidSelection(_availableBus2Requests, State.Bus2.RatedKva, State.Bus2.Scr)
            ? new BusRequestSpec("BUS2", State.Bus2.RatedKva, State.Bus2.Scr)
            : null;

        BusRequestSpec? bus3 = State.Bus3.IsEnabled &&
                               bus2 is not null &&
                               HasValidSelection(_availableBus3Requests, State.Bus3.RatedKva, State.Bus3.Scr)
            ? new BusRequestSpec("BUS3", State.Bus3.RatedKva, State.Bus3.Scr)
            : null;

        return _algorithmService.BuildPlan(bus1, bus2, bus3);
    }

    private void RefreshBusAvailability()
    {
        _isRefreshingSelections = true;
        try
        {
            RefreshBus2Availability();
            RefreshBus3Availability();
        }
        finally
        {
            _isRefreshingSelections = false;
        }
    }

    private void RefreshBus2Availability()
    {
        _availableBus2Requests = [];
        Bus2RatedOptions.Clear();
        Bus2ScrOptions.Clear();

        var bus1 = new BusRequestSpec("BUS1", State.Bus1.RatedKva, State.Bus1.Scr);
        if (!_algorithmService.CanBuildBus1(bus1))
        {
            if (State.Bus3.IsEnabled)
            {
                State.Bus3.IsEnabled = false;
            }

            return;
        }

        _availableBus2Requests = _algorithmService.GetAvailableBus2Requests(bus1);
        PopulateRatedOptions(Bus2RatedOptions, _availableBus2Requests);
        EnsureSelectedRated(Bus2RatedOptions, value => State.Bus2.RatedKva = value, State.Bus2.RatedKva);
        RefreshBus2ScrOptionsOnly();
    }

    private void RefreshBus3Availability()
    {
        _availableBus3Requests = [];
        Bus3RatedOptions.Clear();
        Bus3ScrOptions.Clear();

        if (!State.Bus2.IsEnabled || !HasValidSelection(_availableBus2Requests, State.Bus2.RatedKva, State.Bus2.Scr))
        {
            if (State.Bus3.IsEnabled)
            {
                State.Bus3.IsEnabled = false;
            }

            return;
        }

        var bus1 = new BusRequestSpec("BUS1", State.Bus1.RatedKva, State.Bus1.Scr);
        var bus2 = new BusRequestSpec("BUS2", State.Bus2.RatedKva, State.Bus2.Scr);
        _availableBus3Requests = _algorithmService.GetAvailableBus3Requests(bus1, bus2);

        PopulateRatedOptions(Bus3RatedOptions, _availableBus3Requests);
        EnsureSelectedRated(Bus3RatedOptions, value => State.Bus3.RatedKva = value, State.Bus3.RatedKva);
        RefreshBus3ScrOptionsOnly();

        if (_availableBus3Requests.Count == 0 && State.Bus3.IsEnabled)
        {
            State.Bus3.IsEnabled = false;
        }
    }

    private void RefreshBus2ScrOptionsOnly()
    {
        Bus2ScrOptions.Clear();
        if (_availableBus2Requests.Count == 0)
        {
            return;
        }

        PopulateScrOptions(Bus2ScrOptions, _availableBus2Requests, State.Bus2.RatedKva);
        EnsureSelectedScr(Bus2ScrOptions, value => State.Bus2.Scr = value, State.Bus2.Scr);
    }

    private void RefreshBus3ScrOptionsOnly()
    {
        Bus3ScrOptions.Clear();
        if (_availableBus3Requests.Count == 0)
        {
            return;
        }

        PopulateScrOptions(Bus3ScrOptions, _availableBus3Requests, State.Bus3.RatedKva);
        EnsureSelectedScr(Bus3ScrOptions, value => State.Bus3.Scr = value, State.Bus3.Scr);
    }

    private static bool HasValidSelection(IReadOnlyList<BusRequestSpec> requests, double ratedKva, double scr)
    {
        return requests.Any(request => request.RatedKva.Equals(ratedKva) && request.Scr.Equals(scr));
    }

    private void PopulateRatedOptions(ObservableCollection<double> target, IReadOnlyList<BusRequestSpec> requests)
    {
        var values = _algorithmService.SupportedRatedKva
            .Where(rated => requests.Any(request => request.RatedKva.Equals(rated)))
            .ToArray();

        ReplaceItems(target, values);
    }

    private void PopulateScrOptions(ObservableCollection<double> target, IReadOnlyList<BusRequestSpec> requests, double ratedKva)
    {
        var values = _algorithmService.SupportedScr
            .Where(scr => requests.Any(request => request.RatedKva.Equals(ratedKva) && request.Scr.Equals(scr)))
            .ToArray();

        ReplaceItems(target, values);
    }

    private static void EnsureSelectedRated(ObservableCollection<double> options, Action<double> applySelection, double currentValue)
    {
        if (options.Count == 0)
        {
            return;
        }

        if (!options.Contains(currentValue))
        {
            applySelection(options[0]);
        }
    }

    private static void EnsureSelectedScr(ObservableCollection<double> options, Action<double> applySelection, double currentValue)
    {
        if (options.Count == 0)
        {
            return;
        }

        if (!options.Contains(currentValue))
        {
            applySelection(options[0]);
        }
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private void ApplyPlanToView(AlgorithmPlan plan)
    {
        ClearAssignments();
        ClearKDisplayAssignments();

        State.Bus1.Summary = BuildBusSummary("BUS1", plan.Bus1, isEnabled: true);
        State.Bus2.Summary = BuildBusSummary("BUS2", plan.Bus2, State.Bus2.IsEnabled);
        State.Bus3.Summary = BuildBusSummary("BUS3", plan.Bus3, State.Bus3.IsEnabled);
        State.AlgorithmSummary = plan.Explanation;

        AssignBus(plan.Bus1, "BUS1");

        if (plan.Bus2 is { IsAssigned: true })
        {
            AssignBus(plan.Bus2, "BUS2");
        }

        if (plan.Bus3 is { IsAssigned: true })
        {
            AssignBus(plan.Bus3, "BUS3");
        }
    }

    private void AssignBus(BusSelectionResult result, string busName)
    {
        foreach (var number in result.McNumbers)
        {
            McItems.First(item => item.Number == number).AssignedBus = busName;
            SetKDisplayAssignment(number, busName);
        }
    }

    private static string BuildBusSummary(string busName, BusSelectionResult? result, bool isEnabled)
    {
        if (!isEnabled || result is null || !result.IsAssigned || result.McNumbers.Count == 0)
        {
            return $"{busName}: disable";
        }

        var kCodes = result.McNumbers
            .Select(number => KCatalog.ByMcBus.TryGetValue((number, busName), out var definition) ? definition.Code : $"MC{number}")
            .ToArray();

        return $"{busName} 투입: {string.Join(", ", kCodes)}";
    }

    private void ClearAssignments()
    {
        foreach (var mc in McItems)
        {
            mc.AssignedBus = "-";
        }
    }

    private void ClearKDisplayAssignments()
    {
        foreach (var kItem in KItems)
        {
            kItem.DisplayTargetBus = "-";
        }
    }


    private void SetKDisplayAssignment(int sourceMcNumber, string busName)
    {
        ResolveKItem(sourceMcNumber, busName).DisplayTargetBus = busName;
    }
    private KContactViewModel ResolveKItem(int sourceMcNumber, string busName)
    {
        return KItems.First(item => item.Definition.SourceMcNumber == sourceMcNumber && item.TargetBus == busName);
    }

    private async Task SetKStateAsync(KContactViewModel kItem, bool state, string reason, bool delayAfter = false)
    {
        try
        {
            if (State.Connection.IsConnected)
            {
                await WriteSingleCoilAsync(
                      (byte)State.Connection.UnitId,
                      kItem.Definition.CoilAddress,
                      state,
                      CancellationToken.None);
                AddLog($"{reason}: {kItem.Code} -> {(state ? "ON" : "OFF")} (coil {kItem.Definition.CoilAddress})");
            }
            else
            {
                kItem.IsOn = state;
                SyncBusDiagramFeedback();
                AddLog($"{reason}: {kItem.Code} -> {(state ? "ON" : "OFF")} [simulation]");
            }
        }
        catch (Exception ex)
        {
            AddLog($"{kItem.Code} write failed: {ex.Message}");
        }

        if (delayAfter)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    private async Task<bool> ApplyKWithFeedbackCheckAsync(KContactViewModel kItem, string busName)
    {
        if (!State.Connection.IsConnected)
        {
            await SetKStateAsync(kItem, true, $"{busName} apply", delayAfter: true);
            return true;
        }

        for (var attempt = 1; attempt <= MaxApplyFeedbackRetryCount; attempt++)
        {
            await SetKStateAsync(kItem, true, $"{busName} apply attempt {attempt}", delayAfter: true);

            bool feedbackMatched;
            try
            {
                feedbackMatched = await ReadKFeedbackStateAsync(kItem, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AddLog($"{kItem.Code} feedback verification failed: {ex.Message}");
                await DisconnectCoreAsync("Disconnected automatically after feedback verification failure.", stopPolling: true);
                return false;
            }

            if (feedbackMatched)
            {
                AddLog($"{kItem.Code} feedback confirmed.");
                return true;
            }

            AddLog($"{kItem.Code} feedback mismatch ({attempt}/{MaxApplyFeedbackRetryCount}).");
        }

        AddLog($"{kItem.Code} feedback retry exhausted. Alarm coil {AlarmCoilAddress} -> ON");
        //await WriteAlarmCoilAsync();
        return false;
    }

    private async Task<bool> TurnKOffWithFeedbackCheckAsync(KContactViewModel kItem, string reason)
    {
        if (!State.Connection.IsConnected)
        {
            await SetKStateAsync(kItem, false, reason, delayAfter: true);
            return true;
        }

        for (var attempt = 1; attempt <= MaxApplyFeedbackRetryCount; attempt++)
        {
            await SetKStateAsync(kItem, false, $"{reason} attempt {attempt}", delayAfter: true);

            try
            {
                var feedbackMatched = !await ReadKFeedbackStateAsync(kItem, CancellationToken.None);
                if (feedbackMatched)
                {
                    AddLog($"{kItem.Code} feedback confirmed OFF.");
                    return true;
                }

                AddLog($"{kItem.Code} off feedback mismatch ({attempt}/{MaxApplyFeedbackRetryCount}).");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AddLog($"{kItem.Code} off feedback verification failed: {ex.Message}");
                await DisconnectCoreAsync("Disconnected automatically after off feedback verification failure.", stopPolling: true);
                return false;
            }
        }

        AddLog($"{kItem.Code} off feedback retry exhausted. Alarm coil {AlarmCoilAddress} -> ON");
        //await WriteAlarmCoilAsync();
        return false;
    }

    private KContactViewModel? FindInterlockedPeer(KContactViewModel target)
    {
        if (!IsSharedMc(target.Definition.SourceMcNumber))
        {
            return null;
        }

        return KItems.FirstOrDefault(item =>
            !ReferenceEquals(item, target) &&
            item.Definition.SourceMcNumber == target.Definition.SourceMcNumber &&
            item.IsOn);
    }

    private static bool IsSharedMc(int sourceMcNumber)
    {
        return McCatalog.ByNumber.TryGetValue(sourceMcNumber, out var definition) && definition.IsShared;
    }

    private void AddLog(string message)
    {
        Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        while (Logs.Count > 200)
        {
            Logs.RemoveAt(Logs.Count - 1);
        }
    }

    private bool CanApplyBus(string busName)
    {
        if (_isBusOperationRunning)
        {
            return false;
        }

        return busName switch
        {
            "BUS1" => !State.Bus1.IsApplied,
            "BUS2" => State.Bus2.IsEnabled && State.Bus1.IsApplied && !State.Bus2.IsApplied,
            "BUS3" => State.Bus3.IsEnabled && State.Bus2.IsApplied && !State.Bus3.IsApplied,
            _ => false,
        };
    }

    private bool CanOffBus(string busName)
    {
        if (_isBusOperationRunning)
        {
            return false;
        }

        return busName switch
        {
            "BUS1" => State.Bus1.IsApplied,
            "BUS2" => State.Bus2.IsApplied,
            "BUS3" => State.Bus3.IsApplied,
            _ => false,
        };
    }

    private void SetBusApplied(string busName, bool isApplied)
    {
        switch (busName)
        {
            case "BUS1":
                State.Bus1.IsApplied = isApplied;
                break;
            case "BUS2":
                State.Bus2.IsApplied = isApplied;
                break;
            case "BUS3":
                State.Bus3.IsApplied = isApplied;
                break;
        }
    }

    private void ResetBusAppliedFlags()
    {
        State.Bus1.IsApplied = false;
        State.Bus2.IsApplied = false;
        State.Bus3.IsApplied = false;
    }

    private bool TryBeginBusOperation()
    {
        if (_isBusOperationRunning)
        {
            return false;
        }

        _isBusOperationRunning = true;
        RaiseBusCommandCanExecuteChanged();
        return true;
    }

    private void EndBusOperation()
    {
        _isBusOperationRunning = false;
        RaiseBusCommandCanExecuteChanged();
    }

    public void RequestShutdown()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;
        _feedbackPollingCts?.Cancel();
        _ovrPollingCts?.Cancel();
        _idleMonitorCts?.Cancel();
    }

    private void StartIdleMonitor()
    {
        if (_idleMonitorTask is { IsCompleted: false })
        {
            return;
        }

        _idleMonitorCts = new CancellationTokenSource();
        _idleMonitorTask = MonitorIdleEndpointsAsync(_idleMonitorCts.Token);
    }

    private async Task StopIdleMonitorAsync()
    {
        if (_idleMonitorCts is null)
        {
            return;
        }

        _idleMonitorCts.Cancel();

        if (_idleMonitorTask is not null)
        {
            try
            {
                await _idleMonitorTask;
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
        }

        _idleMonitorCts.Dispose();
        _idleMonitorCts = null;
        _idleMonitorTask = null;
    }

    private async Task MonitorIdleEndpointsAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));

        await RefreshIdleStatusesAsync(cancellationToken);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await RefreshIdleStatusesAsync(cancellationToken);
        }
    }

    private async Task RefreshIdleStatusesAsync(CancellationToken cancellationToken)
    {
        var disabledEndpoints = State.OvrSettings.Endpoints
            .Where(endpoint => !endpoint.IsEnabled)
            .ToArray();

        if (disabledEndpoints.Length == 0)
        {
            return;
        }

        // OVR/PM이 활성화된 엔드포인트는 상태가 Idle이든 Disable이든 상관없이 OVR/PM에서 관리되므로, 여기서는 비활성화된 엔드포인트만 주기적으로 상태를 확인하여 Idle/Disable을 업데이트함
        var snapshots = await Task.WhenAll(disabledEndpoints.Select(endpoint => ProbeIdleStatusAsync(endpoint, cancellationToken))); 
        await ApplyIdleSnapshotsAsync(snapshots);
    }

    private async Task<EndpointIdleSnapshot> ProbeIdleStatusAsync(OvrEndpointSettingsModel endpoint, CancellationToken cancellationToken)
    {
        try
        {
            await endpoint.IoLock.WaitAsync(cancellationToken);
            try
            {
                if (endpoint.IsEnabled)
                {
                    return new EndpointIdleSnapshot(endpoint, null, null);
                }

                if (endpoint.Socket.IsConnected)
                {
                    await endpoint.Socket.DisconnectAsync();
                }
            }
            finally
            {
                endpoint.IoLock.Release();
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(endpoint.IpAddress))
            {
                return new EndpointIdleSnapshot(endpoint, EndpointStatus.Disable, "Ping skipped: empty IP");
            }

            using var ping = new Ping();
            var reply = await ping.SendPingAsync(endpoint.IpAddress, 1000);
            var status = reply.Status == IPStatus.Success ? EndpointStatus.Idle : EndpointStatus.Disable;
            var info = reply.Status == IPStatus.Success
                ? $"Ping OK: {endpoint.IpAddress}"
                : $"Ping failed: {reply.Status}";
            return new EndpointIdleSnapshot(endpoint, status, info);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new EndpointIdleSnapshot(endpoint, EndpointStatus.Disable, $"Ping failed: {ex.Message}");
        }
    }

    private async Task ApplyIdleSnapshotsAsync(IReadOnlyList<EndpointIdleSnapshot> snapshots)
    {
        if (_isShuttingDown)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ApplyIdleSnapshots(snapshots);
            return;
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        await dispatcher.InvokeAsync(() => ApplyIdleSnapshots(snapshots));
    }

    private void ApplyIdleSnapshots(IReadOnlyList<EndpointIdleSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Status is null || snapshot.Endpoint.IsEnabled)
            {
                continue;
            }

            snapshot.Endpoint.ApplyReadResult(
                isConnected: false,
                currentValue: null,
                statusText: snapshot.Status.Value);
            snapshot.Endpoint.Info = snapshot.Info ?? string.Empty;
        }
    }

    private async Task RestartOvrPollingAsync()
    {
        await StopOvrPollingAsync(disconnectSockets: false);

        if (!State.OvrSettings.Endpoints.Any(endpoint => endpoint.IsEnabled))
        {
            await RefreshIdleStatusesAsync(CancellationToken.None);
            AddLog("OVR polling stopped.");
            return;
        }

        _ovrPollingCts = new CancellationTokenSource();
        _ovrPollingTask = PollOvrLoopAsync(_ovrPollingCts.Token);
        AddLog("OVR polling started.");
    }

    private async Task StopOvrPollingAsync(bool disconnectSockets = true)
    {
        if (_ovrPollingCts is not null)
        {
            _ovrPollingCts.Cancel();

            if (_ovrPollingTask is not null)
            {
                try
                {
                    await _ovrPollingTask;
                }
                catch (OperationCanceledException)
                {
                    // normal shutdown
                }
            }

            _ovrPollingCts.Dispose();
            _ovrPollingCts = null;
            _ovrPollingTask = null;
        }

        if (!disconnectSockets)
        {
            return;
        }

        foreach (var endpoint in State.OvrSettings.Endpoints)
        {
            try
            {
                await endpoint.Socket.DisconnectAsync();
            }
            catch
            {
                // ignore endpoint shutdown errors
            }
        }
    }

    private async Task PollOvrLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        await RefreshOvrCurrentsAsync(cancellationToken);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await RefreshOvrCurrentsAsync(cancellationToken);
        }
    }
    #region OCR / PM Communication
    private async Task ApplyOvrSettingsAsync()
    {
        State.OvrSettings.IsVisible = false;
        await StopOvrPollingAsync(disconnectSockets: false);

        var snapshots = await Task.WhenAll(State.OvrSettings.Endpoints.Select(endpoint => ConfigureEndpointAsync(endpoint, CancellationToken.None)));
        await ApplyOvrSnapshotsAsync(snapshots);
        await RefreshIdleStatusesAsync(CancellationToken.None);

        var enabledNames = State.OvrSettings.Endpoints
            .Where(endpoint => endpoint.IsEnabled)
            .Select(endpoint => endpoint.Name)
            .ToArray();

        if (enabledNames.Length == 0)
        {
            SyncOvrCurrentsToDiagram(new Dictionary<string, double?>());
            AddLog("OVR/PM endpoint apply: nothing selected.");
            return;
        }

        AddLog($"OVR/PM endpoint apply: {string.Join(", ", enabledNames)}");
        await RestartOvrPollingAsync();
    }

    private async Task RefreshOvrCurrentsAsync(CancellationToken cancellationToken)
    {
        var snapshots = await Task.WhenAll(State.OvrSettings.Endpoints.Select(endpoint => ReadEndpointRegistersAsync(endpoint, cancellationToken)));
        await ApplyOvrSnapshotsAsync(snapshots);
    }

    private void ResetOvrEndpointStates()
    {
        foreach (var endpoint in State.OvrSettings.Endpoints)
        {
            endpoint.ApplyReadResult(
                isConnected: false,
                currentValue: null,
                statusText: endpoint.IsEnabled ? EndpointStatus.Idle : EndpointStatus.Disable);
        }
    }
    private async Task ApplyOvrSnapshotsAsync(IReadOnlyList<EndpointReadSnapshot> snapshots)
    {
        if (_isShuttingDown)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ApplyOvrSnapshots(snapshots);
            return;
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        await dispatcher.InvokeAsync(() => ApplyOvrSnapshots(snapshots));
    }
    private void ApplyOvrSnapshots(IReadOnlyList<EndpointReadSnapshot> snapshots)
    {
        var currentValues = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);

        foreach (var snapshot in snapshots)
        {
            snapshot.Endpoint.ApplyReadResult(snapshot.IsConnected, snapshot.CurrentValue, snapshot.StatusText, snapshot.Registers);
            snapshot.Endpoint.Info = snapshot.Info;
            currentValues[snapshot.Endpoint.DeviceKey] = snapshot.CurrentValue;
        }

        SyncOvrCurrentsToDiagram(currentValues);
    }

    #endregion
    public async Task WriteOvrEndpointRegisterAsync(string endpointName, ushort registerAddress, ushort value, CancellationToken cancellationToken = default) // Write Single Register
    {
        var endpoint = FindEndpointOrThrow(endpointName);
        var snapshot = await WriteEndpointRegistersAsync(endpoint, registerAddress, [value], writeSingleRegister: true, cancellationToken);
        await ApplyOvrSnapshotsAsync([snapshot]);
        AddLog(snapshot.IsConnected
            ? $"{endpoint.Name} register write: {registerAddress} = {value}"
            : $"{endpoint.Name} register write failed: {snapshot.StatusText}");
    }

    public async Task WriteOvrEndpointRegistersAsync(string endpointName, ushort startAddress, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default) // Write Multiple Registers
    {
        var endpoint = FindEndpointOrThrow(endpointName);
        if (values.Count == 0)
        {
            throw new ArgumentException("At least one register value is required.", nameof(values));
        }

        var snapshot = await WriteEndpointRegistersAsync(endpoint, startAddress, values, writeSingleRegister: false, cancellationToken);
        await ApplyOvrSnapshotsAsync([snapshot]);
        AddLog(snapshot.IsConnected
            ? $"{endpoint.Name} register block write: {startAddress}~{startAddress + values.Count - 1} ({values.Count} words)"
            : $"{endpoint.Name} register block write failed: {snapshot.StatusText}");
    }
    private async Task<EndpointReadSnapshot> WriteEndpointRegistersAsync(
        OvrEndpointSettingsModel endpoint,
        ushort startAddress,
        IReadOnlyList<ushort> values,
        bool writeSingleRegister,
        CancellationToken cancellationToken)
    {
        await endpoint.IoLock.WaitAsync(cancellationToken);
        try
        {
            if (!endpoint.IsEnabled)
            {
                return new EndpointReadSnapshot(endpoint, false, null, EndpointStatus.Disable, "Endpoint disabled", []);
            }

            if (!endpoint.Socket.IsConnected)
            {
                await endpoint.Socket.ConnectAsync(endpoint.IpAddress, endpoint.Port, cancellationToken);
            }

            var slaveId = ResolveSlaveId(endpoint); // Slave ID는 Endpoint 설정의 SlaveId 또는 UnitId에서 가져오며, 1~247 범위로 클램프됨

            if (writeSingleRegister)
            {
                await endpoint.Socket.WriteSingleRegisterAsync(slaveId, startAddress, values[0], cancellationToken);
            }
            else
            {
                await endpoint.Socket.WriteMultipleRegistersAsync(slaveId, startAddress, values.ToArray(), cancellationToken);
            }

            return await ReadEndpointSnapshotCoreAsync(endpoint, cancellationToken, $"Write OK / {startAddress}~{startAddress + values.Count - 1}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            try
            {
                await endpoint.Socket.DisconnectAsync();
            }
            catch
            {
                // ignore endpoint shutdown errors
            }

            return new EndpointReadSnapshot(endpoint, false, null, EndpointStatus.Disable, $"Write failed: {ex.Message}", []);
        }
        finally
        {
            endpoint.IoLock.Release();
        }
    }
    private static byte ResolveSlaveId(OvrEndpointSettingsModel endpoint)
    {
        var requestId = endpoint.SlaveId > 0 ? endpoint.SlaveId : endpoint.UnitId;
        return (byte)Math.Clamp(requestId, 1, 247); // Modbus 프로토콜에서 유효한 Slave ID 범위는 1~247
    }

    private OvrEndpointSettingsModel FindEndpointOrThrow(string endpointName)
    {
        return State.OvrSettings.Endpoints.FirstOrDefault(
                   endpoint => string.Equals(endpoint.Name, endpointName, StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(endpoint.DeviceKey, endpointName, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException($"OVR/PM endpoint '{endpointName}' was not found.");
    }

    private async Task<EndpointReadSnapshot> ConfigureEndpointAsync(OvrEndpointSettingsModel endpoint, CancellationToken cancellationToken)
    {
        await endpoint.IoLock.WaitAsync(cancellationToken);
        try
        {
            if (!endpoint.IsEnabled)
            {
                await endpoint.Socket.DisconnectAsync();
                return new EndpointReadSnapshot(endpoint, false, null, EndpointStatus.Disable, "Endpoint disabled", []);
            }

            if (!endpoint.Socket.IsConnected)
            {
                await endpoint.Socket.ConnectAsync(endpoint.IpAddress, endpoint.Port, cancellationToken);
            }

            return new EndpointReadSnapshot(endpoint, true, endpoint.CurrentValue, EndpointStatus.Enable, "Connected", endpoint.RegisterSnapshot.ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            try
            {
                await endpoint.Socket.DisconnectAsync();
            }
            catch
            {
                // ignore endpoint shutdown errors
            }

            return new EndpointReadSnapshot(endpoint, false, null, EndpointStatus.Disable, ex.Message, []);
        }
        finally
        {
            endpoint.IoLock.Release();
        }
    }

    private async Task<EndpointReadSnapshot> ReadEndpointRegistersAsync(OvrEndpointSettingsModel endpoint, CancellationToken cancellationToken)
    {
        await endpoint.IoLock.WaitAsync(cancellationToken);
        try
        {
            return await ReadEndpointSnapshotCoreAsync(endpoint, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            try
            {
                await endpoint.Socket.DisconnectAsync();
            }
            catch
            {
                // ignore endpoint shutdown errors
            }

            return new EndpointReadSnapshot(endpoint, false, null, EndpointStatus.Disable, ex.Message, []);
        }
        finally
        {
            endpoint.IoLock.Release();
        }
    }

    

    private async Task<EndpointReadSnapshot> ReadEndpointSnapshotCoreAsync(
        OvrEndpointSettingsModel endpoint,
        CancellationToken cancellationToken,
        string? statusPrefix = null)
    {
        if (!endpoint.IsEnabled)
        {
            return new EndpointReadSnapshot(endpoint, false, null, EndpointStatus.Disable, "", []);
        }

        if (!endpoint.Socket.IsConnected)
        {
            await endpoint.Socket.ConnectAsync(endpoint.IpAddress, endpoint.Port, cancellationToken);
        }

        var slaveId = ResolveSlaveId(endpoint);
        var registers = await endpoint.Socket.ReadHoldingRegistersAsync(slaveId, EndpointRegisterStartAddress, EndpointRegisterCount, cancellationToken);

        double? currentValue = null;
        var currentRegisterAddress = endpoint.CurrentRegisterAddress;

        if (currentRegisterAddress >= EndpointRegisterStartAddress &&
            currentRegisterAddress + 1 < EndpointRegisterStartAddress + registers.Length)
        {
            var registerIndex = currentRegisterAddress - EndpointRegisterStartAddress;
            uint rawValue = ((uint)registers[registerIndex] << 16) | registers[registerIndex + 1];

            currentValue = rawValue * endpoint.CurrentScale;
        }

        EndpointStatus status = EndpointStatus.Enable;
        string info = $"패킷: {currentRegisterAddress} ~ {currentRegisterAddress + registers.Length}";

        return new EndpointReadSnapshot(endpoint, true, currentValue, status, info, registers);
    }

    

    

    private sealed record EndpointReadSnapshot(
        OvrEndpointSettingsModel Endpoint,
        bool IsConnected,
        double? CurrentValue,
        EndpointStatus StatusText,
        string Info,
        IReadOnlyList<ushort> Registers);

    private sealed record EndpointIdleSnapshot(
        OvrEndpointSettingsModel Endpoint,
        EndpointStatus? Status,
        string? Info);

    private void RaiseBusCommandCanExecuteChanged()
    {
        Bus1ApplyCommand.RaiseCanExecuteChanged();
        Bus2ApplyCommand.RaiseCanExecuteChanged();
        Bus3ApplyCommand.RaiseCanExecuteChanged();
        Bus1OffCommand.RaiseCanExecuteChanged();
        Bus2OffCommand.RaiseCanExecuteChanged();
        Bus3OffCommand.RaiseCanExecuteChanged();
    }

    private void StartFeedbackPolling()
    {
        if (_feedbackPollingTask is { IsCompleted: false })
        {
            return;
        }

        _feedbackPollingCts = new CancellationTokenSource();
        _feedbackPollingTask = PollFeedbackLoopAsync(_feedbackPollingCts.Token);
    }

    private async Task StopFeedbackPollingAsync()
    {
        if (_feedbackPollingCts is null)
        {
            return;
        }

        _feedbackPollingCts.Cancel();

        if (_feedbackPollingTask is not null)
        {
            try
            {
                await _feedbackPollingTask;
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
        }

        _feedbackPollingCts.Dispose();
        _feedbackPollingCts = null;
        _feedbackPollingTask = null;
    }

    private async Task PollFeedbackLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        await RefreshFeedbackAsync(cancellationToken);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await RefreshFeedbackAsync(cancellationToken);
        }
    }

    private async Task RefreshFeedbackAsync(CancellationToken cancellationToken)
    {
        try
        {
            var values = await ReadDiscreteInputsAsync(
                (byte)State.Connection.UnitId,
                FeedbackStartAddress,
                FeedbackCount,
                cancellationToken);

            await UpdateFeedbackStatesAsync(values);

            if (_feedbackReadFailed)
            {
                AddLog("Discrete input feedback recovered.");
                _feedbackReadFailed = false;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (!_feedbackReadFailed)
            {
                AddLog($"Feedback read failed: {ex.Message}");
            }

            _feedbackReadFailed = true;
            await DisconnectCoreAsync("Disconnected automatically after feedback read failure.", stopPolling: false);
        }
    }

    private async Task UpdateFeedbackStatesAsync(IReadOnlyList<bool> values)
    {
        if (_isShuttingDown)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            ApplyFeedbackStates(values);
            return;
        }

        if (dispatcher.CheckAccess())
        {
            ApplyFeedbackStates(values);
            return;
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        await dispatcher.InvokeAsync(() => ApplyFeedbackStates(values));
    }

    private void ApplyFeedbackStates(IReadOnlyList<bool> values)
    {
        foreach (var kItem in KItems)
        {
            var index = kItem.Definition.FeedbackAddress - FeedbackStartAddress;
            kItem.IsOn = index >= 0 && index < values.Count && values[index];
        }

        SyncBusDiagramFeedback();
    }

    private async Task DisconnectCoreAsync(string logMessage, bool stopPolling)
    {
        if (stopPolling)
        {
            await StopFeedbackPollingAsync();
        }
        else
        {
            _feedbackPollingCts?.Cancel();
        }

        await _modbusGatewayService.DisconnectAsync();
        State.Connection.IsConnected = false;
        ResetBusAppliedFlags();
        await UpdateFeedbackStatesAsync([]);
        _feedbackReadFailed = false;
        AddLog(logMessage);
    }

    #region line simulator communication
    // Write coil
    private async Task WriteAlarmCoilAsync() // 알람 코일은 시스템이 비정상 상태에 빠졌을 때 PLC에서 감지하여 자체적으로 복구 시도를 하거나 관리자에게 경고하기 위한 용도
    {
        if (!State.Connection.IsConnected)
        {
            AddLog($"Alarm coil {AlarmCoilAddress} write skipped: disconnected");
            return;
        }

        try
        {
            //await WriteSingleCoilAsync((byte)State.Connection.UnitId, AlarmCoilAddress, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            AddLog($"Alarm coil write failed: {ex.Message}");
            await DisconnectCoreAsync("Disconnected automatically after alarm coil write failure.", stopPolling: true);
        }
    }
    private async Task WriteSingleCoilAsync(byte unitId, ushort coilAddress, bool value, CancellationToken cancellationToken)
    {
        await _modbusIoLock.WaitAsync(cancellationToken);
        try
        {
            await _modbusGatewayService.WriteSingleCoilAsync(unitId, coilAddress, value, cancellationToken);
        }
        finally
        {
            _modbusIoLock.Release();
        }
    }
    // Read discrete input
    private async Task<bool> ReadKFeedbackStateAsync(KContactViewModel kItem, CancellationToken cancellationToken)
    {
        var values = await ReadDiscreteInputsAsync(
            (byte)State.Connection.UnitId,
            kItem.Definition.FeedbackAddress,
            1,
            cancellationToken);

        var isOn = values.Length > 0 && values[0];
        await UpdateSingleFeedbackStateAsync(kItem, isOn);
        return isOn;
    }
    private async Task<bool[]> ReadDiscreteInputsAsync(byte unitId, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken)
    {
        await _modbusIoLock.WaitAsync(cancellationToken);
        try
        {
            return await _modbusGatewayService.ReadDiscreteInputsAsync(unitId, startAddress, numberOfPoints, cancellationToken);
        }
        finally
        {
            _modbusIoLock.Release();
        }
    }
    // Read registers
    private Task<ushort[]> ReadLineSimulatorRegistersAsync(CancellationToken cancellationToken)
    {
        return ReadHoldingRegistersAsync(
            (byte)State.Connection.UnitId,
            LineRegisterStartAddress,
            LineRegisterCount,
            cancellationToken);
    }
    private async Task<ushort[]> ReadHoldingRegistersAsync(byte unitId, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken)
    {
        await _modbusIoLock.WaitAsync(cancellationToken);
        try
        {
            return await _modbusGatewayService.ReadHoldingRegistersAsync(unitId, startAddress, numberOfPoints, cancellationToken);
        }
        finally
        {
            _modbusIoLock.Release();
        }
    }
    
    #endregion


    

    private async Task UpdateSingleFeedbackStateAsync(KContactViewModel kItem, bool isOn)
    {
        if (_isShuttingDown)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            kItem.IsOn = isOn;
            SyncBusDiagramFeedback();
            return;
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        await dispatcher.InvokeAsync(() =>
        {
            kItem.IsOn = isOn;
            SyncBusDiagramFeedback();
        });
    }

    

    private void SyncBusDiagramFeedback()
    {
        var feedbackStates = KItems.ToDictionary(item => item.Code, item => item.IsOn);
        BusDiagram.SynchronizeFeedback(feedbackStates);
    }

    private void SyncOvrCurrentsToDiagram(IReadOnlyDictionary<string, double?> currentValues)
    {
        BusDiagram.UpdateMarkerCurrents(currentValues);
    }

    public void Dispose()
    {
        RequestShutdown();
        UnsubscribeStateEvents();

        try
        {
            State.OvrSettings.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
            // shutdown path
        }

        try
        {
            _modbusGatewayService.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
            // shutdown path
        }

        try
        {
            _modbusIoLock.Dispose();
        }
        catch
        {
            // shutdown path
        }
    }
}
