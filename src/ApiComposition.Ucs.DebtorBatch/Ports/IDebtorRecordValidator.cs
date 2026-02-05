using ApiComposition.Ucs.DebtorBatch.Domain;

namespace ApiComposition.Ucs.DebtorBatch.Ports
{
    public interface IDebtorRecordValidator
    {
        IReadOnlyList<ValidationError> Validate(int rowNumber, DebtorRecord record);
    }
}
