namespace UCS.DebtorBatch.Api.Options
{
    public sealed class CoreOptions
    {
        public required string BaseUrl { get; init; } 
        public required string BatchImportPath { get; init; } = "/debtors/batch-import"; 
    }
}
