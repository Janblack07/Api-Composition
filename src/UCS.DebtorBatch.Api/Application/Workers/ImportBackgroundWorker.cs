using Microsoft.Extensions.Options;
using UCS.DebtorBatch.Api.Application.Abstractions;
using UCS.DebtorBatch.Api.Application.Import;
using UCS.DebtorBatch.Api.Application.Validation;
using UCS.DebtorBatch.Api.Contracts.Shared;
using UCS.DebtorBatch.Api.DomainLike;
using UCS.DebtorBatch.Api.Infrastructure.Core;
using UCS.DebtorBatch.Api.Options;

namespace UCS.DebtorBatch.Api.Application.Workers;

public sealed class ImportBackgroundWorker(
    ILogger<ImportBackgroundWorker> logger,
    IBackgroundTaskQueue queue,
    IImportJobRepository repo,
    IFileStorage storage,
    IExcelParser parser,
    IValidationRuleProvider ruleProvider,
    DynamicValidatorFactory validatorFactory,
    IDebtorBatchClient coreClient,
    ImportJobTracker tracker,
    IOptions<ImportOptions> opt)
    : BackgroundService, IImportJobExecutor
{
    private readonly ImportOptions _o = opt.Value;

    private sealed record BatchSendResult(int ProcessedDelta, int FailedDelta);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var work = await queue.DequeueAsync(stoppingToken);
            await work(stoppingToken);
        }
    }

    public async Task ExecuteAsync(
    Guid jobId,
    string userJwt,
    Guid tenantId,
    Guid departmentId,
    string correlationId,
    CancellationToken ct)
    {
        var job = await repo.GetAsync(jobId, ct);
        if (job is null) return;

        using var _scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["JobId"] = jobId,
            ["TenantId"] = tenantId,
            ["DepartmentId"] = departmentId,
            ["CorrelationId"] = correlationId
        });

        logger.LogInformation("JOB START -> FileUrl={FileUrl}", job.FileUrl);

        try
        {
            await tracker.UpdateAsync(job, ImportJobStatus.VALIDATING, ct);

            var rules = await ruleProvider.GetRulesAsync(tenantId, ct);
            var validate = validatorFactory.Build(rules);

            await using var file = await storage.OpenReadAsync(job.FileUrl, ct);

            int total = 0;
            int processed = 0;
            int failed = 0;

            // Fail-fast
            int inspected = 0;
            int invalid = 0;

            var errors = new List<ValidationError>(capacity: 256);
            var validBuffer = new List<DebtorRecord>(capacity: _o.BatchSize);

            await tracker.UpdateAsync(job, ImportJobStatus.PROCESSING, ct);

            await foreach (var record in parser.ParseAsync(file, ct))
            {
                ct.ThrowIfCancellationRequested();
                total++;

                // contamos filas inspeccionadas hasta N
                if (inspected < _o.FailFastInspectRows) inspected++;

                var rowErrors = validate(record).ToList();
                if (rowErrors.Count > 0)
                {
                    failed++;
                    errors.AddRange(rowErrors);

                    if (inspected <= _o.FailFastInspectRows) // dentro del muestreo
                    {
                        invalid++;

                        if (FailFastEvaluator.ShouldFailFast(
                            inspectedRows: inspected,
                            invalidRows: invalid,
                            thresholdPercent: _o.FailFastThresholdPercent,
                            inspectRowsTarget: _o.FailFastInspectRows,
                            endOfFile: false))
                        {
                            logger.LogWarning(
                                "FAIL-FAST TRIGGERED -> inspected={Inspected} invalid={Invalid} threshold={Threshold}% targetRows={Target}",
                                inspected, invalid, _o.FailFastThresholdPercent, _o.FailFastInspectRows);

                            await FailJobAsync(job, errors, total, processed, failed, ct, "FAIL_FAST_TRIGGERED");
                            return;
                        }
                    }

                    await tracker.UpdateProgressAsync(job, total, processed, failed, ct);
                    continue;
                }

                validBuffer.Add(record);

                if (validBuffer.Count >= _o.BatchSize)
                {
                    var result = await SendChunkAsync(job, validBuffer, userJwt, tenantId, departmentId, correlationId, errors, ct);
                    processed += result.ProcessedDelta;
                    failed += result.FailedDelta;

                    validBuffer.Clear();
                    await tracker.UpdateProgressAsync(job, total, processed, failed, ct);
                }
            }

            // EOF: Si el archivo tenía < N filas, aquí se evalúa fail-fast con las filas reales
            if (FailFastEvaluator.ShouldFailFast(
                inspectedRows: inspected,
                invalidRows: invalid,
                thresholdPercent: _o.FailFastThresholdPercent,
                inspectRowsTarget: _o.FailFastInspectRows,
                endOfFile: true))
            {
                logger.LogWarning(
                    "FAIL-FAST TRIGGERED (EOF) -> inspected={Inspected} invalid={Invalid} threshold={Threshold}% targetRows={Target}",
                    inspected, invalid, _o.FailFastThresholdPercent, _o.FailFastInspectRows);

                await FailJobAsync(job, errors, total, processed, failed, ct, "FAIL_FAST_TRIGGERED");
                return;
            }

            // Remanente
            if (validBuffer.Count > 0)
            {
                var result = await SendChunkAsync(job, validBuffer, userJwt, tenantId, departmentId, correlationId, errors, ct);
                processed += result.ProcessedDelta;
                failed += result.FailedDelta;

                validBuffer.Clear();
                await tracker.UpdateProgressAsync(job, total, processed, failed, ct);
            }

            // Reporte de errores (si aplica)
            if (errors.Count > 0)
            {
                var errorFileUrl = await WriteErrorReportAsync(jobId, errors, ct);
                job.ErrorFileUrl = errorFileUrl;

                logger.LogInformation("ERROR REPORT GENERATED -> ErrorFileUrl={ErrorFileUrl} ErrorsCount={ErrorsCount}",
                    job.ErrorFileUrl, errors.Count);
            }

            await tracker.UpdateProgressAsync(job, total, processed, failed, ct);
            await tracker.UpdateAsync(job, ImportJobStatus.COMPLETED, ct);

            logger.LogInformation("JOB COMPLETED -> total={Total} processed={Processed} failed={Failed} errors={ErrorsCount}",
                total, processed, failed, errors.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId} failed", jobId);

            var reason = ex is HttpRequestException
                ? "CORE_COMMUNICATION_ERROR"
                : "UNHANDLED_ERROR";

            await tracker.UpdateAsync(job, ImportJobStatus.FAILED, ct, reason);
        }
    }

    private async Task<BatchSendResult> SendChunkAsync(
     ImportJob job,
     IReadOnlyList<DebtorRecord> chunk,
     string userJwt,
     Guid tenantId,
     Guid departmentId,
     string correlationId,
     List<ValidationError> errors,
     CancellationToken ct)
    {
        var req = BatchBuilder.Build(job.JobId, chunk);

        try
        {
            var resp = await coreClient.SendBatchAsync(req, userJwt, tenantId, departmentId, correlationId, ct);

            // Errores por fila que devuelve el Core (esto NO tumba el job)
            if (resp?.Data?.Errors is not null)
            {
                foreach (var e in resp.Data.Errors)
                    errors.Add(new ValidationError(e.RowIndex, e.ExternalKey, e.Message));
            }

            return new BatchSendResult(
                ProcessedDelta: resp?.Data?.ProcessedCount ?? 0,
                FailedDelta: resp?.Data?.FailedCount ?? 0
            );
        }
        catch (HttpRequestException ex)
        {
            // ✅ ESTE ES EL CAMBIO: comunicación con Core tumba el Job
            // (404, 5xx, DNS, conexión, etc.)
            logger.LogError(ex,
                "CORE COMMUNICATION ERROR -> JobId={JobId} Items={Items} CorrelationId={CorrelationId}",
                job.JobId, chunk.Count, correlationId);

            // lanzamos para que el catch general marque FAILED
            throw;
        }
        catch (TaskCanceledException ex)
        {
            // timeout (HttpClient) normalmente cae aquí
            logger.LogError(ex,
                "CORE TIMEOUT -> JobId={JobId} Items={Items} CorrelationId={CorrelationId}",
                job.JobId, chunk.Count, correlationId);

            throw new HttpRequestException("Core request timeout.", ex);
        }
    }

    private async Task FailJobAsync(
        ImportJob job,
        List<ValidationError> errors,
        int total,
        int processed,
        int failed,
        CancellationToken ct,
        string reason)
    {
        if (errors.Count > 0)
        {
            var errorFileUrl = await WriteErrorReportAsync(job.JobId, errors, ct);
            job.ErrorFileUrl = errorFileUrl;

            logger.LogInformation("ERROR REPORT GENERATED (FAIL) -> ErrorFileUrl={ErrorFileUrl} ErrorsCount={ErrorsCount}",
                job.ErrorFileUrl, errors.Count);
        }

        await tracker.UpdateProgressAsync(job, total, processed, failed, ct);
        await tracker.UpdateAsync(job, ImportJobStatus.FAILED, ct, reason);

        logger.LogWarning("JOB FAILED -> reason={Reason} total={Total} processed={Processed} failed={Failed}",
            reason, total, processed, failed);
    }

    private async Task<string> WriteErrorReportAsync(Guid jobId, List<ValidationError> errors, CancellationToken ct)
    {
        var ms = new MemoryStream();
        await using (var sw = new StreamWriter(ms, leaveOpen: true))
        {
            await sw.WriteLineAsync("rowIndex,externalKey,message");

            foreach (var e in errors)
            {
                var msg = (e.Message ?? "").Replace("\"", "'");
                await sw.WriteLineAsync($"{e.RowIndex},{e.ExternalKey},\"{msg}\"");
            }

            await sw.FlushAsync();
        }

        ms.Position = 0;

        return await storage.SaveErrorReportAsync(ms, $"job-{jobId:N}-errors.csv", TimeSpan.FromDays(7), ct);
    }
}