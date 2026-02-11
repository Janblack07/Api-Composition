using UCS.DebtorBatch.Api.Application.Abstractions;
using UCS.DebtorBatch.Api.Application.Workers;

namespace UCS.DebtorBatch.Api.Application.Import
{
    public sealed class ImportJobDispatcher(
        IBackgroundTaskQueue queue,
        IImportJobExecutor executor)
    {
        public void Enqueue(Guid jobId, string userJwt, Guid tenantId, Guid departmentId, string correlationId)
        {
            queue.Enqueue(ct => executor.ExecuteAsync(jobId, userJwt, tenantId, departmentId, correlationId, ct));
        }
    }
}