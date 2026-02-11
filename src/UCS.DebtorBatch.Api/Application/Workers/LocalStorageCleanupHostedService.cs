using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using UCS.DebtorBatch.Api.Options;

namespace UCS.DebtorBatch.Api.Application.Workers;

public sealed class LocalStorageCleanupHostedService(
    IOptions<ImportOptions> opt,
    ILogger<LocalStorageCleanupHostedService> logger)
    : BackgroundService
{
    private readonly ImportOptions _o = opt.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var retention = TimeSpan.FromDays(_o.FileRetentionDays);
                var cutoff = DateTimeOffset.UtcNow.Subtract(retention);

                var baseDir = Path.Combine(AppContext.BaseDirectory, "storage");
                if (!Directory.Exists(baseDir))
                {
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                    continue;
                }

                var files = Directory.EnumerateFiles(baseDir, "*", SearchOption.AllDirectories);
                var deleted = 0;

                foreach (var file in files)
                {
                    try
                    {
                        var info = new FileInfo(file);
                        var lastWrite = info.LastWriteTimeUtc;

                        if (lastWrite < cutoff.UtcDateTime)
                        {
                            info.Delete();
                            deleted++;
                        }
                    }
                    catch
                    {
                        // no tumbar el servicio por 1 archivo bloqueado
                    }
                }

                if (deleted > 0)
                    logger.LogInformation("Local storage cleanup deleted {Count} file(s)", deleted);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Local storage cleanup failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }
}