namespace ExtensionApi.Options;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.5-flash-lite";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int MaxImages { get; set; } = 20;
    public int MaxImageBytes { get; set; } = 8_000_000;
    public int MaxTotalImageBytes { get; set; } = 30_000_000;
}
