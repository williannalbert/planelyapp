using FluentValidation;

namespace PlanelyApp.Application.Catalog.ServiceCategories.Rename;

internal sealed class RenameServiceCategoryValidator : AbstractValidator<RenameServiceCategoryCommand>
{
    public RenameServiceCategoryValidator()
    {
        RuleFor(command => command.ServiceCategoryId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty();
    }
}