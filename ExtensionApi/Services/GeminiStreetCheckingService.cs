using System.Net.Http.Json;
using System.Text.Json;
using ExtensionApi.Models;
using ExtensionApi.Options;
using Microsoft.Extensions.Options;

namespace ExtensionApi.Services;

public sealed class GeminiStreetCheckingService(
    IHttpClientFactory httpClientFactory,
    IOptions<GeminiOptions> options,
    ILogger<GeminiStreetCheckingService> logger) : IStreetCheckingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly GeminiOptions settings = options.Value;

    public async Task<StreetCheckResponse> CheckAsync(StreetCheckRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new PhotoOrderingNotConfiguredException("Gemini__ApiKey is not configured.");
        if (request.Candidates.Count is < 2 or > 20)
            throw new PhotoInputException("Send between 2 and 20 street candidates.");
        if (request.Candidates.Select(item => item.Index).Distinct().Count() != request.Candidates.Count)
            throw new PhotoInputException("Street candidate indexes must be unique.");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 120)));
        var prompt = BuildPrompt(request);
        var body = new
        {
            contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseJsonSchema = ResponseSchema(request.Candidates)
            }
        };

        var client = httpClientFactory.CreateClient(GeminiPhotoOrderingService.ClientName);
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
            throw new PhotoProviderException("Gemini street checking timed out.", error);
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
                var choice = JsonSerializer.Deserialize<GeminiStreetChoice>(json ?? string.Empty, JsonOptions)
                    ?? throw new JsonException("Gemini response was empty.");
                return StreetCheckNormalizer.Normalize(choice, request.Candidates);
            }
            catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
            {
                logger.LogWarning(error, "Gemini returned an invalid street-check response.");
                throw new PhotoProviderException("Gemini returned an invalid street-check response.", error);
            }
        }
    }

    private static string BuildPrompt(StreetCheckRequest request) => $"""
        Check an ambiguous Georgian real-estate street name for the {request.Site} website.
        You may select ONLY one candidate index from the supplied JSON, or -1 if none is clearly the same street.
        Account for Georgian inflection, initials versus full names, common abbreviations, transliteration, and former
        names, but never guess merely because two streets share a district. Use confidence 0-100 and select a street
        only at 80 or higher confidence. Source street: {JsonSerializer.Serialize(request.SourceStreet)}
        Full address: {JsonSerializer.Serialize(request.Address)}
        District: {JsonSerializer.Serialize(request.District)}
        Neighborhood: {JsonSerializer.Serialize(request.Neighborhood)}
        Candidate JSON: {JsonSerializer.Serialize(request.Candidates, JsonOptions)}
        """;

    private static object ResponseSchema(IReadOnlyList<StreetCandidate> candidates) => new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            selectedIndex = new
            {
                type = "integer",
                @enum = candidates.Select(item => item.Index).Append(-1).Distinct().ToArray()
            },
            confidence = new { type = "integer", minimum = 0, maximum = 100 },
            reason = new { type = "string" }
        },
        required = new[] { "selectedIndex", "confidence", "reason" }
    };
}
