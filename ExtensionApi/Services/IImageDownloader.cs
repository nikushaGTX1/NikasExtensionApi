namespace ExtensionApi.Services;

public sealed record DownloadedImage(int Index, string MimeType, byte[] Bytes);

public interface IImageDownloader
{
    Task<IReadOnlyList<DownloadedImage>> DownloadAsync(
        IReadOnlyList<string> imageUrls,
        int maxImageBytes,
        int maxTotalBytes,
        CancellationToken cancellationToken);
}
