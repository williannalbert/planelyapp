using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.SharedKernel;

/// <summary>A language the platform supports for messages and notifications.</summary>
public sealed class Language : ValueObject
{
    public static readonly Language PortugueseBrazil = new("pt-BR");
    public static readonly Language English = new("en");
    public static readonly Language Spanish = new("es");

    private static readonly Language[] Supported = [PortugueseBrazil, English, Spanish];

    private Language(string code) => Code = code;

    public static Language Default => PortugueseBrazil;

    public string Code { get; }

    public static Language Create(string? code)
    {
        var language = Supported.FirstOrDefault(candidate =>
            string.Equals(candidate.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));

        return language ?? throw new DomainException("Language.NotSupported", code ?? string.Empty);
    }

    public override string ToString() => Code;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Code;
    }
}