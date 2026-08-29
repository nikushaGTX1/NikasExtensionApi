using ExtensionApi.Models;

namespace ExtensionApi.Services;

public static class StreetCheckNormalizer
{
    public const int MinimumConfidence = 80;

    public static StreetCheckResponse Normalize(GeminiStreetChoice choice, IReadOnlyList<StreetCandidate> candidates)
    {
        var confidence = Math.Clamp(choice.Confidence, 0, 100);
        var candidate = candidates.FirstOrDefault(item => item.Index == choice.SelectedIndex);
        if (choice.SelectedIndex < 0 || candidate is null || confidence < MinimumConfidence)
            return new StreetCheckResponse(false, null, null, confidence,
                string.IsNullOrWhiteSpace(choice.Reason) ? "No safe catalog match." : choice.Reason.Trim());

        return new StreetCheckResponse(true, candidate.Index, candidate.Label, confidence,
            string.IsNullOrWhiteSpace(choice.Reason) ? "AI selected a supplied catalog candidate." : choice.Reason.Trim());
    }
}
