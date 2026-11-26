namespace PlanelyApp.Api.Endpoints.Catalog.ServiceCategories;

/// <summary>The body of <c>PUT /service-categories/{serviceCategoryId}</c>. No id: the category is the one of the route.</summary>
internal sealed record ServiceCategoryBody(string Name);