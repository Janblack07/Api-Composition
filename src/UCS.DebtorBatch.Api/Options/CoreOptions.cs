namespace UCS.DebtorBatch.Api.Options
{
    public sealed class CoreOptions
    {
        public required string BaseUrl { get; init; } // ej: https://api-uat-...azurewebsites.net
        public required string BatchImportPath { get; init; } = "/debtors/batch-import"; // /debtors/batch-import
    }
}
