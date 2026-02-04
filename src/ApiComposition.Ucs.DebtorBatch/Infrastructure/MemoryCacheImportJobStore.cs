using ApiComposition.Ucs.DebtorBatch.Domain;
using ApiComposition.Ucs.DebtorBatch.Ports;
using Microsoft.Extensions.Caching.Memory;

namespace ApiComposition.Ucs.DebtorBatch.Infrastructure
{
    public sealed class MemoryCacheImportJobStore(IMemoryCache cache) : IImportJobStore
    {
        private static string Key(Guid jobId) => $"importjob:{jobId:N}";
        private sealed record Entry(ImportJob Job, DateTimeOffset ExpiresAt);

        public Task<ImportJob?> GetAsync(Guid jobId, CancellationToken ct = default)
        {
            cache.TryGetValue(Key(jobId), out Entry? entry);
            return Task.FromResult(entry?.Job);
        }

        public Task SetAsync(ImportJob job, TimeSpan ttl, CancellationToken ct = default)
        {
            var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
            var entry = new Entry(job, expiresAt);

            cache.Set(Key(job.JobId), entry, new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = expiresAt
            });

            return Task.CompletedTask;
        }

        public Task UpdateAsync(Guid jobId, Action<ImportJob> mutate, CancellationToken ct = default)
        {
            var key = Key(jobId);
            if (!cache.TryGetValue(key, out Entry? entry) || entry is null)
                return Task.CompletedTask;

            mutate(entry.Job);

            cache.Set(key, entry, new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = entry.ExpiresAt
            });

            return Task.CompletedTask;
        }
    }
}
