using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.SharedKernel;

/// <summary>A pause inside a working day (e.g. lunch) during which nobody is served.</summary>
public sealed class Break : ValueObject
{
    private const int DescriptionMaxLength = 100;

    // For EF Core only: it creates the instance and fills the properties afterwards.
    #pragma warning disable CS8618
    private Break()
    {
    }
    #pragma warning restore CS8618
    private Break(TimeOfDayRange range, string? description)
    {
        Range = range;
        Description = description;
    }

    public TimeOfDayRange Range { get; }

    public string? Description { get; }

    public static Break Create(TimeOfDayRange range, string? description = null) =>
        new(range, Guard.OptionalText(description, DescriptionMaxLength, "Break.DescriptionInvalid"));

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Range;
        yield return Description;
    }
}