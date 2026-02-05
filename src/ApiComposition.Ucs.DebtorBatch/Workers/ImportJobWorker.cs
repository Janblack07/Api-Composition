using ApiComposition.Ucs.DebtorBatch.Domain;
using ApiComposition.Ucs.DebtorBatch.Ports;

namespace ApiComposition.Ucs.DebtorBatch.Workers
{
    public sealed class ImportJobWorker(
         IImportQueue queue,
         IImportJobStore store,
         IObjectStorage storage,
         IImportFileReader fileReader,
         IDebtorRecordValidator validator,
         IErrorReportWriter errorWriter,
         IConfiguration cfg,
         ILogger<ImportJobWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("ImportJobWorker started");

            var chunkSize = cfg.GetValue<int>("Processing:ChunkSize", 500);
            var maxErrors = cfg.GetValue<int>("Processing:MaxErrorsInMemory", 50_000);

            while (!stoppingToken.IsCancellationRequested)
            {
                Guid jobId;

                try
                {
                    jobId = await queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
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

                    if (string.IsNullOrWhiteSpace(job.SourceObjectKey))
                    {
                        await store.UpdateAsync(jobId, j =>
                        {
                            j.Status = ImportJobStatus.Failed;
                            j.FailureReason = "SourceObjectKey is missing.";
                        }, stoppingToken);
                        continue;
                    }

                    await store.UpdateAsync(jobId, j =>
                    {
                        j.Status = ImportJobStatus.Validating;
                        j.FailureReason = null;
                        j.TotalRecords = 0;
                        j.ProcessedRecords = 0;
                        j.FailedRecords = 0;
                        j.ErrorsReportObjectKey = null;
                    }, stoppingToken);

                    await using var fileStream = await storage.OpenReadAsync(job.SourceObjectKey, stoppingToken);

                    // -------------------------------
                    // 1) FAIL-FAST: primeras 100 filas
                    // -------------------------------
                    var errors = new List<ValidationError>(capacity: 1024);

                    int checkedRows = 0;
                    int invalidRows = 0;

                    // Como el reader es streaming, guardamos las primeras 100 para luego reprocesar sin re-leer
                    // => Solución simple: volvemos a abrir el stream y recorremos completo después del fail-fast.
                    // (con S3 real esto es normal, igual haces OpenRead otra vez).
                    await foreach (var (row, rec) in fileReader.ReadAsync(fileStream, job.SourceObjectKey, stoppingToken))
                    {
                        checkedRows++;
                        var rowErrors = validator.Validate(row, rec);

                        if (rowErrors.Count > 0)
                        {
                            invalidRows++;
                            if (errors.Count < maxErrors) errors.AddRange(rowErrors);
                        }

                        if (checkedRows >= 100) break;
                    }

                    if (checkedRows > 0)
                    {
                        var invalidRate = (double)invalidRows / checkedRows;
                        if (invalidRate > 0.10)
                        {
                            // Generar reporte y FAIL
                            var errorsKey = await errorWriter.WriteAsync(jobId, errors, stoppingToken);

                            await store.UpdateAsync(jobId, j =>
                            {
                                j.Status = ImportJobStatus.Failed;
                                j.FailureReason = $"Fail-fast: invalid rate {(invalidRate * 100):0.##}% in first {checkedRows} rows.";
                                j.ErrorsReportObjectKey = errorsKey;
                                j.TotalRecords = checkedRows;
                                j.ProcessedRecords = checkedRows;
                                j.FailedRecords = invalidRows;
                            }, stoppingToken);

                            logger.LogWarning("Job {JobId} fail-fast. InvalidRate={InvalidRate}", jobId, invalidRate);
                            continue;
                        }
                    }

                    // -------------------------------
                    // 2) Procesamiento completo real
                    // -------------------------------
                    await store.UpdateAsync(jobId, j =>
                    {
                        j.Status = ImportJobStatus.Processing;
                        j.FailureReason = null;
                        j.TotalRecords = 0; // lo iremos seteando al final (o progresivo)
                        j.ProcessedRecords = 0;
                        j.FailedRecords = 0;
                    }, stoppingToken);

                    // Reabrimos stream para leer completo desde el inicio
                    await using var fullStream = await storage.OpenReadAsync(job.SourceObjectKey, stoppingToken);

                    int total = 0;
                    int processed = 0;
                    int failed = 0;

                    var chunk = new List<(int Row, DebtorRecord Rec)>(chunkSize);

                    await foreach (var item in fileReader.ReadAsync(fullStream, job.SourceObjectKey, stoppingToken))
                    {
                        total++;
                        chunk.Add(item);

                        if (chunk.Count >= chunkSize)
                        {
                            await ProcessChunk(jobId, chunk, validator, errors, maxErrors,  processed,  failed, store, stoppingToken);
                            chunk.Clear();

                            // Total aún no definitivo, pero podemos aproximar
                            await store.UpdateAsync(jobId, j =>
                            {
                                j.TotalRecords = Math.Max(j.TotalRecords, total);
                            }, stoppingToken);
                        }
                    }

                    if (chunk.Count > 0)
                    {
                        await ProcessChunk(jobId, chunk, validator, errors, maxErrors,  processed,  failed, store, stoppingToken);
                        chunk.Clear();
                    }

                    // Si hubo errores, generar reporte
                    string? reportKey = null;
                    if (errors.Count > 0)
                        reportKey = await errorWriter.WriteAsync(jobId, errors, stoppingToken);

                    await store.UpdateAsync(jobId, j =>
                    {
                        j.TotalRecords = total;
                        j.ProcessedRecords = processed;
                        j.FailedRecords = failed;
                        j.ErrorsReportObjectKey = reportKey;
                        j.Status = ImportJobStatus.Completed;
                    }, stoppingToken);

                    logger.LogInformation("Job completed: {JobId}. Total={Total} Failed={Failed}", jobId, total, failed);
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

        private static async Task ProcessChunk(
            Guid jobId,
            List<(int Row, DebtorRecord Rec)> chunk,
            IDebtorRecordValidator validator,
            List<ValidationError> errors,
            int maxErrors,
             int processed,
             int failed,
            IImportJobStore store,
            CancellationToken ct)
        {
            int chunkFailed = 0;

            foreach (var (row, rec) in chunk)
            {
                var rowErrors = validator.Validate(row, rec);
                if (rowErrors.Count > 0)
                {
                    chunkFailed++;
                    if (errors.Count < maxErrors) errors.AddRange(rowErrors);
                }
            }

            processed += chunk.Count;
            failed += chunkFailed;

            await store.UpdateAsync(jobId, j =>
            {
                j.ProcessedRecords = processed;
                j.FailedRecords = failed;
                // TotalRecords se setea afuera cuando ya lo conocemos
            }, ct);
        }
    }
}
