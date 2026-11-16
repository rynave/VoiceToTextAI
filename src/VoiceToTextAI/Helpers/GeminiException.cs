namespace VoiceToTextAI.Helpers;

/// <summary>An error with a message that is already safe and friendly to show to the user.</summary>
public sealed class GeminiException : Exception
{
    public GeminiException(string userMessage) : base(userMessage) { }
}
