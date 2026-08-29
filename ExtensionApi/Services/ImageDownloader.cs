using System.Net;

namespace ExtensionApi.Services;

public sealed class ImageDownloader(IHttpClientFactory httpClientFactory) : IImageDownloader
{
    public const string ClientName = "listing-images";
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/heic", "image/heif"
    };

    public async Task<IReadOnlyList<DownloadedImage>> DownloadAsync(
        IReadOnlyList<string> imageUrls,
        int maxImageBytes,
        int maxTotalBytes,
        CancellationToken cancellationToken)
    {
        var images = new List<DownloadedImage>(imageUrls.Count);
        var totalBytes = 0;
        var client = httpClientFactory.CreateClient(ClientName);
        client.Timeout = TimeSpan.FromSeconds(12);

        for (var index = 0; index < imageUrls.Count; index++)
        {
            var uri = await ValidatePublicHttpsUriAsync(imageUrls[index], cancellationToken);
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest)
                throw new PhotoInputException($"Image {index} redirected; redirected image URLs are not accepted.");
            if (!response.IsSuccessStatusCode)
                throw new PhotoInputException($"Image {index} returned HTTP {(int)response.StatusCode}.");

            var mimeType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!AllowedMimeTypes.Contains(mimeType))
                throw new PhotoInputException($"Image {index} has unsupported content type '{mimeType}'.");
            if (response.Content.Headers.ContentLength is > 0 && response.Content.Headers.ContentLength > maxImageBytes)
                throw new PhotoInputException($"Image {index} is larger than the configured limit.");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[81_920];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > maxImageBytes || totalBytes + buffer.Length + read > maxTotalBytes)
                    throw new PhotoInputException("The listing images exceed the configured byte limit.");
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            }

            var bytes = buffer.ToArray();
            totalBytes += bytes.Length;
            images.Add(new DownloadedImage(index, mimeType, bytes));
        }

        return images;
    }

    private static async Task<Uri> ValidatePublicHttpsUriAsync(string value, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(uri.Host))
            throw new PhotoInputException("Every image URL must be an absolute HTTPS URL.");

        IPAddress[] addresses;
        try { addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw new PhotoInputException($"The image host '{uri.DnsSafeHost}' could not be resolved.");
        }

        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
            throw new PhotoInputException("Private, loopback, link-local, and multicast image hosts are not accepted.");
        return uri;
    }

    internal static bool IsPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ||
            address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal)
            return false;
        if (address.IsIPv4MappedToIPv6) return IsPublicAddress(address.MapToIPv4());
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            return (bytes[0] & 0xFE) != 0xFC;
        return bytes[0] != 0 && bytes[0] != 10 && bytes[0] != 127 && bytes[0] < 224 &&
               !(bytes[0] == 169 && bytes[1] == 254) &&
               !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) &&
               !(bytes[0] == 192 && bytes[1] == 168) &&
               !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127) &&
               !(bytes[0] == 198 && bytes[1] is 18 or 19);
    }
}
