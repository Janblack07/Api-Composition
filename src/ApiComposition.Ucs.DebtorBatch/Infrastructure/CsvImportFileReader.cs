using ApiComposition.Ucs.DebtorBatch.Domain;
using ApiComposition.Ucs.DebtorBatch.Ports;
using System.Globalization;

namespace ApiComposition.Ucs.DebtorBatch.Infrastructure
{
    public sealed class CsvImportFileReader : IImportFileReader
    {
        public async IAsyncEnumerable<(int RowNumber, DebtorRecord Record)> ReadAsync(
            Stream fileStream,
            string fileName,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            using var sr = new StreamReader(fileStream, leaveOpen: true);

            // Header
            var headerLine = await sr.ReadLineAsync();
            if (headerLine is null) yield break;

            var headers = SplitCsvLine(headerLine);

            int rowNumber = 1; // header = 1
            while (!sr.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();
                var line = await sr.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                rowNumber++;
                var cols = SplitCsvLine(line);

                string Get(string name)
                {
                    var idx = Array.FindIndex(headers, h => string.Equals(h.Trim(), name, StringComparison.OrdinalIgnoreCase));
                    if (idx < 0 || idx >= cols.Length) return "";
                    return cols[idx]?.Trim() ?? "";
                }

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

        // CSV split básico (soporta comillas)
        private static string[] SplitCsvLine(string line)
        {
            var result = new List<string>();
            var current = new List<char>();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == '"')
                {
                    // doble comilla escapada
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Add('"');
                        i++;
                        continue;
                    }

                    inQuotes = !inQuotes;
                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    result.Add(new string(current.ToArray()));
                    current.Clear();
                    continue;
                }

                current.Add(c);
            }

            result.Add(new string(current.ToArray()));
            return result.ToArray();
        }
    }
}
