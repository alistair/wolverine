using Wolverine.Http.FluentValidation.Internals;

namespace Wolverine.Http.FluentValidation;

public enum FluentValidationExecutionPolicy {
    Default,
    ForceSequential,
}

public static class WolverineHttpOptionsExtensions
{
    #region sample_usage_of_http_add_policy

    /// <summary>
    ///     Apply Fluent Validation middleware to all Wolverine HTTP endpoints with a known Fluent Validation
    ///     validator for the request type
    /// </summary>
    /// <param name="httpOptions"></param>
    public static void UseFluentValidationProblemDetailMiddleware(this WolverineHttpOptions httpOptions, FluentValidationExecutionPolicy validationExecutionPolicy = FluentValidationExecutionPolicy.Default)
    {
        httpOptions.AddPolicy<HttpChainFluentValidationPolicy>(new HttpChainFluentValidationPolicy(validationExecutionPolicy));
    }

    #endregion
}