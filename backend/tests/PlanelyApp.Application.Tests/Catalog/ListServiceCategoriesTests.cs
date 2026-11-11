using NSubstitute;
using PlanelyApp.Application.Catalog.ServiceCategories;
using PlanelyApp.Application.Catalog.ServiceCategories.List;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class ListServiceCategoriesTests
{
    [Fact]
    public async Task What_the_read_model_lists_should_come_back_as_it_is()
    {
        IReadOnlyList<ServiceCategoryDto> categories =
            [new(Guid.NewGuid(), "Beard"), new(Guid.NewGuid(), "Hair")];
        var readModel = Substitute.For<IServiceCategoryReadModel>();
        readModel.ListAsync(Arg.Any<CancellationToken>()).Returns(categories);

        var result = await new ListServiceCategoriesHandler(readModel)
            .HandleAsync(new ListServiceCategoriesQuery(), TestContext.Current.CancellationToken);

        result.Value.ShouldBe(categories);
    }
}