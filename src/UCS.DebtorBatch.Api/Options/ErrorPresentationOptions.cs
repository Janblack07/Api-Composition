namespace UCS.DebtorBatch.Api.Options
{
    public sealed class ErrorPresentationOptions
    {
        public List<ErrorMapping> Mappings { get; set; } = [];
    }

    public sealed class ErrorMapping
    {
        public string Contains { get; set; } = "";     // match simple
        public string Friendly { get; set; } = "";     // mensaje para usuario
        public string Hint { get; set; } = "";         // sugerencia
    }
}
