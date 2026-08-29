using ExtensionApi.Models;
using ExtensionApi.Services;

namespace ExtensionApi.Tests;

public sealed class PhotoOrderNormalizerTests
{
    [Fact]
    public void Normalize_RemovesInvalidAndDuplicateIndexesAndAppendsMissingImages()
    {
        var input = new GeminiPhotoOrder([2, 2, 99, -1], "bedroom", []);

        var result = PhotoOrderNormalizer.Normalize(input, 4);

        Assert.Equal([2, 0, 1, 3], result.OrderedIndexes);
        Assert.Equal(4, result.Images.Count);
    }

    [Fact]
    public void Normalize_MovesSafeRoomAheadOfBathroom()
    {
        var input = new GeminiPhotoOrder(
            [0, 1, 2],
            "bathroom",
            [
                new GeminiImageAssessment(0, "bathroom", 95, false, false),
                new GeminiImageAssessment(1, "living_room", 80, false, false),
                new GeminiImageAssessment(2, "bedroom", 70, false, false)
            ]);

        var result = PhotoOrderNormalizer.Normalize(input, 3);

        Assert.Equal([1, 2, 0], result.OrderedIndexes);
        Assert.Equal("living_room", result.CoverCategory);
    }

    [Fact]
    public void Normalize_EnforcesCategorySequenceEvenForWeakPhotos()
    {
        var input = new GeminiPhotoOrder(
            [0, 1, 2],
            "bathroom",
            [
                new GeminiImageAssessment(0, "bathroom", 90, false, false),
                new GeminiImageAssessment(1, "living_room", 90, false, true),
                new GeminiImageAssessment(2, "bedroom", 90, true, false)
            ]);

        var result = PhotoOrderNormalizer.Normalize(input, 3);

        Assert.Equal([1, 2, 0], result.OrderedIndexes);
    }

    [Fact]
    public void Normalize_GroupsEveryRequestedRoomTypeAndLeavesBathroomsLast()
    {
        var input = new GeminiPhotoOrder(
            [0, 1, 2, 3, 4, 5, 6],
            "bathroom",
            [
                new GeminiImageAssessment(0, "bathroom", 99, false, false),
                new GeminiImageAssessment(1, "balcony_view", 70, false, false),
                new GeminiImageAssessment(2, "kitchen", 75, false, false),
                new GeminiImageAssessment(3, "bedroom", 80, false, false),
                new GeminiImageAssessment(4, "living_room", 85, false, false),
                new GeminiImageAssessment(5, "toilet", 90, false, false),
                new GeminiImageAssessment(6, "exterior", 95, false, false)
            ]);

        var result = PhotoOrderNormalizer.Normalize(input, 7);

        Assert.Equal([4, 3, 2, 1, 6, 0, 5], result.OrderedIndexes);
        Assert.Equal(
            ["living_room", "bedroom", "kitchen", "balcony_view", "exterior", "bathroom", "toilet"],
            result.Images.Select(image => image.Category));
    }
}

public sealed class ImageDownloaderAddressTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.20.1.2")]
    [InlineData("192.168.1.2")]
    [InlineData("169.254.1.2")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    public void IsPublicAddress_RejectsPrivateIpv4(string address)
    {
        Assert.False(ImageDownloader.IsPublicAddress(System.Net.IPAddress.Parse(address)));
    }

    [Fact]
    public void IsPublicAddress_AcceptsPublicIpv4()
    {
        Assert.True(ImageDownloader.IsPublicAddress(System.Net.IPAddress.Parse("8.8.8.8")));
    }
}
