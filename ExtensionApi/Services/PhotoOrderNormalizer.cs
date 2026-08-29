using ExtensionApi.Models;

namespace ExtensionApi.Services;

public static class PhotoOrderNormalizer
{
    private static readonly IReadOnlyDictionary<string, int> CategoryOrder =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["living_room"] = 0,
        ["bedroom"] = 1,
        ["kitchen"] = 2,
        ["balcony_view"] = 3,
        ["exterior"] = 4,
        ["hallway"] = 4,
        ["utility"] = 4,
        ["detail"] = 4,
        ["other"] = 4,
        ["bathroom"] = 5,
        ["toilet"] = 5
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
        var modelPosition = order.Select((index, position) => (index, position))
            .ToDictionary(item => item.index, item => item.position);
        order = order
            .OrderBy(index => assessments.TryGetValue(index, out var item) ? CategoryRank(item.Category) : 4)
            .ThenBy(index => assessments.TryGetValue(index, out var item) && item.IsDuplicate ? 1 : 0)
            .ThenBy(index => assessments.TryGetValue(index, out var item) && item.IsBlurry ? 1 : 0)
            .ThenByDescending(index => assessments.TryGetValue(index, out var item) ? item.QualityScore : -1)
            .ThenBy(index => modelPosition[index])
            .ThenBy(index => index)
            .ToList();

        var category = assessments.TryGetValue(order[0], out var selected)
            ? selected.Category ?? "unknown"
            : result.CoverCategory ?? "unknown";
        var images = order.Select(index => assessments.TryGetValue(index, out var item)
            ? new PhotoImageResult(index, item.Category ?? "other", Math.Clamp(item.QualityScore, 0, 100), item.IsDuplicate, item.IsBlurry)
            : new PhotoImageResult(index, "other", 0, false, false)).ToArray();
        return new PhotoOrderResponse(order, category, images);
    }

    private static int CategoryRank(string? category) =>
        CategoryOrder.TryGetValue(category ?? "other", out var rank) ? rank : 4;
}
