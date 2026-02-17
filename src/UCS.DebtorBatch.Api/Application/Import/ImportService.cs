using Microsoft.Extensions.Options;
using UCS.DebtorBatch.Api.Application.Abstractions;
using UCS.DebtorBatch.Api.Application.Workers;
using UCS.DebtorBatch.Api.DomainLike;
using UCS.DebtorBatch.Api.Options;

namespace UCS.DebtorBatch.Api.Application.Import
{
    public sealed class ImportService(
    IFileStorage storage,
    IImportJobRepository repo,
    ImportJobTracker tracker,
    IBackgroundTaskQueue queue,
    IOptions<ImportOptions> opt)
    : IImportService
    {
        private readonly ImportOptions _o = opt.Value;

        public async Task<Guid> StartUploadAsync(Stream fileStream, string originalFileName, long fileSizeBytes, string? correlationId, string userJwt, CancellationToken ct)
        {
            var maxBytes = _o.MaxFileSizeMB * 1024L * 1024L;
            if (fileSizeBytes > maxBytes)
                throw new InvalidOperationException($"FILE_TOO_LARGE: max {_o.MaxFileSizeMB} MB");

            var jobId = Guid.NewGuid();

            // Aquí (modo local) guardamos en disco. En S3 sería SaveAsync->s3://...
            var fileUrl = await storage.SaveAsync(fileStream, originalFileName, ct);
            return jobId;
        }

        public async Task<ImportJob> GetJobAsync(Guid jobId, Guid tenantId, CancellationToken ct)
        {
            var job = await repo.GetAsync(jobId, ct) ?? throw new KeyNotFoundException("JOB_NOT_FOUND");
            if (job.TenantId != tenantId) throw new UnauthorizedAccessException("TENANT_MISMATCH");
            return job;
        }

        public async Task<(string? url, DateTimeOffset? expiresAt, int recordCount)> GetErrorLogAsync(Guid jobId, Guid tenantId, CancellationToken ct)
        {
            var job = await GetJobAsync(jobId, tenantId, ct);

            if (job.Status is not Contracts.Shared.ImportJobStatus.COMPLETED and not Contracts.Shared.ImportJobStatus.FAILED)
                throw new KeyNotFoundException("JOB_NOT_COMPLETED");

            if (job.FailedRecords <= 0 || string.IsNullOrWhiteSpace(job.ErrorFileUrl))
                return (null, null, 0);

            var url = await storage.GetPresignedUrlAsync(job.ErrorFileUrl!, TimeSpan.FromMinutes(_o.PresignedUrlExpirationMinutes), ct);
            var expires = DateTimeOffset.UtcNow.AddMinutes(_o.PresignedUrlExpirationMinutes);
            return (url, expires, job.FailedRecords);
        }
    }
}
