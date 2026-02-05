using ApiComposition.Ucs.DebtorBatch.Domain;
using ApiComposition.Ucs.DebtorBatch.Ports;
using MiniExcelLibs;
using System.Globalization;

namespace ApiComposition.Ucs.DebtorBatch.Infrastructure
{
    public sealed class ExcelImportFileReader : IImportFileReader
    {
        public async IAsyncEnumerable<(int RowNumber, DebtorRecord Record)> ReadAsync(
            Stream fileStream,
            string fileName,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            // MiniExcel usa sync enumerables; lo envolvemos en async enumerable
            // Asume headers en primera fila.
            var rows = fileStream.Query(useHeaderRow: true);

            int rowNumber = 1; // header = 1
            foreach (IDictionary<string, object?> row in rows)
            {
                ct.ThrowIfCancellationRequested();
                rowNumber++;

                string Get(string key)
                {
                    if (!row.TryGetValue(key, out var v) || v is null) return "";
                    return v.ToString() ?? "";
                }

                // nombres de columnas esperadas
                var record = MapToDebtorRecord(
                    identification: Get("Identification"),
                    firstName: Get("FirstName"),
                    lastName: Get("LastName"),
                    email: NullIfEmpty(Get("Email")),
                    phone: NullIfEmpty(Get("Phone")),
                    debtAmountRaw: Get("DebtAmount"),
                    daysOverdueRaw: Get("DaysOverdue"),
                    debtorTypeRaw: Get("DebtorType")
                );

                yield return (rowNumber, record);

                // para que el compilador no reclame async
                await Task.Yield();
            }
        }

        private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

        private static DebtorRecord MapToDebtorRecord(
            string identification,
            string firstName,
            string lastName,
            string? email,
            string? phone,
            string debtAmountRaw,
            string daysOverdueRaw,
            string debtorTypeRaw)
        {
            _ = decimal.TryParse(debtAmountRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var debt);
            _ = int.TryParse(daysOverdueRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var days);
            _ = byte.TryParse(debtorTypeRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var type);

            return new DebtorRecord(
                Identification: identification,
                FirstName: firstName,
                LastName: lastName,
                Email: email,
                Phone: phone,
                DebtAmount: debt,
                DaysOverdue: days,
                DebtorType: type
            );
        }
    }
}
