# Voice to Text AI

A lightweight, portable Windows desktop app that transcribes audio files to text using Google's **Gemini 3.5 Transcribe** model (`gemini-3.5-transcribe`).

> 🇮🇷 [نسخه فارسی در پایین همین صفحه](#-راهنمای-فارسی)

Built with **C# / .NET 10 / WPF (MVVM)**. It ships as a **single self-contained `.exe`** — no .NET runtime, Python or installer required.

---

## Features

- **Smart mode** – clean, readable text: removes fillers and stutters, resolves self-corrections, formats numbers and lists.
- **Verbatim mode** – exact word-for-word transcript.
- **Automatic language detection** – including Persian (Farsi), English and mixed-language speech.
- **Supported formats** – MP3, WAV, M4A, AAC, OGG, FLAC, AIFF, OPUS, WEBM.
- **Windows proxy support** – uses system proxy settings (including WPAD / PAC) and Windows proxy authentication.
- **Persian / RTL friendly** – output direction is detected automatically, with a manual toggle.
- **Editable output**, one-click **Copy** and **Save as TXT** (UTF-8).
- **Drag & drop**, **Cancel**, friendly error messages, and a built-in **log panel** for troubleshooting.
- **Privacy-minded** – see [Privacy & security](#privacy--security).

## Download

Grab the latest `VoiceToTextAI.exe` from the [Releases](https://github.com/rynave/VoiceToTextAI/releases) page, copy it anywhere (even a USB drive) and double-click. Windows 10/11 64-bit only.

> The exe is not code-signed, so Windows SmartScreen or your antivirus may show a warning on first run. You can always build it yourself from source (below).

## Getting an API key

1. Open [Google AI Studio → API keys](https://aistudio.google.com/apikey).
2. Create a key and paste it into the app.

Transcription is billed by Google according to your AI Studio plan — check the [official pricing page](https://ai.google.dev/gemini-api/docs/pricing). The model is currently in **preview**, so availability may vary by account/region.

## Usage

1. Paste your API key (use **Show** to verify it).
2. Click **Browse…** or drag an audio file onto the window.
3. Choose **Smart** or **Verbatim**.
4. Click **Start transcription**.
5. Edit the result if you like, then **Copy Text** or **Save as TXT**.

## Build from source

Requirements: Windows 10/11 x64 and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
git clone https://github.com/rynave/VoiceToTextAI.git
cd VoiceToTextAI

# run in development
dotnet run --project src/VoiceToTextAI

# build the portable single-file exe
dotnet publish src/VoiceToTextAI -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

The result is `publish\VoiceToTextAI.exe` (roughly 60–100 MB because the runtime is bundled; trimming isn't supported for WPF). On first launch, native WPF libraries are extracted to a temp folder, so the very first start is slightly slower.

## How it works

1. The audio file is uploaded to the Gemini **Files API** (resumable upload). The real file name is not sent.
2. `generateContent` is called on `gemini-3.5-transcribe` with `audioTranscriptionConfig.mode` set to `SMART` or `VERBATIM`.
3. The text is shown in the app.
4. The uploaded file is deleted from Google's servers (best effort; uploads also expire automatically).

Limits (from Google's docs): up to 1 hour of audio per request, 2 GB per uploaded file.

## Project structure

```text
src/VoiceToTextAI/
├── Models/          TranscriptionMode, TranscriptionResult, AppSettings
├── ViewModels/      MainViewModel (MVVM)
├── Services/        GeminiService, ProxyService, FileService, ExportService, LogService
├── Helpers/         ErrorHandler, GeminiException, ObservableObject, Commands
├── Resources/       Styles
└── MainWindow.xaml  UI
```

## Privacy & security

- The API key is held **in memory only**: it is never written to disk, settings or logs, and it is sent only in the `x-goog-api-key` header.
- Your audio is sent to Google (and nowhere else) for processing. Review Google's terms for how your data is handled for your plan.
- The **Logs** panel records requests and Google's raw responses, which may include transcript text. It never contains the API key, but check it before sharing logs publicly.

## Troubleshooting

| Problem | What to try |
|---|---|
| "API key is invalid" | Re-copy the key from AI Studio; make sure there are no spaces. |
| 403 / "model not available" | The model is in preview; check your account and region access. |
| 429 / quota errors | Wait and retry, or check your plan and billing in AI Studio. |
| Proxy / connection errors | The app uses Windows proxy settings; the footer shows the route in use. |
| "Google returned no text" | Open the **Logs** panel, copy the *Raw response* and open an issue. |

## Contributing

Issues and pull requests are welcome. Please do not include API keys or private audio in bug reports.

## Author

Made by [@rynave](https://github.com/rynave).

---

# 🇮🇷 راهنمای فارسی

**Voice to Text AI** یک برنامه‌ی ویندوزی سبک و پرتابل است که فایل‌های صوتی را با مدل **Gemini 3.5 Transcribe** گوگل به متن تبدیل می‌کند. با C# و .NET 10 و WPF نوشته شده و خروجی آن **یک فایل EXE مستقل** است؛ بدون نیاز به نصب .NET یا هر چیز دیگری.

## امکانات
- **حالت Smart:** متن مرتب و خوانا؛ حذف تپق و حرف‌های اضافه، اصلاح خودکار و قالب‌بندی اعداد و فهرست‌ها.
- **حالت Verbatim:** پیاده‌سازی دقیق و کلمه‌به‌کلمه‌ی گفتار.
- تشخیص خودکار زبان (فارسی، انگلیسی و ترکیبی).
- فرمت‌ها: MP3، WAV، M4A، AAC، OGG، FLAC، AIFF، OPUS، WEBM.
- استفاده از **Proxy خود ویندوز** (شامل PAC و WPAD).
- نمایش درست متن فارسی (راست‌به‌چپ)، ویرایش متن، کپی و ذخیره به‌صورت TXT با UTF-8.
- Drag & Drop، دکمه‌ی لغو، پیام‌های خطای قابل‌فهم و بخش **Logs** برای عیب‌یابی.

## دریافت و اجرا
فایل `VoiceToTextAI.exe` را از بخش [Releases](https://github.com/rynave/VoiceToTextAI/releases) دانلود کنید و دوبار کلیک کنید (ویندوز ۱۰ و ۱۱، ۶۴ بیتی). چون فایل امضای دیجیتال ندارد، ممکن است SmartScreen یا آنتی‌ویروس هشدار بدهد.

## دریافت API Key
از [Google AI Studio](https://aistudio.google.com/apikey) یک کلید بسازید و در برنامه وارد کنید. هزینه‌ی تبدیل طبق پلن شما در گوگل محاسبه می‌شود. مدل فعلاً در مرحله‌ی Preview است.

## ساخت از سورس
پیش‌نیاز: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```powershell
dotnet publish src/VoiceToTextAI -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```
فایل نهایی: `publish\VoiceToTextAI.exe`

## حریم خصوصی
- API Key فقط در حافظه می‌ماند و روی دیسک یا در لاگ ذخیره نمی‌شود.
- فایل صوتی فقط برای پردازش به سرویس گوگل ارسال می‌شود و بعد از تبدیل از سرور حذف می‌شود.
- بخش Logs ممکن است شامل متن تبدیل‌شده باشد؛ قبل از اشتراک‌گذاری عمومی آن را بررسی کنید.

## مشارکت
Issue و Pull Request خوش‌آمد است. لطفاً در گزارش‌ها هرگز API Key یا فایل صوتی خصوصی قرار ندهید.
