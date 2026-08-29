namespace ExtensionApi.Options;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public string ExtensionApiKey { get; set; } = string.Empty;
}
