namespace UCS.DebtorBatch.Api.Options;

public sealed class ImportOptions
{
    public int MaxFileSizeMB { get; set; } = 10;
    public int BatchSize { get; set; } = 500;

    public int FailFastThresholdPercent { get; set; } = 10;
    public int FailFastInspectRows { get; set; } = 100;

    public int JobStateTTLHours { get; set; } = 24;
    public int ValidationCacheTTLDays { get; set; } = 7;
    public int RetryAttempts { get; set; } = 3;
    public int CoreTimeoutSeconds { get; set; } = 30;

    public int PresignedUrlExpirationMinutes { get; set; } = 15;

    public int FileRetentionDays { get; set; } = 7;
}