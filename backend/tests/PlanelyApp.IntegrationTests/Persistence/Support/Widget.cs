using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.IntegrationTests.Persistence.Support;

public enum WidgetStatus
{
    Draft,
    InReview,
    Published,
}

/// <summary>Throwaway aggregate root that has everything the conventions look for: tenant, enum, dates and a child.</summary>
public sealed class Widget : AggregateRoot, ITenantEntity
{
    private readonly List<WidgetPart> _parts = [];

    private Widget()
    {
    }

    public Widget(Guid companyId, string name, WidgetStatus status, DateTime scheduledAtUtc)
    {
        CompanyId = companyId;
        Name = name;
        Status = status;
        ScheduledAtUtc = scheduledAtUtc;
    }

    public Guid CompanyId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public WidgetStatus Status { get; private set; }

    public DateTime ScheduledAtUtc { get; private set; }

    public DateTime? ArchivedAtUtc { get; private set; }

    public IReadOnlyCollection<WidgetPart> Parts => _parts;

    public void Rename(string name) => Name = name;

    public void AddPart(string name) => _parts.Add(new WidgetPart(name));
}

public sealed class WidgetPart : Entity
{
    private WidgetPart()
    {
    }

    public WidgetPart(string name) => Name = name;

    public string Name { get; private set; } = string.Empty;
}