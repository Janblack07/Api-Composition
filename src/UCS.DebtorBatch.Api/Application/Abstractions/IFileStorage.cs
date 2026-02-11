namespace UCS.DebtorBatch.Api.Application.Abstractions
{
    public interface IFileStorage
    {
        Task<string> SaveAsync(Stream stream, string fileName, CancellationToken ct); // retorna FileUrl
        Task<Stream> OpenReadAsync(string fileUrl, CancellationToken ct);
        Task<string> SaveErrorReportAsync(Stream stream, string fileName, TimeSpan ttl, CancellationToken ct);
        Task<string> GetPresignedUrlAsync(string fileUrl, TimeSpan expiresIn, CancellationToken ct);
    }
}
