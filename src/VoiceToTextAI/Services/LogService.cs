namespace VoiceToTextAI.Services;

/// <summary>Simple in-memory log shown in the UI. Never pass the API key to it.</summary>
public static class LogService
{
    public static event Action<string>? LineAdded;

    public static void Info(string message) => Write("INFO ", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
        => LineAdded?.Invoke($"[{DateTime.Now:HH:mm:ss}] {level} {message}");

    public static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max] + $"… (+{text.Length - max} chars)";
}
