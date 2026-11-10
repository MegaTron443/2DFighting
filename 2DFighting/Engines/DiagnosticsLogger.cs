using System.Diagnostics;
using System.Runtime.CompilerServices;

public enum LogSeverity
{
    Info,
    Warning,
    Error
}

public static class DiagnosticsLogger
{
    private static readonly HashSet<string> _loggedOnce = new(StringComparer.OrdinalIgnoreCase);

    [Conditional("DEBUG")]
    public static void LogSilentIssueOnce(
        string key,
        string message,
        LogSeverity severity = LogSeverity.Warning,
        bool includeStackTrace = false,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        if (!_loggedOnce.Add(key)) return;

        string fileName = Path.GetFileName(filePath);
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string tag = severity switch
        {
            LogSeverity.Error   => "ERROR",
            LogSeverity.Warning => "WARN ",
            _                   => "INFO "
        };

        string formatted = $"[{timestamp}] [{tag}] [{fileName}:{lineNumber} -> {memberName}()] {message}";

        if (includeStackTrace)
        {
            Debug.WriteLine($"{formatted}\n{Environment.StackTrace}");
        }
        else
        {
            Debug.WriteLine(formatted);
        }

        ConsoleColor prevColor = Console.ForegroundColor;
        Console.ForegroundColor = severity switch
        {
            LogSeverity.Error   => ConsoleColor.Red,
            LogSeverity.Warning => ConsoleColor.Yellow,
            _                   => ConsoleColor.Cyan
        };

        Console.WriteLine(formatted);
        Console.ResetColor();
    }

    public static void ClearHistory() => _loggedOnce.Clear();
}