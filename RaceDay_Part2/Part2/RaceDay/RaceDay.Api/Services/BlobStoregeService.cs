using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace RaceDay.Api.Services;

public interface IBlobStorageService
{
    /// <summary>Uploads an image and returns a read-only link to it.</summary>
    Task<string> UploadImageAsync(IFormFile file, string folder, CancellationToken ct = default);
    Task DeleteAsync(string? blobUrl, CancellationToken ct = default);
}

public class BlobStorageService : IBlobStorageService
{
    private static readonly string[] AllowedTypes = { "image/jpeg", "image/png", "image/webp" };
    private const long MaxBytes = 5 * 1024 * 1024;

    private readonly IConfiguration _config;
    private BlobContainerClient? _container;

    public BlobStorageService(IConfiguration config) => _config = config;

    // Created on first use, so the API (and the unit tests) start fine even without Azure configured.
    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken ct)
    {
        if (_container != null) return _container;

        var conn = _config["AzureBlob:ConnectionString"];
        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException("AzureBlob:ConnectionString is not configured.");

        var client = new BlobContainerClient(conn, _config["AzureBlob:ContainerName"] ?? "raceday-images");
        await client.CreateIfNotExistsAsync(cancellationToken: ct);   // private container
        _container = client;
        return client;
    }

    public async Task<string> UploadImageAsync(IFormFile file, string folder, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) throw new ArgumentException("The file is empty.");
        if (file.Length > MaxBytes) throw new ArgumentException("The image must be 5 MB or smaller.");
        if (!AllowedTypes.Contains(file.ContentType)) throw new ArgumentException("Only JPG, PNG or WEBP images are allowed.");

        var container = await GetContainerAsync(ct);
        var blob = container.GetBlobClient($"{folder}/{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}");

        await using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType }
        }, ct);

        // The container is private, so return a read-only link that is valid for one year
        return blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddYears(1)).ToString();
    }

    public async Task DeleteAsync(string? blobUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(blobUrl)) return;
        var container = await GetContainerAsync(ct);
        var name = new BlobUriBuilder(new Uri(blobUrl)).BlobName;
        await container.DeleteBlobIfExistsAsync(name, cancellationToken: ct);
    }
}