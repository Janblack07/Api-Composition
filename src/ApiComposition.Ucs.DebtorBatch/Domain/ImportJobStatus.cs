namespace ApiComposition.Ucs.DebtorBatch.Domain
{
    public enum ImportJobStatus
    {
        Queued,
        Uploading, 
        Validating, 
        Processing,
        Completed,
        Failed, 
    }
}
