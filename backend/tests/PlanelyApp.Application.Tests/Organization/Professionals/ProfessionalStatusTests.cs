using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Professionals.Activate;
using PlanelyApp.Application.Organization.Professionals.Deactivate;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Professionals;

public class ProfessionalStatusTests
{
    [Fact]
    public async Task Deactivating_should_turn_the_professional_off()
    {
        var professional = ProfessionalData.NewProfessional();
        var handler = new DeactivateProfessionalHandler(ProfessionalData.RepositoryWith(professional));

        var result = await handler.HandleAsync(
            new DeactivateProfessionalCommand(professional.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        professional.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Activating_should_turn_the_professional_back_on()
    {
        var professional = ProfessionalData.NewProfessional();
        professional.Deactivate();
        var handler = new ActivateProfessionalHandler(ProfessionalData.RepositoryWith(professional));

        var result = await handler.HandleAsync(
            new ActivateProfessionalCommand(professional.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        professional.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Deactivating_a_missing_professional_should_be_not_found()
    {
        var handler = new DeactivateProfessionalHandler(ProfessionalData.RepositoryWith(null));

        var result = await handler.HandleAsync(
            new DeactivateProfessionalCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Professional.NotFound");
    }

    [Fact]
    public async Task Activating_a_missing_professional_should_be_not_found()
    {
        var handler = new ActivateProfessionalHandler(ProfessionalData.RepositoryWith(null));

        var result = await handler.HandleAsync(
            new ActivateProfessionalCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }
}