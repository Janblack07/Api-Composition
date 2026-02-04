using System.Text.Json;

namespace ApiComposition.Ucs.DebtorBatch.Json
{
    public sealed class UpperCaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name) => name.ToUpperInvariant();
    }
}
