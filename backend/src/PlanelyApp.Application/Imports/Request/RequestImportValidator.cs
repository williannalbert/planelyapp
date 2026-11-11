using FluentValidation;
using PlanelyApp.Application.Abstractions.Validation;

namespace PlanelyApp.Application.Imports.Request;

internal sealed class RequestImportValidator : AbstractValidator<RequestImportCommand>
{
    public RequestImportValidator()
    {
        RuleFor(command => command.Type).IsInEnum().WithErrorCode(ValidationCodes.Invalid);

        // The length of the name is the Domain's (DataImport.FileNameInvalid).
        RuleFor(command => command.FileName).NotEmpty();
        RuleFor(command => command.FileName)
            .Must(name => name!.Trim().EndsWith(ImportRules.SpreadsheetExtension, StringComparison.OrdinalIgnoreCase))
            .WithErrorCode(ValidationCodes.Invalid)
            .When(command => !string.IsNullOrWhiteSpace(command.FileName));

        RuleFor(command => command.Content).NotNull();

        // The size has to be known before anything is saved, and an upload is buffered by the Api, so it can seek.
        RuleFor(command => command.Content)
            .Must(content => content!.CanRead && content.CanSeek)
            .WithErrorCode(ValidationCodes.Invalid)
            .When(command => command.Content is not null);

        RuleFor(command => command.Content)
            .Must(content => content!.Length > 0)
            .WithErrorCode(ValidationCodes.Required)
            .When(command => command.Content is { CanSeek: true });

        RuleFor(command => command.Content)
            .Must(content => content!.Length <= ImportRules.MaxFileBytes)
            .WithErrorCode(ValidationCodes.TooLong)
            .When(command => command.Content is { CanSeek: true });
    }
}