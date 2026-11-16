using System.Text;
using System.Windows;
using VoiceToTextAI.Helpers;
using VoiceToTextAI.Models;
using VoiceToTextAI.Services;

namespace VoiceToTextAI.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly GeminiService _gemini = new();
    private CancellationTokenSource? _cts;

    private string _apiKey = "";                 // memory only – never written anywhere
    private string? _filePath;
    private TranscriptionMode _mode = TranscriptionMode.Smart;
    private bool _isBusy;
    private string _status = "Enter your API key and choose an audio file.";
    private bool _statusIsError;
    private string _resultText = "";
    private FlowDirection _resultFlow = FlowDirection.LeftToRight;
    private string _proxyInfo = "";

    public MainViewModel()
    {
        BrowseCommand = new RelayCommand(Browse, () => !IsBusy);
        StartCommand = new AsyncRelayCommand(StartAsync, CanStart);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        CopyCommand = new RelayCommand(Copy, () => !string.IsNullOrWhiteSpace(ResultText));
        SaveCommand = new RelayCommand(Save, () => !string.IsNullOrWhiteSpace(ResultText));
        ToggleDirectionCommand = new RelayCommand(() =>
            ResultFlow = ResultFlow == FlowDirection.RightToLeft ? FlowDirection.LeftToRight : FlowDirection.RightToLeft);

        ClearLogCommand = new RelayCommand(() => { _log.Clear(); Raise(nameof(LogText)); });
        CopyLogCommand = new RelayCommand(CopyLog, () => _log.Length > 0);

        LogService.LineAdded += OnLogLine;
        LogService.Info("App started.");
        LoadProxyInfo();
    }

    // ---- bindable state ----
    public string ApiKey { get => _apiKey; set => Set(ref _apiKey, value); }

    public string? FilePath
    {
        get => _filePath;
        private set { if (Set(ref _filePath, value)) Raise(nameof(FileDisplay)); }
    }

    public string FileDisplay => FilePath == null
        ? "No file selected — click Browse or drop a file here"
        : $"{Path.GetFileName(FilePath)}  ({FileService.FormatSize(new FileInfo(FilePath).Length)})";

    public bool IsSmart
    {
        get => _mode == TranscriptionMode.Smart;
        set { if (value) { _mode = TranscriptionMode.Smart; Raise(); Raise(nameof(IsVerbatim)); } }
    }

    public bool IsVerbatim
    {
        get => _mode == TranscriptionMode.Verbatim;
        set { if (value) { _mode = TranscriptionMode.Verbatim; Raise(); Raise(nameof(IsSmart)); } }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (Set(ref _isBusy, value)) Raise(nameof(IsNotBusy)); }
    }
    public bool IsNotBusy => !IsBusy;

    public string Status { get => _status; private set => Set(ref _status, value); }
    public bool StatusIsError { get => _statusIsError; private set => Set(ref _statusIsError, value); }
    public string ResultText { get => _resultText; set => Set(ref _resultText, value); }
    public FlowDirection ResultFlow { get => _resultFlow; set => Set(ref _resultFlow, value); }
    public string ProxyInfo { get => _proxyInfo; private set => Set(ref _proxyInfo, value); }

    private readonly StringBuilder _log = new();
    public string LogText => _log.ToString();
    public RelayCommand ClearLogCommand { get; }
    public RelayCommand CopyLogCommand { get; }

    public RelayCommand BrowseCommand { get; }
    public AsyncRelayCommand StartCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand CopyCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ToggleDirectionCommand { get; }

    // ---- actions ----
    private void Browse()
    {
        var path = FileService.PickAudioFile();
        if (path != null) SetFile(path);
    }

    public void SetFile(string path)
    {
        if (IsBusy) return;
        var error = FileService.Validate(path);
        if (error != null) { SetStatus(error, true); return; }
        FilePath = path;
        SetStatus("File selected. Press “Start transcription”.", false);
    }

    private bool CanStart() => !IsBusy && ApiKey.Trim().Length > 0 && FilePath != null;

    private async Task StartAsync()
    {
        var key = ApiKey.Trim();
        if (key.Length < 20 || key.Any(char.IsWhiteSpace))
        {
            SetStatus("The API key looks invalid. Paste the key from Google AI Studio without spaces.", true);
            return;
        }

        var path = FilePath!;
        var fileError = FileService.Validate(path);
        if (fileError != null) { SetStatus(fileError, true); return; }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        IsBusy = true;
        SetStatus("Starting…", false);
        LogService.Info("---- New transcription ----");

        try
        {
            var progress = new Progress<string>(s => Status = s);
            var mode = _mode;
            var result = await Task.Run(() => _gemini.TranscribeAsync(key, path, mode, progress, token), token);

            ResultText = result.Text;
            ResultFlow = DetectFlow(result.Text);
            SetStatus($"Done — {result.Text.Length:N0} characters.", false);
            LogService.Info($"Finished OK. {result.Text.Length:N0} characters.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            SetStatus("Canceled.", false);
            LogService.Info("Canceled by user.");
        }
        catch (Exception ex)
        {
            LogService.Error(DescribeForLog(ex));
            SetStatus(ErrorHandler.Describe(ex), true);
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void Copy()
    {
        try
        {
            Clipboard.SetDataObject(ResultText, true);   // retries if the clipboard is busy
            SetStatus("Text copied to clipboard.", false);
        }
        catch (Exception)
        {
            SetStatus("Couldn't access the clipboard. Try again.", true);
        }
    }

    private void Save()
    {
        try
        {
            var suggested = FilePath != null ? Path.GetFileNameWithoutExtension(FilePath) : "transcript";
            var saved = ExportService.SaveText(ResultText, suggested);
            if (saved != null) SetStatus("Saved: " + saved, false);
        }
        catch (Exception ex)
        {
            SetStatus("Couldn't save the file: " + ErrorHandler.Describe(ex), true);
        }
    }

    // ---- logging ----
    private void OnLogLine(string line)
    {
        void Append()
        {
            _log.AppendLine(line);
            if (_log.Length > 300_000) _log.Remove(0, _log.Length - 200_000);
            Raise(nameof(LogText));
        }

        var d = Application.Current?.Dispatcher;
        if (d == null || d.CheckAccess()) Append(); else d.BeginInvoke(Append);
    }

    private void CopyLog()
    {
        try { Clipboard.SetDataObject(LogText, true); SetStatus("Log copied to clipboard.", false); }
        catch (Exception) { SetStatus("Couldn't access the clipboard.", true); }
    }

    private static string DescribeForLog(Exception ex)
    {
        var sb = new StringBuilder();
        for (Exception? e = ex; e != null; e = e.InnerException)
            sb.Append(e == ex ? "" : " <- ").Append(e.GetType().Name).Append(": ").Append(e.Message);
        return sb.ToString();
    }

    // ---- helpers ----
    private void SetStatus(string text, bool isError)
    {
        Status = text;
        StatusIsError = isError;
    }

    private async void LoadProxyInfo()
    {
        try { ProxyInfo = await Task.Run(ProxyService.Describe); LogService.Info(ProxyInfo); }
        catch { /* informational only */ }
    }

    private static FlowDirection DetectFlow(string text)
    {
        int rtl = 0, ltr = 0;
        foreach (var ch in text)
        {
            if ((ch >= '\u0590' && ch <= '\u08FF') || (ch >= '\uFB1D' && ch <= '\uFDFF') || (ch >= '\uFE70' && ch <= '\uFEFF'))
                rtl++;
            else if (char.IsLetter(ch))
                ltr++;
        }
        return rtl > ltr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }
}
