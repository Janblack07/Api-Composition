using ApiComposition.Ucs.DebtorBatch.Domain;
using ApiComposition.Ucs.DebtorBatch.Ports;

namespace ApiComposition.Ucs.DebtorBatch.Workers
{
    public sealed class ImportJobWorker(
    IImportQueue queue,
    IImportJobStore store,
    ILogger<ImportJobWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("ImportJobWorker started");

            while (!stoppingToken.IsCancellationRequested)
            {
                Guid jobId;

                try
                {
                    jobId = await queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error dequeuing jobId");
                    await Task.Delay(500, stoppingToken);
                    continue;
                }

                try
                {
                    var job = await store.GetAsync(jobId, stoppingToken);
                    if (job is null)
                    {
                        logger.LogWarning("Job not found: {JobId}", jobId);
                        continue;
                    }

                    await store.UpdateAsync(jobId, j => j.Status = ImportJobStatus.Validating, stoppingToken);
                    await Task.Delay(500, stoppingToken);

                    // Simulación de procesamiento (Iteración 0)
                    const int total = 10;
                    await store.UpdateAsync(jobId, j =>
                    {
                        j.Status = ImportJobStatus.Processing;
                        j.TotalRecords = total;
                        j.ProcessedRecords = 0;
                        j.FailedRecords = 0;
                        j.FailureReason = null;
                    }, stoppingToken);

                    for (var i = 1; i <= total; i++)
                    {
                        await Task.Delay(250, stoppingToken);

                        await store.UpdateAsync(jobId, j =>
                        {
                            j.ProcessedRecords = i;
                        }, stoppingToken);
                    }

                    await store.UpdateAsync(jobId, j => j.Status = ImportJobStatus.Completed, stoppingToken);
                    logger.LogInformation("Job completed: {JobId}", jobId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Job failed: {JobId}", jobId);

                    await store.UpdateAsync(jobId, j =>
                    {
                        j.Status = ImportJobStatus.Failed;
                        j.FailureReason = ex.Message;
                    }, stoppingToken);
                }
            }

            logger.LogInformation("ImportJobWorker stopped");
        }
    }
}
