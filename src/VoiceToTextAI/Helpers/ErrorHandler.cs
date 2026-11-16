using System.Net;
using System.Text.Json;

namespace VoiceToTextAI.Helpers;

public static class ErrorHandler
{
    /// <summary>Turns any exception into a short, understandable message. Never includes the API key.</summary>
    public static string Describe(Exception ex)
    {
        switch (ex)
        {
            case GeminiException g:
                return g.Message;
            case OperationCanceledException:
                return "The request timed out. Check your connection or proxy and try again.";
            case HttpRequestException h:
                return DescribeHttp(h);
            case FileNotFoundException:
                return "The selected audio file could not be found.";
            case UnauthorizedAccessException:
                return "Access to the file or folder was denied.";
            case IOException io:
                return "File error: " + io.Message;
            case JsonException:
                return "Google returned an unexpected response. Please try again.";
            default:
                return "Unexpected error: " + ex.Message;
        }
    }

    private static string DescribeHttp(HttpRequestException h)
    {
        if (h.StatusCode == HttpStatusCode.ProxyAuthenticationRequired)
            return "Your proxy requires authentication (407). Check your Windows proxy settings or credentials.";

        return h.HttpRequestError switch
        {
            HttpRequestError.ProxyTunnelError =>
                "Could not connect through the proxy. Check your Windows proxy settings.",
            HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError =>
                "Could not reach Google. Check your internet connection (and proxy, if you use one).",
            HttpRequestError.SecureConnectionError =>
                "A secure (TLS) connection could not be established. Check your proxy, antivirus or system date/time.",
            _ => "Network error. Check your internet connection and proxy settings."
        };
    }

    /// <summary>Maps a failed Gemini HTTP response to a friendly exception.</summary>
    public static GeminiException FromApiResponse(HttpStatusCode status, string body)
    {
        string? apiMessage = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("message", out var m))
                apiMessage = m.GetString();
        }
        catch { /* body was not JSON */ }

        var lower = (apiMessage ?? body ?? "").ToLowerInvariant();
        var detail = apiMessage is { Length: > 0 }
            ? " (" + (apiMessage.Length > 300 ? apiMessage[..300] + "…" : apiMessage) + ")"
            : "";

        var msg = (int)status switch
        {
            400 when lower.Contains("api key") =>
                "The API key is invalid. Check it and try again.",
            400 =>
                "Google rejected the request (unsupported or corrupted audio?)." + detail,
            401 or 403 =>
                "The API key was rejected or doesn't have access to this model. Check the key and your Google AI Studio account/region.",
            404 =>
                "The transcription model is not available for this key.",
            407 =>
                "Your proxy requires authentication (407).",
            413 =>
                "The audio file is too large.",
            429 when lower.Contains("quota") || lower.Contains("billing") =>
                "Your quota or credit is exhausted. Check your Google AI Studio plan and billing." + detail,
            429 =>
                "Too many requests. Wait a little and try again.",
            >= 500 =>
                "Google's service had a temporary error. Please try again in a moment.",
            _ =>
                $"Unexpected response from Google ({(int)status})." + detail
        };

        return new GeminiException(msg);
    }
}
