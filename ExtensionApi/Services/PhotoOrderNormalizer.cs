using ExtensionApi.Models;

namespace ExtensionApi.Services;

public static class PhotoOrderNormalizer
{
    private static readonly HashSet<string> AvoidCover = new(StringComparer.OrdinalIgnoreCase)
    {
        "bathroom", "toilet", "hallway", "utility", "detail", "other"
    };

    public static PhotoOrderResponse Normalize(GeminiPhotoOrder result, int imageCount)
    {
        var seen = new HashSet<int>();
        var order = new List<int>(imageCount);
        foreach (var index in result.OrderedIndexes ?? [])
            if (index >= 0 && index < imageCount && seen.Add(index)) order.Add(index);
        for (var index = 0; index < imageCount; index++) if (seen.Add(index)) order.Add(index);

        var assessments = (result.Images ?? [])
            .Where(item => item.Index >= 0 && item.Index < imageCount)
            .GroupBy(item => item.Index)
            .ToDictionary(group => group.Key, group => group.First());
        var cover = order.FirstOrDefault(index => assessments.TryGetValue(index, out var item) && IsSafeCover(item), -1);
        if (cover >= 0 && order[0] != cover)
        {
            order.Remove(cover);
            order.Insert(0, cover);
        }

        var category = assessments.TryGetValue(order[0], out var selected)
            ? selected.Category ?? "unknown"
            : result.CoverCategory ?? "unknown";
        return new PhotoOrderResponse(order, category);
    }

    private static bool IsSafeCover(GeminiImageAssessment image) =>
        !image.IsBlurry && !image.IsDuplicate && !AvoidCover.Contains(image.Category ?? "other");
}
