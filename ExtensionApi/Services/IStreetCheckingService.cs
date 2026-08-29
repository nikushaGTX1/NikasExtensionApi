using ExtensionApi.Models;

namespace ExtensionApi.Services;

public interface IStreetCheckingService
{
    Task<StreetCheckResponse> CheckAsync(StreetCheckRequest request, CancellationToken cancellationToken);
}
