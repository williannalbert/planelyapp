using FluentValidation;

namespace PlanelyApp.Application.Catalog.ServiceCategories.Create;

internal sealed class CreateServiceCategoryValidator : AbstractValidator<CreateServiceCategoryCommand>
{
    public CreateServiceCategoryValidator()
    {
        // The length is the Domain's (ServiceCategory.NameInvalid).
        RuleFor(command => command.Name).NotEmpty();
    }
}