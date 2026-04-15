using System.Collections.ObjectModel;
using System.Windows;
using TestMcAlgorithm.Models;

namespace TestMcAlgorithm.Services;

public sealed class LogStore
{
    private readonly object _syncRoot = new();
    private readonly List<LogEntryModel> _entries = [];
    private const int MaxEntries = 5000;
    private const int MaxRecentEntries = 200;

    public ObservableCollection<LogEntryModel> RecentEntries { get; } = [];

    public void Append(LogDefinition definition, string message)
    {
        var entry = new LogEntryModel(DateTime.Now, definition, message);

        lock (_syncRoot)
        {
            _entries.Insert(0, entry);
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(_entries.Count - 1);
            }
        }

        RunOnUiThread(() =>
        {
            RecentEntries.Insert(0, entry);
            while (RecentEntries.Count > MaxRecentEntries)
            {
                RecentEntries.RemoveAt(RecentEntries.Count - 1);
            }
        });
    }

    public IReadOnlyList<LogEntryModel> Snapshot()
    {
        lock (_syncRoot)
        {
            return _entries.ToList();
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _entries.Clear();
        }

        RunOnUiThread(() => RecentEntries.Clear());
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        dispatcher.Invoke(action);
    }
}
