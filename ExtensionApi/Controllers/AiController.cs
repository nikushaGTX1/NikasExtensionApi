using ExtensionApi.Models;
using ExtensionApi.Security;
using ExtensionApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExtensionApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("listing-ai")]
[ExtensionApiKey]
public sealed class AiController(
    IPhotoOrderingService photoOrderingService,
    IStreetCheckingService streetCheckingService,
    ILogger<AiController> logger) : ControllerBase
{
    /// <summary>Classifies listing photos and enforces the configured real-estate room sequence.</summary>
    /// <remarks>
    /// Accepts 2–20 public HTTPS image URLs. Images are analyzed but are not persisted. The response contains
    /// every source index exactly once in this order: living rooms, bedrooms, kitchens, balconies/views,
    /// other/exterior images, then bathrooms/toilets. Clients should retain their original order if this endpoint fails.
    /// </remarks>
    [HttpPost("photo-order", Name = "OrderListingPhotos")]
    [ProducesResponseType<PhotoOrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PhotoOrderResponse>> OrderPhotos(
        [FromBody] PhotoOrderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await photoOrderingService.OrderAsync(request, cancellationToken));
        }
        catch (PhotoInputException error)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid image input", Detail = error.Message });
        }
        catch (PhotoOrderingNotConfiguredException error)
        {
            logger.LogError(error, "Photo ordering is not configured.");
            return StatusCode(503, new ProblemDetails { Status = 503, Title = "Photo ordering is not configured", Detail = error.Message });
        }
        catch (PhotoProviderException error)
        {
            logger.LogWarning(error, "Photo ordering provider failed.");
            return StatusCode(502, new ProblemDetails { Status = 502, Title = "Photo ordering provider failed", Detail = error.Message });
        }
    }

    /// <summary>Chooses a street only from the supplied MyHome or SS.ge catalog candidates.</summary>
    /// <remarks>
    /// This is a fallback for unresolved or ambiguous catalog matches. It never returns an invented label:
    /// selectedLabel is copied server-side from the submitted candidate list. Confidence below 80 returns no match.
    /// </remarks>
    [HttpPost("street-check", Name = "CheckListingStreet")]
    [ProducesResponseType<StreetCheckResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<StreetCheckResponse>> CheckStreet(
        [FromBody] StreetCheckRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await streetCheckingService.CheckAsync(request, cancellationToken));
        }
        catch (PhotoInputException error)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid street input", Detail = error.Message });
        }
        catch (PhotoOrderingNotConfiguredException error)
        {
            logger.LogError(error, "Street checking is not configured.");
            return StatusCode(503, new ProblemDetails { Status = 503, Title = "Street checking is not configured", Detail = error.Message });
        }
        catch (PhotoProviderException error)
        {
            logger.LogWarning(error, "Street checking provider failed.");
            return StatusCode(502, new ProblemDetails { Status = 502, Title = "Street checking provider failed", Detail = error.Message });
        }
    }
}
