using ApiComposition.Ucs.DebtorBatch.Domain;
using ApiComposition.Ucs.DebtorBatch.Ports;
using System.Text.RegularExpressions;

namespace ApiComposition.Ucs.DebtorBatch.Infrastructure
{
    public sealed class BasicDebtorRecordValidator : IDebtorRecordValidator
    {
        private static readonly Regex EmailRx = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        public IReadOnlyList<ValidationError> Validate(int rowNumber, DebtorRecord r)
        {
            var errors = new List<ValidationError>();

            if (string.IsNullOrWhiteSpace(r.Identification))
                errors.Add(new(rowNumber, "Identification", "Identification is required."));

            if (string.IsNullOrWhiteSpace(r.FirstName))
                errors.Add(new(rowNumber, "FirstName", "FirstName is required."));

            if (string.IsNullOrWhiteSpace(r.LastName))
                errors.Add(new(rowNumber, "LastName", "LastName is required."));

            if (!string.IsNullOrWhiteSpace(r.Email) && !EmailRx.IsMatch(r.Email))
                errors.Add(new(rowNumber, "Email", "Invalid email format."));

            if (!string.IsNullOrWhiteSpace(r.Phone))
            {
                var digits = new string(r.Phone.Where(char.IsDigit).ToArray());
                if (digits.Length < 7)
                    errors.Add(new(rowNumber, "Phone", "Phone must have at least 7 digits."));
            }

            if (r.DebtAmount <= 0)
                errors.Add(new(rowNumber, "DebtAmount", "DebtAmount must be > 0."));

            if (r.DaysOverdue < 0)
                errors.Add(new(rowNumber, "DaysOverdue", "DaysOverdue must be >= 0."));

            return errors;
        }
    }
}
