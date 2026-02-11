using System.Globalization;
using MiniExcelLibs;
using UCS.DebtorBatch.Api.Application.Abstractions;
using UCS.DebtorBatch.Api.DomainLike;

namespace UCS.DebtorBatch.Api.Infrastructure.Excel;

public sealed class MiniExcelAdapter : IExcelParser
{
    // Nombres esperados de columnas (exactos como tu plantilla)
    private const string ColId = "Identificación";
    private const string ColFirst = "Nombres";
    private const string ColLast = "Apellidos";
    private const string ColEmail = "Email";
    private const string ColPhone = "Teléfono";
    private const string ColAmount = "Monto Deuda";
    private const string ColDue = "Fecha Vencimiento";
    private const string ColConcept = "Concepto";

    public async IAsyncEnumerable<DebtorRecord> ParseAsync(Stream fileStream, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        // MiniExcel devuelve rows como IDictionary<string, object?>
        // useHeaderRow:true => la primera fila son headers
        var rows = MiniExcel.Query(fileStream, useHeaderRow: true);

        int rowIndex = 1; // header = 1, primera data row = 2

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            rowIndex++;

            // MiniExcel usa keys tal como vienen en el header
            var dict = (IDictionary<string, object?>)row;

            var rec = new DebtorRecord
            {
                RowIndex = rowIndex,
                ExternalKey = GetString(dict, ColId),
                FirstName = GetString(dict, ColFirst),
                LastName = GetString(dict, ColLast),
                Email = GetString(dict, ColEmail),
                PhoneNumber = GetString(dict, ColPhone),
                Amount = GetDecimal(dict, ColAmount),
                DueDate = GetDate(dict, ColDue),
                Concept = GetString(dict, ColConcept)
            };

            yield return rec;
        }

        await Task.CompletedTask;
    }

    private static string? GetString(IDictionary<string, object?> row, string key)
    {
        var v = GetValue(row, key);
        if (v is null) return null;
        var s = v.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static decimal GetDecimal(IDictionary<string, object?> row, string key)
    {
        var v = GetValue(row, key);
        if (v is null) return 0m;

        if (v is decimal d) return d;
        if (v is double db) return (decimal)db;
        if (v is float f) return (decimal)f;
        if (v is int i) return i;
        if (v is long l) return l;

        var s = v.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(s)) return 0m;

        // soporta coma/punto
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var inv))
            return inv;
        if (decimal.TryParse(s, NumberStyles.Any, new CultureInfo("es-EC"), out var es))
            return es;

        return 0m;
    }

    private static DateOnly GetDate(IDictionary<string, object?> row, string key)
    {
        var v = GetValue(row, key);
        if (v is null) return default;

        // Caso 1: ya viene DateTime
        if (v is DateTime dt)
            return DateOnly.FromDateTime(dt);

        // Caso 2: serial de Excel (OADate)
        if (v is double oa)
            return DateOnly.FromDateTime(DateTime.FromOADate(oa));
        if (v is int oai)
            return DateOnly.FromDateTime(DateTime.FromOADate(oai));
        if (v is long oal)
            return DateOnly.FromDateTime(DateTime.FromOADate(oal));

        // Caso 3: texto
        var s = v.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(s)) return default;

        var formats = new[]
        {
        "yyyy-MM-dd",
        "yyyy/MM/dd",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "MM/dd/yyyy",
        "M/d/yyyy",
        "dd-MM-yyyy",
        "d-M-yyyy",
        "yyyyMMdd"
    };

        if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var exact))
            return DateOnly.FromDateTime(exact);

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var anyInv))
            return DateOnly.FromDateTime(anyInv);

        if (DateTime.TryParse(s, new CultureInfo("es-EC"), DateTimeStyles.AssumeLocal, out var anyEs))
            return DateOnly.FromDateTime(anyEs);

        return default;
    }

    private static object? GetValue(IDictionary<string, object?> row, string key)
    {
        // match exact
        if (row.TryGetValue(key, out var v)) return v;

        // match por trim/case (por si el excel trae espacios)
        var found = row.FirstOrDefault(k => string.Equals(k.Key?.Trim(), key, StringComparison.OrdinalIgnoreCase));
        return found.Value;
    }
}