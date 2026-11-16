using System.Text;
using Microsoft.Win32;

namespace VoiceToTextAI.Services;

public static class ExportService
{
    /// <summary>Asks for a path and saves UTF-8 text. Returns the saved path, or null if cancelled.</summary>
    public static string? SaveText(string text, string suggestedName)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save transcript",
            Filter = "Text file (*.txt)|*.txt",
            DefaultExt = ".txt",
            AddExtension = true,
            FileName = suggestedName + ".txt"
        };
        if (dlg.ShowDialog() != true) return null;

        // UTF-8 with BOM so Persian text opens correctly in every Windows app (incl. old Notepad).
        File.WriteAllText(dlg.FileName, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return dlg.FileName;
    }
}
