using UCS.DebtorBatch.Api.DomainLike;
using UCS.DebtorBatch.Api.Infrastructure.Core;

namespace UCS.DebtorBatch.Api.Application.Import
{
    public static class BatchBuilder
    {
        public static CoreBatchImportRequest Build(Guid jobId, IReadOnlyList<DebtorRecord> records)
        {
            var items = new List<CoreBatchImportItem>(records.Count);

            foreach (var r in records)
            {
                var debtorType = "Person"; // si tu plantilla distingue, lo calculas aquí

                var fullName = string.Join(" ", new[] { r.FirstName, r.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

                var debtor = new CoreDebtor
                {
                    ExternalKey = r.ExternalKey,
                    DocumentNumber = r.ExternalKey,
                    Type = debtorType,
                    FirstName = r.FirstName,
                    LastName = r.LastName,
                    FullName = fullName,
                    Status = "Active",
                    PrimaryEmail = r.Email,
                    PrimaryPhone = r.PhoneNumber
                };

                // contacts consistentes
                if (!string.IsNullOrWhiteSpace(r.Email))
                    debtor.Contacts.Add(new CoreContact { Type = "Email", Value = r.Email!, IsPrimary = true });

                if (!string.IsNullOrWhiteSpace(r.PhoneNumber))
                    debtor.Contacts.Add(new CoreContact { Type = "Phone", Value = r.PhoneNumber!, IsPrimary = string.IsNullOrWhiteSpace(r.Email) });

                debtor.Debts.Add(new CoreDebt
                {
                    ExternalId = $"DEBT-{r.ExternalKey}-{r.RowIndex}",
                    Amount = r.Amount,
                    CurrencyCode = "USD",
                    DueDate = r.DueDate.ToDateTime(TimeOnly.MinValue),
                    OriginSystem = "Excel Import",
                    Status = "Pending",
                    Description = r.Concept
                });

                items.Add(new CoreBatchImportItem { Debtor = debtor });
            }

            return new CoreBatchImportRequest
            {
                BatchId = jobId.ToString(),
                Items = items
            };
        }
    }
}
