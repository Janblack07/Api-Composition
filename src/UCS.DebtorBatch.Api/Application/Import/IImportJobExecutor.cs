namespace UCS.DebtorBatch.Api.Application.Import
{
    public interface IImportJobExecutor
    {
        Task ExecuteAsync(Guid jobId, string userJwt, Guid tenantId, Guid departmentId, string correlationId, CancellationToken ct);
    }
}
