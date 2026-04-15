using System.Collections.ObjectModel;
using System.Windows;
using TestMcAlgorithm.Models;

namespace TestMcAlgorithm.ViewModels;

public sealed class LogWindowViewModel : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource _refreshCts = new();
    private readonly Task _refreshTask;
    private string _selectedCategory = "전체";
    private string _selectedLevel = "all";

    public LogWindowViewModel(LineSimulatorViewModel lineSimulator)
    {
        LineSimulator = lineSimulator;
        CategoryOptions =
        [
            "전체",
            "시스템",
            "운전",
            "보호",
            "통신",
            "설정 변경",
            "알람"
        ];
        LevelOptions =
        [
            "all",
            "info",
            "warn",
            "error",
            "alarm",
            "trip"
        ];

        CloseCommand = new RelayCommand(_ =>
        {
            Dispose();
            CloseRequested?.Invoke();
        });
        ClearCommand = new RelayCommand(_ => LineSimulator.LogStore.Clear());

        _refreshTask = RefreshLoopAsync(_refreshCts.Token);
    }

    public LineSimulatorViewModel LineSimulator { get; }
    public ObservableCollection<LogEntryModel> FilteredLogs { get; } = [];
    public ObservableCollection<string> CategoryOptions { get; }
    public ObservableCollection<string> LevelOptions { get; }
    public RelayCommand CloseCommand { get; }
    public RelayCommand ClearCommand { get; }
    public event Action? CloseRequested;

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                _ = RefreshOnceAsync();
            }
        }
    }

    public string SelectedLevel
    {
        get => _selectedLevel;
        set
        {
            if (SetProperty(ref _selectedLevel, value))
            {
                _ = RefreshOnceAsync();
            }
        }
    }

    private async Task RefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await RefreshOnceAsync();

            try
            {
                await Task.Delay(500, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RefreshOnceAsync()
    {
        var snapshot = LineSimulator.LogStore.Snapshot()
            .Where(IsCategoryMatch)
            .Where(IsLevelMatch)
            .ToList();

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ReplaceLogs(snapshot);
            return;
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        await dispatcher.InvokeAsync(() => ReplaceLogs(snapshot));
    }

    private bool IsCategoryMatch(LogEntryModel entry)
    {
        return SelectedCategory == "전체" || string.Equals(entry.CategoryText, SelectedCategory, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsLevelMatch(LogEntryModel entry)
    {
        return SelectedLevel == "all" || string.Equals(entry.LevelText, SelectedLevel, StringComparison.OrdinalIgnoreCase);
    }

    private void ReplaceLogs(IReadOnlyList<LogEntryModel> snapshot)
    {
        if (FilteredLogs.Count == snapshot.Count)
        {
            var isSame = true;
            for (var index = 0; index < snapshot.Count; index++)
            {
                if (!ReferenceEquals(FilteredLogs[index], snapshot[index]))
                {
                    isSame = false;
                    break;
                }
            }

            if (isSame)
            {
                return;
            }
        }

        FilteredLogs.Clear();
        foreach (var entry in snapshot)
        {
            FilteredLogs.Add(entry);
        }
    }

    public void Dispose()
    {
        if (_refreshCts.IsCancellationRequested)
        {
            return;
        }

        _refreshCts.Cancel();
        try
        {
            //_refreshTask.Wait(1000);
        }
        catch
        {
            // ignore shutdown race
        }
        finally
        {
            _refreshCts.Dispose();
        }
    }
}
