namespace UCS.DebtorBatch.Api.Options
{
    public sealed class ImportOptions
    {
        public int MaxFileSizeMB { get; init; } = 10;
        public int BatchSize { get; init; } = 500;
        public int FailFastThresholdPercent { get; init; } = 10;
        public int JobStateTTLHours { get; init; } = 24;
        public int ValidationCacheTTLDays { get; init; } = 7;
        public int RetryAttempts { get; init; } = 3;
        public int CoreTimeoutSeconds { get; init; } = 30;
        public int PresignedUrlExpirationMinutes { get; init; } = 15;
    }
}
