using UCS.DebtorBatch.Api.DomainLike;

namespace UCS.DebtorBatch.Api.Application.Validation
{
    public sealed class DynamicValidatorFactory
    {
        public Func<DebtorRecord, IEnumerable<ValidationError>> Build(ValidationRule rules)
        {
            var emailRegex = new System.Text.RegularExpressions.Regex(rules.Validationprofile.Email.Regex, System.Text.RegularExpressions.RegexOptions.Compiled);
            var phoneRegexes = rules.Validationprofile.Phone.Formats
                .Select(f => new System.Text.RegularExpressions.Regex(f.Regex, System.Text.RegularExpressions.RegexOptions.Compiled))
                .ToList();

            var algo = rules.Validationprofile.Identification.RequiredAlgorithm;

            return record =>
            {
                var errors = new List<ValidationError>();

                // Required: ExternalKey + FirstName + Amount + DueDate
                if (string.IsNullOrWhiteSpace(record.ExternalKey))
                    errors.Add(new(record.RowIndex, "", "ExternalKey/Identificación is required"));

                if (string.IsNullOrWhiteSpace(record.FirstName))
                    errors.Add(new(record.RowIndex, record.ExternalKey, "FirstName/Nombres is required"));

                if (record.Amount <= 0)
                    errors.Add(new(record.RowIndex, record.ExternalKey, "Amount must be > 0"));

                if (record.DueDate == default)
                    errors.Add(new(record.RowIndex, record.ExternalKey, "DueDate is invalid or missing"));

                // Email (optional but validate if present)
                if (!string.IsNullOrWhiteSpace(record.Email) && !emailRegex.IsMatch(record.Email))
                    errors.Add(new(record.RowIndex, record.ExternalKey, "Invalid email format"));

                // Phone (optional but validate if present)
                if (!string.IsNullOrWhiteSpace(record.PhoneNumber))
                {
                    var ok = phoneRegexes.Any(r => r.IsMatch(record.PhoneNumber));
                    if (!ok)
                        errors.Add(new(record.RowIndex, record.ExternalKey, "Invalid phone format"));
                }

                // Identification algorithm
                if (!string.IsNullOrWhiteSpace(record.ExternalKey))
                {
                    var id = record.ExternalKey;
                    var valid = algo switch
                    {
                        "MOD_01_EC" => Algorithms.Mod01EcCedulaValidator.IsValid(id),
                        "MOD_02_EC" => Algorithms.Mod02EcRucValidator.IsValid(id),
                        "NONE" => id.All(char.IsLetterOrDigit) && id.Length >= 6,
                        _ => false
                    };

                    if (!valid)
                        errors.Add(new(record.RowIndex, record.ExternalKey, $"Invalid identification for algorithm {algo}"));
                }

                return errors;
            };
        }
    }
}
