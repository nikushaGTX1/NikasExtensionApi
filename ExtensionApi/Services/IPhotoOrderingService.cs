using ExtensionApi.Models;

namespace ExtensionApi.Services;

public interface IPhotoOrderingService
{
    Task<PhotoOrderResponse> OrderAsync(PhotoOrderRequest request, CancellationToken cancellationToken);
}
