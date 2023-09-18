using System.Runtime.CompilerServices;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Wolverine.Http.FluentValidation.Internals;

public static class FluentValidationHttpExecutor
{
    #region sample_FluentValidationHttpExecutor_ExecuteOne

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task<IResult> ExecuteOne<T>(IValidator<T> validator, IProblemDetailSource<T> source, T message)
    {
        // First, validate the incoming request of type T
        var result = await validator.ValidateAsync(message);
            
        // If there are any errors, create a ProblemDetails result and return
        // that to write out the validation errors and otherwise stop processing
        if (result.Errors.Any())
        {
            var details = source.Create(message, result.Errors);
            return Results.Problem(details);
        }

        // Everything is good, full steam ahead!
        return WolverineContinue.Result();
    }

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task<IResult> ExecuteMany<T>(
        IReadOnlyList<IValidator<T>> validators,
        IProblemDetailSource<T> source,
        FluentValidationExecutionPolicy executionPolicy,
        T message)
    {
        global::FluentValidation.Results.ValidationResult[] validationFailures = null;

        if (executionPolicy == FluentValidationExecutionPolicy.ForceSequential)
        {
            var results = new List<global::FluentValidation.Results.ValidationResult>();
            foreach(var validator in validators)
            {
                results.Add(await validator.ValidateAsync(message));
            }
            validationFailures = results.ToArray();
        } else {
            var validationFailureTasks = validators
                .Select(validator => validator.ValidateAsync(message));

            validationFailures = await Task.WhenAll(validationFailureTasks);
        }

        var failures = validationFailures.SelectMany(validationResult => validationResult.Errors)
            .Where(validationFailure => validationFailure != null)
            .ToList();

        if (failures.Any())
        {
            var problems = source.Create(message, failures);
            return Results.Problem(problems);
        }

        return WolverineContinue.Result();
    }
}