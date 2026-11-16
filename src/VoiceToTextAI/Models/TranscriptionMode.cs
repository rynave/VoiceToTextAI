namespace VoiceToTextAI.Models;

public enum TranscriptionMode
{
    /// <summary>Clean, readable text (removes fillers, formats lists/numbers).</summary>
    Smart,

    /// <summary>Exact word-for-word transcript.</summary>
    Verbatim
}
