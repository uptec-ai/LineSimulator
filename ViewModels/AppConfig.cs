using System.Configuration;

namespace TestMcAlgorithm.ViewModels;

/// <summary>
/// App.config(appSettings) 읽기 헬퍼. 키가 없거나 비어 있으면 fallback 값을 사용한다.
/// 값을 실수로 지워도 앱은 기존 기본값으로 계속 동작한다.
/// </summary>
internal static class AppConfig
{
    public static string GetString(string key, string fallback)
    {
        var value = ConfigurationManager.AppSettings[key];
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    public static int GetInt(string key, int fallback)
    {
        return int.TryParse(ConfigurationManager.AppSettings[key], out var value) ? value : fallback;
    }
}
