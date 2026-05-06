using System.Text;

namespace VKFoodArea.Domain.Tests;

internal static class ScenarioLog
{
    private static readonly object Gate = new();
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static bool _initialized;

    public static void WriteLine(string line)
    {
        lock (Gate)
        {
            var path = GetLogPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            EnsureInitialized(path);
            File.AppendAllText(
                path,
                $"{line}{Environment.NewLine}",
                Utf8NoBom);
        }
    }

    private static void EnsureInitialized(string path)
    {
        if (_initialized)
            return;

        var header = string.Join(
            Environment.NewLine,
            "============================================================",
            "VKFoodArea POI Automation Demo Log",
            $"Run started UTC : {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}Z",
            "Log encoding    : UTF-8",
            "============================================================",
            string.Empty);

        File.WriteAllText(path, header, Utf8WithBom);
        _initialized = true;
    }

    private static string GetLogPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VKFoodArea.slnx")))
            {
                return Path.Combine(
                    directory.FullName,
                    "artifacts",
                    "test-logs",
                    "poi-automation-scenarios.log");
            }

            directory = directory.Parent;
        }

        return Path.Combine(
            AppContext.BaseDirectory,
            "artifacts",
            "test-logs",
            "poi-automation-scenarios.log");
    }
}
