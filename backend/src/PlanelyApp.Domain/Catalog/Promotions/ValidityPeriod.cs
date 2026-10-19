using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Catalog.Promotions;

/// <summary>
/// The calendar days a promotion is in force. Both ends are inclusive:
/// a promotion "until November 30" still applies on the 30th.
/// </summary>
public sealed class ValidityPeriod : ValueObject
{
    private ValidityPeriod(DateOnly start, DateOnly end)
    {
        Start = start;
        End = end;
    }

    public DateOnly Start { get; }

    public DateOnly End { get; }

    public static ValidityPeriod Create(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new DomainException("ValidityPeriod.EndBeforeStart");
        }

        return new ValidityPeriod(start, end);
    }

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}