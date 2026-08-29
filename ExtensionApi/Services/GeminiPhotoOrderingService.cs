using System.Net.Http.Json;
using System.Text.Json;
using ExtensionApi.Models;
using ExtensionApi.Options;
using Microsoft.Extensions.Options;

namespace ExtensionApi.Services;

public sealed class GeminiPhotoOrderingService(
    IHttpClientFactory httpClientFactory,
    IImageDownloader imageDownloader,
    IOptions<GeminiOptions> options,
    ILogger<GeminiPhotoOrderingService> logger) : IPhotoOrderingService
{
    public const string ClientName = "gemini";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly GeminiOptions settings = options.Value;

    public async Task<PhotoOrderResponse> OrderAsync(PhotoOrderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new PhotoOrderingNotConfiguredException("Gemini__ApiKey is not configured.");
        if (request.ImageUrls.Count is < 2 || request.ImageUrls.Count > settings.MaxImages)
            throw new PhotoInputException($"Send between 2 and {settings.MaxImages} image URLs.");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 120)));
        var images = await imageDownloader.DownloadAsync(
            request.ImageUrls,
            settings.MaxImageBytes,
            settings.MaxTotalImageBytes,
            deadline.Token);

        var parts = new List<object>
        {
            new { text = BuildPrompt(request.ImageUrls.Count) }
        };
        foreach (var image in images)
        {
            parts.Add(new { text = $"IMAGE INDEX {image.Index}" });
            parts.Add(new { inlineData = new { mimeType = image.MimeType, data = Convert.ToBase64String(image.Bytes) } });
        }

        var body = new
        {
            contents = new[] { new { role = "user", parts } },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseJsonSchema = ResponseSchema(request.ImageUrls.Count)
            }
        };

        var client = httpClientFactory.CreateClient(ClientName);
        client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = Timeout.InfiniteTimeSpan;
        using var message = new HttpRequestMessage(HttpMethod.Post,
            $"models/{Uri.EscapeDataString(settings.Model)}:generateContent");
        message.Headers.Add("x-goog-api-key", settings.ApiKey);
        message.Content = JsonContent.Create(body, options: JsonOptions);

        HttpResponseMessage response;
        try { response = await client.SendAsync(message, deadline.Token); }
        catch (OperationCanceledException error) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PhotoProviderException("Gemini photo ordering timed out.", error);
        }
        catch (HttpRequestException error)
        {
            throw new PhotoProviderException("Gemini could not be reached.", error);
        }

        using (response)
        {
            var responseText = await response.Content.ReadAsStringAsync(deadline.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Gemini returned {StatusCode}: {Response}", (int)response.StatusCode, responseText);
                throw new PhotoProviderException($"Gemini returned HTTP {(int)response.StatusCode}.");
            }

            try
            {
                using var envelope = JsonDocument.Parse(responseText);
                var json = envelope.RootElement.GetProperty("candidates")[0]
                    .GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                var result = JsonSerializer.Deserialize<GeminiPhotoOrder>(json ?? string.Empty, JsonOptions)
                    ?? throw new JsonException("Gemini response was empty.");
                return PhotoOrderNormalizer.Normalize(result, request.ImageUrls.Count);
            }
            catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
            {
                logger.LogWarning(error, "Gemini returned an invalid photo-order response.");
                throw new PhotoProviderException("Gemini returned an invalid photo-order response.", error);
            }
        }
    }

    private static string BuildPrompt(int count) => $"""
        Classify these {count} real-estate listing images and return every image index exactly once.
        The required category sequence is strict: all living-room images first, then all bedrooms, then all
        kitchens, then all balconies/views, then exterior/other/detail/hallway/utility images, and finally all
        bathrooms and toilets. Treat an open-plan living/dining room as living_room. Do not classify a balcony
        door or window seen from inside as balcony_view unless the photo is primarily of the balcony or its view.
        Within each category put sharp, bright, wide, non-duplicate photos before weak or duplicate photos.
        Classify every image and score visual quality from 0 to 100. Do not invent facts. The server will enforce
        the category sequence from your classifications even if orderedIndexes is imperfect.
        """;

    private static object ResponseSchema(int count) => new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            orderedIndexes = new
            {
                type = "array",
                minItems = count,
                maxItems = count,
                items = new { type = "integer", minimum = 0, maximum = count - 1 }
            },
            coverCategory = new { type = "string" },
            images = new
            {
                type = "array",
                minItems = count,
                maxItems = count,
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    properties = new
                    {
                        index = new { type = "integer", minimum = 0, maximum = count - 1 },
                        category = new
                        {
                            type = "string",
                            @enum = new[] { "living_room", "exterior", "bedroom", "kitchen", "balcony_view", "bathroom", "toilet", "hallway", "utility", "detail", "other" }
                        },
                        qualityScore = new { type = "integer", minimum = 0, maximum = 100 },
                        isDuplicate = new { type = "boolean" },
                        isBlurry = new { type = "boolean" }
                    },
                    required = new[] { "index", "category", "qualityScore", "isDuplicate", "isBlurry" }
                }
            }
        },
        required = new[] { "orderedIndexes", "coverCategory", "images" }
    };
}
