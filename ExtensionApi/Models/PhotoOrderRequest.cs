using System.ComponentModel.DataAnnotations;

namespace ExtensionApi.Models;

public sealed class PhotoOrderRequest
{
    public string? SourceUrl { get; init; }
    public string? ListingId { get; init; }

    [Required, MinLength(2), MaxLength(20)]
    public IReadOnlyList<string> ImageUrls { get; init; } = [];

    public PhotoCoverRules? CoverRules { get; init; }
}

public sealed class PhotoCoverRules
{
    public IReadOnlyList<string> Prefer { get; init; } = [];
    public IReadOnlyList<string> Avoid { get; init; } = [];
}
