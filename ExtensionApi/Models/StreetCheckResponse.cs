namespace ExtensionApi.Models;

public sealed record StreetCheckResponse(
    bool Matched,
    int? SelectedIndex,
    string? SelectedLabel,
    int Confidence,
    string Reason);

public sealed record GeminiStreetChoice(int SelectedIndex, int Confidence, string? Reason);
