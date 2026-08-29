using System.ComponentModel.DataAnnotations;

namespace ExtensionApi.Models;

public sealed class StreetCheckRequest
{
    [Required, RegularExpression("^(myhome|ss)$")]
    public string Site { get; init; } = string.Empty;

    [Required, StringLength(250, MinimumLength = 2)]
    public string SourceStreet { get; init; } = string.Empty;

    [StringLength(350)]
    public string? Address { get; init; }

    [StringLength(150)]
    public string? District { get; init; }

    [StringLength(150)]
    public string? Neighborhood { get; init; }

    [Required, MinLength(2), MaxLength(20)]
    public IReadOnlyList<StreetCandidate> Candidates { get; init; } = [];
}

public sealed class StreetCandidate
{
    [Range(0, 1000)]
    public int Index { get; init; }

    [Required, StringLength(250, MinimumLength = 2)]
    public string Label { get; init; } = string.Empty;

    [Range(0, 120)]
    public int MatcherScore { get; init; }
}
