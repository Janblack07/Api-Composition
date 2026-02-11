namespace UCS.DebtorBatch.Api.DomainLike
{
    public sealed class ValidationRule
    {
        public Guid TenantId { get; init; }

        public required ValidationProfile Validationprofile { get; init; }

        public sealed class ValidationProfile
        {
            public required PhoneProfile Phone { get; init; }
            public required EmailProfile Email { get; init; }
            public required IdentificationProfile Identification { get; init; }
        }

        public sealed class PhoneProfile
        {
            public bool AllowList { get; init; } = true;
            public string Separator { get; init; } = ",";
            public List<PhoneFormat> Formats { get; init; } = [];
        }

        public sealed class PhoneFormat
        {
            public required string Type { get; init; }
            public required string Regex { get; init; }
            public string? Description { get; init; }
        }

        public sealed class EmailProfile
        {
            public bool AllowList { get; init; } = true;
            public string Separator { get; init; } = ",";
            public required string Regex { get; init; }
        }

        public sealed class IdentificationProfile
        {
            public required string RequiredAlgorithm { get; init; } // MOD_01_EC, MOD_02_EC, NONE
            public List<string> AllowedTypes { get; init; } = [];
        }
    }
}
