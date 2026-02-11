using UCS.DebtorBatch.Api.Application.Abstractions;

namespace UCS.DebtorBatch.Api.Infrastructure.Storage
{
    public sealed class LocalFileStorage(IWebHostEnvironment env) : IFileStorage
    {
        private string Root => Path.Combine(env.ContentRootPath, "storage");

        public async Task<string> SaveAsync(Stream stream, string fileName, CancellationToken ct)
        {
            Directory.CreateDirectory(Root);
            var safe = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
            var path = Path.Combine(Root, safe);

            await using var fs = File.Create(path);
            await stream.CopyToAsync(fs, ct);
            return path;
        }

        public Task<Stream> OpenReadAsync(string fileUrl, CancellationToken ct)
        {
            // fileUrl es el path absoluto o relativo guardado en job.FileUrl
            var fullPath = Path.IsPathRooted(fileUrl)
                ? fileUrl
                : Path.Combine(AppContext.BaseDirectory, fileUrl);

            Stream fs = System.IO.File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult(fs);
        }

        public async Task<string> SaveErrorReportAsync(Stream stream, string fileName, TimeSpan ttl, CancellationToken ct)
        {
            Directory.CreateDirectory(Root);
            var safe = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
            var path = Path.Combine(Root, safe);

            await using var fs = File.Create(path);
            await stream.CopyToAsync(fs, ct);
            return path;
        }

        public Task<string> GetPresignedUrlAsync(string fileUrl, TimeSpan expiresIn, CancellationToken ct)
        {
            // Dev/local: devolvemos una URL de descarga a un endpoint estático si quieres,
            // pero para cumplir el contrato, devuelve un string.
            return Task.FromResult(fileUrl);
        }
    }
}
