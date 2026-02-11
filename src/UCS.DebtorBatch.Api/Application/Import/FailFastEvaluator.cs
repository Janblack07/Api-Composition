namespace UCS.DebtorBatch.Api.Application.Import
{
    public static class FailFastEvaluator
    {
        public static bool ShouldFailFast(int inspectedRows, int invalidRows, int thresholdPercent)
        {
            if (inspectedRows < 100) return false;
            var percent = (invalidRows * 100.0) / inspectedRows;
            return percent > thresholdPercent;
        }
    }
}
