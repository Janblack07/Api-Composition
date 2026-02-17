namespace UCS.DebtorBatch.Api.Options
{
    public sealed class ErrorPresentationOptions
    {
        public List<ErrorMapping> Mappings { get; set; } = [];
    }

    public sealed class ErrorMapping
    {

        public string Contains { get; set; } = "";

        public string? Field { get; set; }
        public string? Rule { get; set; }

        public string Friendly { get; set; } = "";
        public string Hint { get; set; } = "";

        public int Priority { get; set; } = 0;
    }
}