namespace PlanelyApp.Application.Abstractions.Validation;

internal static class OptionalValueExtensions
{
    /// <summary>
    /// A text that was not sent or is blank means "no value". Anything else goes through the factory of the Domain
    /// value object, which throws if it does not accept it (the validators say so before, as a 400).
    /// </summary>
    public static T? OrNull<T>(this string? text, Func<string?, T> factory)
        where T : class =>
        string.IsNullOrWhiteSpace(text) ? null : factory(text);
}