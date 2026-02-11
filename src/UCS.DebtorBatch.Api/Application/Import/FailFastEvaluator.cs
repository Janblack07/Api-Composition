namespace UCS.DebtorBatch.Api.Application.Import;

public static class FailFastEvaluator
{
    public static bool ShouldFailFast(
        int inspectedRows,
        int invalidRows,
        int thresholdPercent,
        int inspectRowsTarget,
        bool endOfFile)
    {
        if (inspectedRows <= 0) return false;

        // Evaluar cuando llegas a N (ej 100) o cuando termina archivo si tiene menos.
        var shouldEvaluate = inspectedRows >= inspectRowsTarget || endOfFile;
        if (!shouldEvaluate) return false;

        var percent = (invalidRows * 100.0) / inspectedRows;

        // INCLUYENTE: 10% exacto debe fallar
        return percent >= thresholdPercent;
    }
}