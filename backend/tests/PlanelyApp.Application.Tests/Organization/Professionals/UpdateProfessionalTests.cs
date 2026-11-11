using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Professionals.UpdateDetails;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Professionals;

public class UpdateProfessionalTests
{
    [Fact]
    public async Task The_details_should_be_updated()
    {
        var professional = ProfessionalData.NewProfessional();
        var handler = new UpdateProfessionalDetailsHandler(ProfessionalData.RepositoryWith(professional));

        var result = await handler.HandleAsync(
            new UpdateProfessionalDetailsCommand(professional.Id, "Ana Lima", null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        professional.Name.ShouldBe("Ana Lima");
        professional.Specialty.ShouldBeNull();
    }

    [Fact]
    public async Task A_missing_professional_should_be_not_found()
    {
        var handler = new UpdateProfessionalDetailsHandler(ProfessionalData.RepositoryWith(null));

        var result = await handler.HandleAsync(
            new UpdateProfessionalDetailsCommand(Guid.NewGuid(), "Ana Lima", null),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Professional.NotFound");
    }

    [Fact]
    public async Task A_name_above_the_limit_should_be_refused_and_leave_the_professional_untouched()
    {
        var professional = ProfessionalData.NewProfessional();
        var handler = new UpdateProfessionalDetailsHandler(ProfessionalData.RepositoryWith(professional));

        var exception = await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(
            new UpdateProfessionalDetailsCommand(professional.Id, new string('x', 101), "Colorista"),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Professional.NameInvalid");
        professional.Name.ShouldBe("Ana Souza");
        professional.Specialty.ShouldBe("Cabeleireira");
    }

    [Fact]
    public async Task An_empty_name_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new UpdateProfessionalDetailsValidator(),
            new UpdateProfessionalDetailsCommand(Guid.NewGuid(), "", null));

        problems.ShouldBe(["Name:Validation.Required"], ignoreOrder: true);
    }
}