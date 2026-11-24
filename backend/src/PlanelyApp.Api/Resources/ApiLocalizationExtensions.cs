using System.Globalization;
using System.Resources;
using Microsoft.AspNetCore.Localization;

[assembly: NeutralResourcesLanguage("pt-BR")]

namespace PlanelyApp.Api.Resources;

internal static class ApiLocalizationExtensions
{
    private static readonly CultureInfo[] Cultures =
        [new("pt-BR"), new("en"), new("es")];

    public static IServiceCollection AddApiLocalization(this IServiceCollection services)
    {
        services.AddSingleton<IErrorTexts, ResourceErrorTexts>();

        return services;
    }

    public static IApplicationBuilder UseApiLocalization(this IApplicationBuilder app)
    {
        var options = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture("pt-BR"),
            SupportedCultures = Cultures,
            SupportedUICultures = Cultures,
            FallBackToParentCultures = true,
            FallBackToParentUICultures = true,
            ApplyCurrentCultureToResponseHeaders = true,
        };

        // Only the header decides. A link with ?culture=en or a cookie cannot change the language of an answer.
        options.RequestCultureProviders.Clear();
        options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());

        return app.UseRequestLocalization(options);
    }
}