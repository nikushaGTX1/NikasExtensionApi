using ExtensionApi.Models;
using ExtensionApi.Services;

namespace ExtensionApi.Tests;

public sealed class StreetCheckNormalizerTests
{
    private static readonly StreetCandidate[] Candidates =
    [
        new() { Index = 0, Label = "ილია ჭავჭავაძის გამზირი", MatcherScore = 88 },
        new() { Index = 1, Label = "ალექსანდრე ჭავჭავაძის ქუჩა", MatcherScore = 86 }
    ];

    [Fact]
    public void ReturnsExactSuppliedCandidateForConfidentChoice()
    {
        var result = StreetCheckNormalizer.Normalize(new GeminiStreetChoice(0, 94, "Same person and road type."), Candidates);
        Assert.True(result.Matched);
        Assert.Equal(0, result.SelectedIndex);
        Assert.Equal(Candidates[0].Label, result.SelectedLabel);
        Assert.Equal(94, result.Confidence);
    }

    [Fact]
    public void RejectsLowConfidenceChoice()
    {
        var result = StreetCheckNormalizer.Normalize(new GeminiStreetChoice(0, 79, "Uncertain."), Candidates);
        Assert.False(result.Matched);
        Assert.Null(result.SelectedIndex);
        Assert.Null(result.SelectedLabel);
    }

    [Fact]
    public void RejectsIndexThatWasNotSupplied()
    {
        var result = StreetCheckNormalizer.Normalize(new GeminiStreetChoice(42, 99, "Invented."), Candidates);
        Assert.False(result.Matched);
        Assert.Null(result.SelectedLabel);
    }

    [Fact]
    public void RejectsExplicitNoMatch()
    {
        var result = StreetCheckNormalizer.Normalize(new GeminiStreetChoice(-1, 95, "Different streets."), Candidates);
        Assert.False(result.Matched);
        Assert.Null(result.SelectedLabel);
    }
}
