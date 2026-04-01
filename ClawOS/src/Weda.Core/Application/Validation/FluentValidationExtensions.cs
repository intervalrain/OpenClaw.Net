using ErrorOr;

using FluentValidation;

namespace Weda.Core.Application.Validation;

public static class FluentValidationExtensions
{
    public static IRuleBuilderOptions<T, TProperty> WithError<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> rule,
        Error error)
    {
        return rule
            .WithErrorCode(error.Code)
            .WithMessage(error.Description);
    }

    public static IRuleBuilderOptions<T, TProperty> WithError<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> rule,
        Func<T, Error> errorFactory)
    {
        return rule
            .WithErrorCode(errorFactory(default!).Code)
            .WithMessage((instance, _) => errorFactory(instance).Description);
    }
}