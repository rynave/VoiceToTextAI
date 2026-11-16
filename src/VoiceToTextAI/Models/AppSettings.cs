namespace VoiceToTextAI.Models;

/// <summary>
/// Static, non-sensitive configuration. Nothing here is persisted and the API key is NEVER stored.
/// </summary>
public static class AppSettings
{
    // Model ID for the Gemini Developer API (Google AI Studio keys).
    public const string ModelId = "gemini-3.5-transcribe";

    public const string ApiBase = "https://generativelanguage.googleapis.com/v1beta";
    public const string UploadUrl = "https://generativelanguage.googleapis.com/upload/v1beta/files";

    // Files API limit per file.
    public const long MaxFileBytes = 2L * 1024 * 1024 * 1024;

    public const int HttpTimeoutMinutes = 60;
}
