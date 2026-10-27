using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.IntegrationTests.Catalog;

/// <summary>Valid catalog objects, unique on every call because the database is shared by all tests.</summary>
internal static class CatalogData
{
    public static string NewName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    public static ServiceCategory NewCategory(Guid companyId, string? name = null) =>
        ServiceCategory.Create(companyId, name ?? NewName("Category"));

    public static ServiceOffering NewFixedService(
        Guid companyId,
        decimal price = 50m,
        int minutes = 30,
        string? name = null,
        Guid? categoryId = null) =>
        ServiceOffering.CreateWithFixedPrice(
            companyId,
            name ?? NewName("Service"),
            TimeSpan.FromMinutes(minutes),
            Money.From(price),
            categoryId: categoryId);

    public static ServiceOffering NewOnRequestService(Guid companyId, string? name = null) =>
        ServiceOffering.CreateOnRequest(companyId, name ?? NewName("Quote"), TimeSpan.FromMinutes(60));
}