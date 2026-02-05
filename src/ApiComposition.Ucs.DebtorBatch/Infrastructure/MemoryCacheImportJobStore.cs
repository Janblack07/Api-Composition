using ApiComposition.Ucs.DebtorBatch.Domain;
using ApiComposition.Ucs.DebtorBatch.Ports;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace ApiComposition.Ucs.DebtorBatch.Infrastructure
{
    public sealed class MemoryCacheImportJobStore(IMemoryCache cache) : IImportJobStore
    {
        private static string Key(Guid jobId) => $"importjob:{jobId:N}";
        private sealed record Entry(ImportJob Job, DateTimeOffset ExpiresAt);

        // Evita que dos threads pisen el mismo job (controller + worker)
        private static readonly ConcurrentDictionary<Guid, object> _locks = new();

        private static object Gate(Guid jobId) => _locks.GetOrAdd(jobId, _ => new object());

        public Task<ImportJob?> GetAsync(Guid jobId, CancellationToken ct = default)
        {
            cache.TryGetValue(Key(jobId), out Entry? entry);
            return Task.FromResult(entry?.Job);
        }

        public Task SetAsync(ImportJob job, TimeSpan ttl, CancellationToken ct = default)
        {
            var expiresAt = DateTimeOffset.UtcNow.Add(ttl);

            lock (Gate(job.JobId))
            {
                job.RecalculateProgress();
                job.Touch();

                var entry = new Entry(job, expiresAt);

                cache.Set(Key(job.JobId), entry, new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = expiresAt
                });
            }

            return Task.CompletedTask;
        }

        public Task UpdateAsync(Guid jobId, Action<ImportJob> mutate, CancellationToken ct = default)
        {
            var key = Key(jobId);

            lock (Gate(jobId))
            {
                if (!cache.TryGetValue(key, out Entry? entry) || entry is null)
                    return Task.CompletedTask;

                mutate(entry.Job);

                entry.Job.RecalculateProgress();
                entry.Job.Touch();

                cache.Set(key, entry, new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = entry.ExpiresAt
                });
            }

            return Task.CompletedTask;
        }
    }
}