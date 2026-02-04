using ApiComposition.Ucs.DebtorBatch.Ports;

namespace ApiComposition.Ucs.DebtorBatch.Infrastructure
{
    public sealed class LocalDiskObjectStorage(IConfiguration cfg) : IObjectStorage
    {
        private readonly string _root = cfg["Storage:Local:RootPath"] ?? "App_Data/uploads";

        public async Task<string> PutAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
        {
            Directory.CreateDirectory(_root);

            var safeName = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
            var path = Path.Combine(_root, safeName);

            await using var fs = File.Create(path);
            await content.CopyToAsync(fs, ct);

            return safeName; // objectKey
        }

        public Task<string> GetDownloadUrlAsync(string objectKey, TimeSpan ttl, CancellationToken ct = default)
        {
            // DEV: en PROD será presigned S3
            var url = $"/dev-download/{Uri.EscapeDataString(objectKey)}";
            return Task.FromResult(url);
        }
    }
}
