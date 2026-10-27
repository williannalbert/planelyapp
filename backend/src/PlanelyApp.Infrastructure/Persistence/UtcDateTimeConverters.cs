using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PlanelyApp.Infrastructure.Persistence.Conventions;

internal static class UtcDateTimeConverters
{
    // Writing refuses anything that is not UTC. Reading marks the value as UTC,
    // because the provider returns Kind=Unspecified.
    public static readonly ValueConverter<DateTime, DateTime> Required = new(
        value => EnsureUtc(value),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static readonly ValueConverter<DateTime?, DateTime?> Optional = new(
        value => value.HasValue ? EnsureUtc(value.Value) : value,
        value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);

    // A method, because an expression tree cannot contain a throw.
    internal static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : throw new NonUtcDateTimeException(value);
}