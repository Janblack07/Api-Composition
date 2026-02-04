namespace ApiComposition.Ucs.DebtorBatch.Security
{
    public sealed record TenantContext(Guid TenantId, Guid? DepartmentId, string UserId);
}
