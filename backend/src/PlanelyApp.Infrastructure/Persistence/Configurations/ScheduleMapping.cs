using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Infrastructure.Persistence.Configurations;

internal static class ScheduleMapping
{
    /// <summary>
    /// Maps a weekday schedule: its day, its range and its breaks (own table, technical key).
    /// Used for the business hours of a branch and for the working hours of a professional.
    /// </summary>
    public static void MapSchedule<TOwner>(
        OwnedNavigationBuilder<TOwner, DailySchedule> schedule,
        string breakTable,
        string breakForeignKey)
        where TOwner : class
    {
        schedule.Property(s => s.Day).HasColumnName("day");
        schedule.OwnsOne(s => s.Range, range => MapRange(range));
        schedule.Navigation(s => s.Range).IsRequired();

        schedule.OwnsMany(s => s.Breaks, pause =>
        {
            pause.ToTable(breakTable, table =>
                table.HasCheckConstraint($"ck_{breakTable}_range", "end_time > start_time"));
            pause.WithOwner().HasForeignKey(breakForeignKey);

            pause.Property<int>("Id").ValueGeneratedOnAdd();
            pause.HasKey("Id");

            pause.Property(b => b.Description).HasMaxLength(100);
            pause.OwnsOne(b => b.Range, range => MapRange(range));
            pause.Navigation(b => b.Range).IsRequired();
        });

        schedule.Navigation(s => s.Breaks).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void MapRange<TOwner>(OwnedNavigationBuilder<TOwner, TimeOfDayRange> range)
        where TOwner : class
    {
        range.Property(r => r.Start).HasColumnName("start_time");
        range.Property(r => r.End).HasColumnName("end_time");
    }
}