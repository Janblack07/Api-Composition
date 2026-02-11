using UCS.DebtorBatch.Api.Application.Abstractions;
using UCS.DebtorBatch.Api.DomainLike;

namespace UCS.DebtorBatch.Api.Infrastructure.TenantRules
{
    public sealed class MockValidationRuleProvider : IValidationRuleProvider
    {
        public Task<ValidationRule> GetRulesAsync(Guid tenantId, CancellationToken ct)
        {
            // Reglas mínimas para probar el pipeline (ajusta según tu país/tenant).
            var rule = new ValidationRule
            {
                TenantId = tenantId,
                Validationprofile = new ValidationRule.ValidationProfile
                {
                    Phone = new ValidationRule.PhoneProfile
                    {
                        AllowList = true,
                        Separator = ",",
                        Formats =
                        [
                            new ValidationRule.PhoneFormat { Type = "Generic", Regex = @"^\d{7,15}$", Description = "Teléfono genérico" }
                        ]
                    },
                    Email = new ValidationRule.EmailProfile
                    {
                        AllowList = true,
                        Separator = ",",
                        Regex = @"^[\w-\.]+@([\w-]+\.)+[\w-]{2,}$"
                    },
                    Identification = new ValidationRule.IdentificationProfile
                    {
                        RequiredAlgorithm = "NONE",
                        AllowedTypes = ["CEDULA", "RUC"]
                    }
                }
            };

            return Task.FromResult(rule);
        }
    }
}
