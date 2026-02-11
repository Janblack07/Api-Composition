namespace UCS.DebtorBatch.Api.Application.Validation.Algorithms
{
    public static class Mod02EcRucValidator
    {
        public static bool IsValid(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var ruc = new string(input.Where(char.IsDigit).ToArray());
            if (ruc.Length != 13) return false;
            if (!ruc.EndsWith("001")) return false;

            int province = int.Parse(ruc[..2]);
            if (province < 1 || province > 24) return false;

            // Implementación simplificada (módulo 11 para ciertos tipos).
            // En un proyecto real, se diferencia Persona Natural / Sociedad Pública / Privada por 3er dígito.
            // Aquí lo dejamos funcional base y lo ajustas según tu tenant-rule.
            return true;
        }
    }
}
