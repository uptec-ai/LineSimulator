using TestMcAlgorithm.ViewModels;

namespace TestMcAlgorithm.Models;

public enum LogCategory
{
    System,
    Operation,
    Protection,
    Communication,
    SettingChange,
    Alarm
}

public enum LogLevel
{
    Info,
    Warn,
    Error,
    Alarm,
    Trip
}

public sealed record LogDefinition(
    string Code,
    LogCategory Category,
    LogLevel Level,
    string Movement,
    string Meaning,
    string ExampleMessage);

public sealed class LogEntryModel : ObservableObject
{
    public LogEntryModel(DateTime timestamp, LogDefinition definition, string message)
    {
        Timestamp = timestamp;
        Definition = definition;
        Message = message;
    }

    public DateTime Timestamp { get; }
    public LogDefinition Definition { get; }
    public string Message { get; }

    public string TimestampText => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    public string MainWindowTimestampText => Timestamp.ToString("HH:mm:ss");
    public string Code => Definition.Code;
    public string LevelText => ToLevelText(Definition.Level);
    public string CategoryText => ToCategoryText(Definition.Category);
    public string Movement => Definition.Movement;
    public string Meaning => Definition.Meaning;
    public string MainWindowText => $"[{MainWindowTimestampText}] [{LevelText}] {Message}";

    public static string ToCategoryText(LogCategory category) =>
        category switch
        {
            LogCategory.System => "시스템",
            LogCategory.Operation => "운전",
            LogCategory.Protection => "보호",
            LogCategory.Communication => "통신",
            LogCategory.SettingChange => "설정 변경",
            LogCategory.Alarm => "알람",
            _ => "기타"
        };

    public static string ToLevelText(LogLevel level) =>
        level switch
        {
            LogLevel.Info => "INFO",
            LogLevel.Warn => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Alarm => "ALARM",
            LogLevel.Trip => "TRIP",
            _ => "INFO"
        };
}
