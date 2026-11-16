using Microsoft.Win32;
using VoiceToTextAI.Models;

namespace VoiceToTextAI.Services;

public static class FileService
{
    private static readonly Dictionary<string, string> MimeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp3"] = "audio/mp3",
        [".wav"] = "audio/wav",
        [".m4a"] = "audio/m4a",
        [".aac"] = "audio/aac",
        [".ogg"] = "audio/ogg",
        [".flac"] = "audio/flac",
        [".aiff"] = "audio/aiff",
        [".aif"] = "audio/aiff",
        [".opus"] = "audio/opus",
        [".webm"] = "audio/webm",
    };

    public static string? PickAudioFile()
    {
        var patterns = string.Join(";", MimeByExtension.Keys.Select(e => "*" + e));
        var dlg = new OpenFileDialog
        {
            Title = "Choose an audio file",
            Filter = $"Audio files|{patterns}|All files (*.*)|*.*",
            CheckFileExists = true
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public static string GetMimeType(string path)
        => MimeByExtension.TryGetValue(Path.GetExtension(path), out var mime)
            ? mime
            : throw new Helpers.GeminiException("This audio format is not supported.");

    /// <summary>Returns an error message, or null if the file looks fine.</summary>
    public static string? Validate(string path)
    {
        if (!File.Exists(path)) return "The selected file doesn't exist.";
        if (!MimeByExtension.ContainsKey(Path.GetExtension(path)))
            return "Unsupported format. Supported: MP3, WAV, M4A, AAC, OGG, FLAC, AIFF, OPUS, WEBM.";
        var length = new FileInfo(path).Length;
        if (length == 0) return "The audio file is empty.";
        if (length > AppSettings.MaxFileBytes) return "The file is larger than 2 GB, which is Google's upload limit.";
        return null;
    }

    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {units[i]}";
    }
}
