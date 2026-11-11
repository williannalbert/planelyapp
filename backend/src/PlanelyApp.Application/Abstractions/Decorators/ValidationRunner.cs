using FluentValidation;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Validation;

namespace PlanelyApp.Application.Abstractions.Decorators;

/// <summary>Runs every validator of a request and turns what they found into a <see cref="ValidationError"/>.</summary>
internal static class ValidationRunner
{
    public static async Task<ValidationError?> ValidateAsync<TRequest>(
        IEnumerable<IValidator<TRequest>> validators,
        TRequest request,
        CancellationToken cancellationToken)
    {
        List<FieldError>? failures = null;

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken);

            foreach (var failure in result.Errors)
            {
                failures ??= [];
                failures.Add(new FieldError(failure.PropertyName, ToCode(failure.ErrorCode)));
            }
        }

        return failures is null ? null : new ValidationError(failures);
    }

    // FluentValidation names the code after the validator ("NotEmptyValidator"). Those become our stable codes;
    // a code set with WithErrorCode does not end in "Validator" and is kept as it is.
    private static string ToCode(string? errorCode)
    {
        if (string.IsNullOrEmpty(errorCode))
        {
            return ValidationCodes.Invalid;
        }

        return errorCode switch
        {
            "NotNullValidator" or "NotEmptyValidator" => ValidationCodes.Required,
            "MaximumLengthValidator" => ValidationCodes.TooLong,
            "GreaterThanValidator" or "GreaterThanOrEqualValidator" or "LessThanValidator"
                or "LessThanOrEqualValidator" or "InclusiveBetweenValidator" or "ExclusiveBetweenValidator"
                => ValidationCodes.OutOfRange,
            _ when errorCode.EndsWith("Validator", StringComparison.Ordinal) => ValidationCodes.Invalid,
            _ => errorCode,
        };
    }
}