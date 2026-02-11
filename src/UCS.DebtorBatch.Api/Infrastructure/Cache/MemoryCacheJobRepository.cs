using Microsoft.Extensions.Caching.Memory;
using UCS.DebtorBatch.Api.Application.Abstractions;
using UCS.DebtorBatch.Api.DomainLike;

namespace UCS.DebtorBatch.Api.Infrastructure.Cache
{
    public sealed class MemoryCacheJobRepository(IMemoryCache cache) : IImportJobRepository
    {
        private static string Key(Guid jobId) => $"import-job:{jobId:N}";

        public Task CreateAsync(ImportJob job, TimeSpan ttl, CancellationToken ct)
        {
            cache.Set(Key(job.JobId), job, ttl);
            return Task.CompletedTask;
        }

        public Task<ImportJob?> GetAsync(Guid jobId, CancellationToken ct)
        {
            cache.TryGetValue(Key(jobId), out ImportJob? job);
            return Task.FromResult(job);
        }

        public Task UpdateAsync(ImportJob job, TimeSpan ttl, CancellationToken ct)
        {
            cache.Set(Key(job.JobId), job, ttl);
            return Task.CompletedTask;
        }
    }
}
