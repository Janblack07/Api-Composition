using Microsoft.Extensions.Options;
using UCS.DebtorBatch.Api.Application.Abstractions;
using UCS.DebtorBatch.Api.Contracts.Shared;
using UCS.DebtorBatch.Api.DomainLike;
using UCS.DebtorBatch.Api.Options;

namespace UCS.DebtorBatch.Api.Application.Import
{
    public sealed class ImportJobTracker(IImportJobRepository repo, IOptions<ImportOptions> opt)
    {
        private readonly ImportOptions _o = opt.Value;
        private TimeSpan JobTtl => TimeSpan.FromHours(_o.JobStateTTLHours);

        public Task CreateAsync(ImportJob job, CancellationToken ct)
            => repo.CreateAsync(job, JobTtl, ct);

        public async Task UpdateAsync(ImportJob job, ImportJobStatus status, CancellationToken ct, string? failureReason = null)
        {
            job.Status = status;
            if (failureReason is not null)
                job.FailureReason = failureReason;

            job.Touch();
            await repo.UpdateAsync(job, JobTtl, ct);
        }

        public async Task UpdateProgressAsync(ImportJob job, int total, int processed, int failed, CancellationToken ct)
        {
            job.TotalRecords = total;
            job.ProcessedRecords = processed;
            job.FailedRecords = failed;

            var done = processed + failed;
            job.ProgressPercentage = total <= 0 ? 0 : (int)Math.Min(100, Math.Round((done * 100.0) / total));
            job.Touch();

            await repo.UpdateAsync(job, JobTtl, ct);
        }
    }
}
