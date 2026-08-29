namespace ExtensionApi.Models;

public sealed record PhotoOrderResponse(
    IReadOnlyList<int> OrderedIndexes,
    string CoverCategory,
    IReadOnlyList<PhotoImageResult> Images);

public sealed record PhotoImageResult(
    int Index,
    string Category,
    int QualityScore,
    bool IsDuplicate,
    bool IsBlurry);

public sealed record GeminiPhotoOrder(
    IReadOnlyList<int>? OrderedIndexes,
    string? CoverCategory,
    IReadOnlyList<GeminiImageAssessment>? Images);

public sealed record GeminiImageAssessment(
    int Index,
    string? Category,
    int QualityScore,
    bool IsDuplicate,
    bool IsBlurry);
