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
    private sealed class CompositeKeyComparer : IEqualityComparer<(string, string, string)>
    {
        public static readonly CompositeKeyComparer Instance = new();

        public bool Equals((string, string, string) x, (string, string, string) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Item1, y.Item1) &&
            StringComparer.OrdinalIgnoreCase.Equals(x.Item2, y.Item2) &&
            StringComparer.OrdinalIgnoreCase.Equals(x.Item3, y.Item3);

        public int GetHashCode((string, string, string) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item3)
            );
    }

    private static readonly HashSet<string> _loggedOnce = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<(string, string, string)> _loggedCompositeOnce = new(CompositeKeyComparer.Instance);

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
        EmitLog(message, severity, includeStackTrace, memberName, filePath, lineNumber);
    }

    [Conditional("DEBUG")]
    public static void LogSilentIssueOnce(
        string keyPrefix,
        string keyArg,
        Func<string, string> messageFactory,
        LogSeverity severity = LogSeverity.Warning,
        bool includeStackTrace = false,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        if (!_loggedCompositeOnce.Add((keyPrefix, keyArg, ""))) return;
        EmitLog(messageFactory(keyArg), severity, includeStackTrace, memberName, filePath, lineNumber);
    }

    [Conditional("DEBUG")]
    public static void LogSilentIssueOnce(
        string keyPrefix,
        string keyArg1,
        string keyArg2,
        Func<string, string, string> messageFactory,
        LogSeverity severity = LogSeverity.Warning,
        bool includeStackTrace = false,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        if (!_loggedCompositeOnce.Add((keyPrefix, keyArg1, keyArg2))) return;
        EmitLog(messageFactory(keyArg1, keyArg2), severity, includeStackTrace, memberName, filePath, lineNumber);
    }

    private static void EmitLog(
        string message,
        LogSeverity severity,
        bool includeStackTrace,
        string memberName,
        string filePath,
        int lineNumber)
    {
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

        Console.ForegroundColor = severity switch
        {
            LogSeverity.Error   => ConsoleColor.Red,
            LogSeverity.Warning => ConsoleColor.Yellow,
            _                   => ConsoleColor.Cyan
        };

        Console.WriteLine(formatted);
        Console.ResetColor();
    }

    public static void ClearHistory()
    {
        _loggedOnce.Clear();
        _loggedCompositeOnce.Clear();
    }
}