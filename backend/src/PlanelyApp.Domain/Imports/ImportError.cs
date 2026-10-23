using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Imports;

/// <summary>A row of the spreadsheet that could not be imported, and why.</summary>
public sealed class ImportError : ValueObject
{
    public const int ReasonMaxLength = 500;

    private ImportError(int row, string reason)
    {
        Row = row;
        Reason = reason;
    }

    /// <summary>The line number as the user sees it in the spreadsheet, starting at 1.</summary>
    public int Row { get; }

    public string Reason { get; }

    public static ImportError Create(int row, string? reason)
    {
        if (row < 1)
        {
            throw new DomainException("ImportError.RowInvalid");
        }

        // The reason is written by the import job itself: a long one is cut, never rejected.
        return new ImportError(row, Guard.RequiredTextTruncated(reason, ReasonMaxLength, "ImportError.ReasonRequired"));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Row;
        yield return Reason;
    }
}