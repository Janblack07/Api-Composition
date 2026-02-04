using ApiComposition.Ucs.DebtorBatch.Ports;
using System.Threading.Channels;

namespace ApiComposition.Ucs.DebtorBatch.Infrastructure
{
    public sealed class InMemoryImportQueue : IImportQueue
    {
        public static readonly Channel<Guid> Queue = Channel.CreateUnbounded<Guid>();

        public async Task EnqueueAsync(Guid jobId, CancellationToken ct = default)
        => await Queue.Writer.WriteAsync(jobId, ct);

        public async ValueTask<Guid> DequeueAsync(CancellationToken ct = default)
            => await Queue.Reader.ReadAsync(ct);
    }
}
