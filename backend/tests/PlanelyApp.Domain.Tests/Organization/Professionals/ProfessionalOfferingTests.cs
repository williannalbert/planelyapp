using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Organization.Professionals;

public class ProfessionalOfferingTests
{
    private static readonly Guid ServiceId = Guid.CreateVersion7();

    [Fact]
    public void Should_keep_service_and_custom_duration()
    {
        var offering = ProfessionalOffering.Create(ServiceId, TimeSpan.FromMinutes(45));

        offering.ServiceOfferingId.ShouldBe(ServiceId);
        offering.CustomDuration.ShouldBe(TimeSpan.FromMinutes(45));
    }

    [Fact]
    public void Custom_duration_should_be_optional()
    {
        ProfessionalOffering.Create(ServiceId).CustomDuration.ShouldBeNull();
    }

    [Fact]
    public void Empty_service_id_should_be_rejected()
    {
        Should.Throw<DomainException>(() => ProfessionalOffering.Create(Guid.Empty))
            .Code.ShouldBe("ProfessionalOffering.ServiceRequired");
    }

    [Fact]
    public void Invalid_durations_should_be_rejected()
    {
        Should.Throw<DomainException>(() => ProfessionalOffering.Create(ServiceId, TimeSpan.Zero))
            .Code.ShouldBe("ProfessionalOffering.DurationInvalid");
        Should.Throw<DomainException>(() => ProfessionalOffering.Create(ServiceId, TimeSpan.FromMinutes(-30)))
            .Code.ShouldBe("ProfessionalOffering.DurationInvalid");
        Should.Throw<DomainException>(() => ProfessionalOffering.Create(ServiceId, TimeSpan.FromSeconds(90)))
            .Code.ShouldBe("ProfessionalOffering.DurationInvalid");
        Should.Throw<DomainException>(() => ProfessionalOffering.Create(ServiceId, TimeSpan.FromHours(24).Add(TimeSpan.FromMinutes(1))))
            .Code.ShouldBe("ProfessionalOffering.DurationInvalid");
    }

    [Fact]
    public void Twenty_four_hours_should_be_accepted()
    {
        Should.NotThrow(() => ProfessionalOffering.Create(ServiceId, TimeSpan.FromHours(24)));
    }

    [Fact]
    public void ResolveDuration_should_prefer_the_custom_duration()
    {
        var standard = TimeSpan.FromMinutes(60);

        ProfessionalOffering.Create(ServiceId, TimeSpan.FromMinutes(45)).ResolveDuration(standard)
            .ShouldBe(TimeSpan.FromMinutes(45));
        ProfessionalOffering.Create(ServiceId).ResolveDuration(standard)
            .ShouldBe(standard);
    }

    [Fact]
    public void Offerings_with_same_service_and_duration_should_be_equal()
    {
        ProfessionalOffering.Create(ServiceId, TimeSpan.FromMinutes(45))
            .ShouldBe(ProfessionalOffering.Create(ServiceId, TimeSpan.FromMinutes(45)));
        ProfessionalOffering.Create(ServiceId, TimeSpan.FromMinutes(45))
            .ShouldNotBe(ProfessionalOffering.Create(ServiceId));
    }
}