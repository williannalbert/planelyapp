using NSubstitute;
using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Tests.Catalog;

internal static class CatalogData
{
    public static ServiceCategory NewCategory(Guid? companyId = null, string name = "Hair") =>
        ServiceCategory.Create(companyId ?? Guid.NewGuid(), name);

    public static ServiceOffering NewFixedService(
        Guid? companyId = null,
        string name = "Haircut",
        decimal price = 50m,
        int minutes = 30) =>
        ServiceOffering.CreateWithFixedPrice(
            companyId ?? Guid.NewGuid(), name, TimeSpan.FromMinutes(minutes), Money.From(price));

    public static ServiceOffering NewOnRequestService(Guid? companyId = null, string name = "Custom tattoo") =>
        ServiceOffering.CreateOnRequest(companyId ?? Guid.NewGuid(), name, TimeSpan.FromMinutes(120));

    /// <summary>A repository that finds this service (or none) whatever the id.</summary>
    public static IServiceOfferingRepository ServicesWith(ServiceOffering? service)
    {
        var services = Substitute.For<IServiceOfferingRepository>();
        services.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(service);

        return services;
    }

    /// <summary>A repository that finds this category (or none) whatever the id.</summary>
    public static IServiceCategoryRepository CategoriesWith(ServiceCategory? category)
    {
        var categories = Substitute.For<IServiceCategoryRepository>();
        categories.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(category);

        return categories;
    }
}