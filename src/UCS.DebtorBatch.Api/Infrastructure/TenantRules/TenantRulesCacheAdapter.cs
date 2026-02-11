using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using UCS.DebtorBatch.Api.Application.Abstractions;
using UCS.DebtorBatch.Api.DomainLike;
using UCS.DebtorBatch.Api.Options;

namespace UCS.DebtorBatch.Api.Infrastructure.TenantRules
{
    public sealed class TenantRulesCacheAdapter(IMemoryCache cache, IOptions<ImportOptions> opt) : IValidationRuleProvider
    {
        private readonly ImportOptions _o = opt.Value;

        public Task<ValidationRule> GetRulesAsync(Guid tenantId, CancellationToken ct)
        {
            var key = $"tenant-rules:{tenantId:N}";
            if (cache.TryGetValue(key, out ValidationRule? rules) && rules is not null)
                return Task.FromResult(rules);

            // MOCK rules (luego lo cambias a Enterprise Core)
            rules = new ValidationRule
            {
                TenantId = tenantId,
                Validationprofile = new ValidationRule.ValidationProfile
                {
                    Email = new ValidationRule.EmailProfile
                    {
                        Regex = "^[\\w-\\.]+@([\\w-]+\\.)+[\\w-]{2,4}$"
                    },
                    Phone = new ValidationRule.PhoneProfile
                    {
                        Formats =
                        [
                            new ValidationRule.PhoneFormat{ Type="Mobile", Regex="^09\\d{8}$", Description="Celular EC" },
                        new ValidationRule.PhoneFormat{ Type="Intl", Regex="^\\d{10,15}$", Description="Genérico Intl" }
                        ]
                    },
                    Identification = new ValidationRule.IdentificationProfile
                    {
                        RequiredAlgorithm = "MOD_01_EC",
                        AllowedTypes = ["CEDULA", "RUC"]
                    }
                }
            };

            cache.Set(key, rules, TimeSpan.FromDays(_o.ValidationCacheTTLDays));
            return Task.FromResult(rules);
        }
    }
}
