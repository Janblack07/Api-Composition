namespace ApiComposition.Ucs.DebtorBatch.Security
{
    public interface ITenantContextAccessor
    {
        TenantContext GetOrThrow();
    }
}
