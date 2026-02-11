namespace UCS.DebtorBatch.Api.Options
{
    public sealed class MockIdentityOptions
    {
        public bool Enabled { get; init; } = true;
        public string TenantId { get; init; } = "00000000-0000-0000-0000-000000000001";
        public string DepartmentId { get; init; } = "00000000-0000-0000-0000-000000000002";
        public string UserId { get; init; } = "00000000-0000-0000-0000-000000000003";
        public string[] Permissions { get; init; } = ["debtor:batch:create"];
    }
}
