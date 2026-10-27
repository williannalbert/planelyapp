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
/// The Domain changes the list in place (Clear, AddRange), so EF needs to compare it by content
/// and keep a copy of what it read to notice the change.
/// </summary>
internal sealed class GuidListComparer() : ValueComparer<List<Guid>>(
    (left, right) => object.ReferenceEquals(left, right) || (left != null && right != null && left.SequenceEqual(right)),
    ids => ids.Aggregate(0, (hash, id) => HashCode.Combine(hash, id)),
    ids => ids.ToList());