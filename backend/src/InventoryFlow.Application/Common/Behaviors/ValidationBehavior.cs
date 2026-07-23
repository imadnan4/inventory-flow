using FluentValidation;
using MediatR;

namespace InventoryFlow.Application.Common.Behaviors;

/// <summary>Runs FluentValidation before MediatR handlers.</summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(validators.Select(validator => validator.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors).Where(error => error is not null).ToList();
        if (failures.Count != 0) throw new ValidationException(failures);
        return await next(cancellationToken);
    }
}
