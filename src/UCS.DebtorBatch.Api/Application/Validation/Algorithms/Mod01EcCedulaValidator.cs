namespace UCS.DebtorBatch.Api.Application.Validation.Algorithms
{
    public static class Mod01EcCedulaValidator
    {
        public static bool IsValid(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var ced = new string(input.Where(char.IsDigit).ToArray());
            if (ced.Length != 10) return false;

            int province = int.Parse(ced[..2]);
            if (province < 1 || province > 24) return false;

            var digits = ced.Select(c => c - '0').ToArray();
            int sum = 0;
            for (int i = 0; i < 9; i++)
            {
                int d = digits[i];
                if (i % 2 == 0)
                {
                    d *= 2;
                    if (d > 9) d -= 9;
                }
                sum += d;
            }
            int verifier = (10 - (sum % 10)) % 10;
            return verifier == digits[9];
        }
    }
}
