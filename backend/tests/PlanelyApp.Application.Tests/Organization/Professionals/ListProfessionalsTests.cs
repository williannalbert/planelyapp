using NSubstitute;
using PlanelyApp.Application.Organization.Professionals;
using PlanelyApp.Application.Organization.Professionals.List;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Professionals;

public class ListProfessionalsTests
{
    [Fact]
    public async Task What_the_read_model_lists_should_come_back_as_it_is()
    {
        IReadOnlyList<ProfessionalSummaryDto> summaries = [new(Guid.NewGuid(), "Ana Souza", "Cabeleireira", true)];
        var readModel = Substitute.For<IProfessionalReadModel>();
        readModel.ListAsync(null, Arg.Any<CancellationToken>()).Returns(summaries);

        var result = await new ListProfessionalsHandler(readModel)
            .HandleAsync(new ListProfessionalsQuery(), TestContext.Current.CancellationToken);

        result.Value.ShouldBe(summaries, ignoreOrder: false);
    }

    [Fact]
    public async Task The_active_filter_should_reach_the_read_model()
    {
        var readModel = Substitute.For<IProfessionalReadModel>();

        await new ListProfessionalsHandler(readModel)
            .HandleAsync(new ListProfessionalsQuery(IsActive: false), TestContext.Current.CancellationToken);

        await readModel.Received().ListAsync(false, Arg.Any<CancellationToken>());
    }
}