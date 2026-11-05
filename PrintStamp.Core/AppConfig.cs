using System.Text.Json;

namespace PrintStamp.Core;

public sealed class AppConfig
{
    public string? PrinterName { get; init; }
    public string? SumatraPath { get; init; }

    public static AppConfig Load()
    {
        var path = Path.Combine(AppPaths.Root, "config.json");
        if (!File.Exists(path)) return new AppConfig();
        return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AppConfig();
    }
}