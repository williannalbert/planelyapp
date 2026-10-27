using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PlanelyApp.Infrastructure.Persistence.Converters;

/// <summary>Durations are stored in whole minutes (columns ending in _minutes), as the Domain only accepts whole minutes.</summary>
internal sealed class TimeSpanMinutesConverter() : ValueConverter<TimeSpan, int>(
    duration => (int)duration.TotalMinutes,
    minutes => TimeSpan.FromMinutes(minutes));

/// <summary>A list of ids stored as a JSON array, for lists that are always read and replaced together with their owner.</summary>
internal sealed class GuidListConverter() : ValueConverter<List<Guid>, string>(
    ids => JsonSerializer.Serialize(ids, (JsonSerializerOptions?)null),
    json => JsonSerializer.Deserialize<List<Guid>>(json, (JsonSerializerOptions?)null) ?? new List<Guid>());

/// <summary>
/// The Domain changes these lists in place (Clear, AddRange), so EF needs to compare them by content
/// and keep a copy of what it read to notice the change.
/// </summary>
internal sealed class ListComparer<T>() : ValueComparer<List<T>>(
    (left, right) => object.ReferenceEquals(left, right) || (left != null && right != null && left.SequenceEqual(right)),
    list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
    list => list.ToList())
    where T : notnull;

/// <summary>A set of weekdays as one number: bit 0 is Sunday, bit 6 is Saturday. Zero means no restriction.</summary>
internal static class WeekdayMask
{
    public static byte From(IEnumerable<DayOfWeek> days) =>
        (byte)days.Aggregate(0, (mask, day) => mask | (1 << (int)day));

    public static List<DayOfWeek> ToDays(byte mask) =>
        Enum.GetValues<DayOfWeek>().Where(day => (mask & (1 << (int)day)) != 0).Order().ToList();
}

internal sealed class WeekdaysConverter() : ValueConverter<List<DayOfWeek>, byte>(
    days => WeekdayMask.From(days),
    mask => WeekdayMask.ToDays(mask));