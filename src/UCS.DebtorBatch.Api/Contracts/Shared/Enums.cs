namespace UCS.DebtorBatch.Api.Contracts.Shared
{
    public enum ImportJobStatus
    {
        QUEUED,
        UPLOADING,
        VALIDATING,
        PROCESSING,
        COMPLETED,
        FAILED

    }
}