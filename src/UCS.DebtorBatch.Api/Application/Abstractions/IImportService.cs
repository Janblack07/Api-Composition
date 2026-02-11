using UCS.DebtorBatch.Api.DomainLike;

namespace UCS.DebtorBatch.Api.Application.Abstractions
{
    public interface IImportService
    {
        Task<Guid> StartUploadAsync(Stream fileStream, string originalFileName, long fileSizeBytes, string? correlationId, string userJwt, CancellationToken ct);
        Task<ImportJob> GetJobAsync(Guid jobId, Guid tenantId, CancellationToken ct);
        Task<(string? url, DateTimeOffset? expiresAt, int recordCount)> GetErrorLogAsync(Guid jobId, Guid tenantId, CancellationToken ct);
    }
}
