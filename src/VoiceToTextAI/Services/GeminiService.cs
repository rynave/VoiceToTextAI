using System.Text;
using System.Text.Json;
using VoiceToTextAI.Helpers;
using VoiceToTextAI.Models;

namespace VoiceToTextAI.Services;

/// <summary>
/// Gemini Developer API flow: Files API (resumable upload) -> generateContent with
/// audioTranscriptionConfig -> delete the uploaded file. The API key lives only in memory
/// and is sent only in the x-goog-api-key header.
/// </summary>
public sealed class GeminiService : IDisposable
{
    private readonly HttpClient _http;

    public GeminiService()
    {
        _http = new HttpClient(ProxyService.CreateHandler(), disposeHandler: true)
        {
            Timeout = TimeSpan.FromMinutes(AppSettings.HttpTimeoutMinutes)
        };
    }

    private sealed record RemoteFile(string Name, string Uri, string MimeType, string? State)
    {
        public bool IsActive => State is null or "ACTIVE";
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        string apiKey, string filePath, TranscriptionMode mode,
        IProgress<string>? progress, CancellationToken ct)
    {
        var mime = FileService.GetMimeType(filePath);
        string? uploadedName = null;
        LogService.Info($"Transcription started. File=\"{Path.GetFileName(filePath)}\", size={FileService.FormatSize(new FileInfo(filePath).Length)}, mime={mime}, mode={mode}, model={AppSettings.ModelId}");
        try
        {
            progress?.Report("Uploading audio to Google…");
            var file = await UploadAsync(apiKey, filePath, mime, ct);
            uploadedName = file.Name;
            LogService.Info($"Upload complete. name={file.Name}, state={file.State ?? "(none)"}");

            if (!file.IsActive)
            {
                progress?.Report("Google is processing the file…");
                file = await WaitUntilActiveAsync(apiKey, file, ct);
            }

            progress?.Report("Transcribing…");
            var text = await GenerateAsync(apiKey, file, mode, ct);
            return new TranscriptionResult(text);
        }
        finally
        {
            if (uploadedName != null) await TryDeleteAsync(apiKey, uploadedName);
        }
    }

    // ---------- Files API ----------

    private async Task<RemoteFile> UploadAsync(string apiKey, string path, string mime, CancellationToken ct)
    {
        var length = new FileInfo(path).Length;

        // Step 1: start a resumable upload session.
        using var start = CreateRequest(HttpMethod.Post, AppSettings.UploadUrl, apiKey);
        start.Headers.Add("X-Goog-Upload-Protocol", "resumable");
        start.Headers.Add("X-Goog-Upload-Command", "start");
        start.Headers.Add("X-Goog-Upload-Header-Content-Length", length.ToString());
        start.Headers.Add("X-Goog-Upload-Header-Content-Type", mime);
        // Neutral display name: the real file name is not sent to Google.
        start.Content = new StringContent(
            JsonSerializer.Serialize(new { file = new { display_name = "audio-upload" } }),
            Encoding.UTF8, "application/json");

        string uploadUrl;
        using (var startResp = await SendAsync(start, ct))
        {
            if (!startResp.Headers.TryGetValues("x-goog-upload-url", out var urls) || !urls.Any())
                throw new GeminiException("Google didn't return an upload address. Please try again.");
            uploadUrl = urls.First();
        }

        // Step 2: upload the bytes and finalize.
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        using var up = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        up.Headers.Add("X-Goog-Upload-Offset", "0");
        up.Headers.Add("X-Goog-Upload-Command", "upload, finalize");
        up.Content = new StreamContent(fs);
        up.Content.Headers.ContentLength = length;

        using var upResp = await SendAsync(up, ct);
        var json = await upResp.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);
        var f = doc.RootElement.GetProperty("file");
        return new RemoteFile(
            Name: f.GetProperty("name").GetString()!,
            Uri: f.GetProperty("uri").GetString()!,
            MimeType: mime,
            State: f.TryGetProperty("state", out var s) ? s.GetString() : null);
    }

    private async Task<RemoteFile> WaitUntilActiveAsync(string apiKey, RemoteFile file, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMinutes(10);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);

            using var req = CreateRequest(HttpMethod.Get, $"{AppSettings.ApiBase}/{file.Name}", apiKey);
            using var resp = await SendAsync(req, ct);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var state = doc.RootElement.TryGetProperty("state", out var s) ? s.GetString() : "ACTIVE";
            LogService.Info($"File state: {state}");

            if (state == "ACTIVE") return file with { State = "ACTIVE" };
            if (state == "FAILED") throw new GeminiException("Google failed to process the audio file.");
        }
        throw new GeminiException("Google took too long to process the file. Please try again.");
    }

    private async Task TryDeleteAsync(string apiKey, string name)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var req = CreateRequest(HttpMethod.Delete, $"{AppSettings.ApiBase}/{name}", apiKey);
            using var resp = await _http.SendAsync(req, cts.Token);
            LogService.Info($"Uploaded file deleted from Google ({(int)resp.StatusCode}).");
        }
        catch (Exception ex)
        {
            LogService.Error("Could not delete uploaded file (it expires automatically): " + ex.Message);
        }
    }

    // ---------- Transcription ----------

    private async Task<string> GenerateAsync(string apiKey, RemoteFile file, TranscriptionMode mode, CancellationToken ct)
    {
        var body = new
        {
            contents = new[]
            {
                new { parts = new[] { new { fileData = new { fileUri = file.Uri, mimeType = file.MimeType } } } }
            },
            generationConfig = new
            {
                audioTranscriptionConfig = new
                {
                    mode = mode == TranscriptionMode.Smart ? "SMART" : "VERBATIM"
                    // languageCodes omitted => automatic language detection (works for Persian/English mixes)
                }
            }
        };

        using var req = CreateRequest(HttpMethod.Post,
            $"{AppSettings.ApiBase}/models/{AppSettings.ModelId}:generateContent", apiKey);
        var requestJson = JsonSerializer.Serialize(body);
        req.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        LogService.Info("Request body: " + requestJson);

        using var resp = await SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        LogService.Info("Raw response: " + LogService.Truncate(raw, 8000));
        return ParseText(raw);
    }

    private static string ParseText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var parts = new List<string>();
        string? finishReason = null;

        if (root.TryGetProperty("candidates", out var cands) &&
            cands.ValueKind == JsonValueKind.Array && cands.GetArrayLength() > 0)
        {
            var cand = cands[0];
            if (cand.TryGetProperty("finishReason", out var fr) && fr.ValueKind == JsonValueKind.String)
                finishReason = fr.GetString();

            if (cand.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var arr) &&
                arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in arr.EnumerateArray())
                {
                    if (p.TryGetProperty("thought", out var th) && th.ValueKind == JsonValueKind.True) continue;

                    if (p.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                        parts.Add(t.GetString() ?? "");
                    else if (p.TryGetProperty("audioTranscription", out var at))
                        parts.Add(ExtractTranscription(at));
                }
            }
        }

        var text = string.Join(" ", parts.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (text.Length > 0) return text;

        if (root.TryGetProperty("promptFeedback", out var fb) &&
            fb.TryGetProperty("blockReason", out var reason))
            throw new GeminiException($"Google blocked this request ({reason.GetString()}).");

        var raw = json.Length > 600 ? json[..600] + "…" : json;
        throw new GeminiException(
            $"Google returned no text (finishReason: {finishReason ?? "none"}). Raw response: {raw}");
    }

    private static string ExtractTranscription(JsonElement at)
    {
        foreach (var name in new[] { "text", "transcript", "transcription" })
            if (at.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? "";

        if (at.TryGetProperty("words", out var words) && words.ValueKind == JsonValueKind.Array)
            return string.Join(" ", words.EnumerateArray()
                .Select(w => w.TryGetProperty("word", out var x) ? x.GetString() : null)
                .Where(x => !string.IsNullOrEmpty(x)));

        return "";
    }

    // ---------- helpers ----------

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string apiKey)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("x-goog-api-key", apiKey);
        return req;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        // Path only: query strings / headers (which could contain secrets) are never logged.
        var target = req.RequestUri?.GetLeftPart(UriPartial.Path) ?? "?";
        LogService.Info($"{req.Method} {target}");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        LogService.Info($"  -> {(int)resp.StatusCode} {resp.StatusCode} ({sw.ElapsedMilliseconds} ms)");
        if (resp.IsSuccessStatusCode) return resp;

        var status = resp.StatusCode;
        var body = await resp.Content.ReadAsStringAsync(ct);
        resp.Dispose();
        LogService.Error("Error body: " + LogService.Truncate(body, 2000));
        throw ErrorHandler.FromApiResponse(status, body);
    }

    public void Dispose() => _http.Dispose();
}
