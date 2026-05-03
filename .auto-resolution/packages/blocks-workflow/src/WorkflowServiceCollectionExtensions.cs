using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.Localization;

namespace Sunfish.Blocks.Workflow;

/// <summary>
/// Extension methods for registering workflow services in a dependency-injection container.
/// </summary>
public static class WorkflowServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="InMemoryWorkflowRuntime"/> as the
    /// <see cref="IWorkflowRuntime"/> implementation.
    /// State is in-process only — crashes lose all running instances.
    /// Suitable for testing, prototyping, and kitchen-sink demos.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddInMemoryWorkflow(this IServiceCollection services)
    {
        services.AddSingleton<IWorkflowRuntime, InMemoryWorkflowRuntime>();
        services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));
        return services;
    }
}
