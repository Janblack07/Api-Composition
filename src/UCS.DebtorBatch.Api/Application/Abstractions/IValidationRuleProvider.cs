using UCS.DebtorBatch.Api.DomainLike;

namespace UCS.DebtorBatch.Api.Application.Abstractions
{
    public interface IValidationRuleProvider
    {
        Task<ValidationRule> GetRulesAsync(Guid tenantId, CancellationToken ct);
    }
}
