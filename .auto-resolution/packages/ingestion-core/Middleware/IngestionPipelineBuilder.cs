namespace Sunfish.Ingestion.Core.Middleware;

/// <summary>
/// Fluent builder for composing an ordered chain of <see cref="IIngestionMiddleware{TInput}"/>
/// instances around a terminal <see cref="IngestionDelegate{TInput}"/>.
/// </summary>
/// <typeparam name="TInput">The modality-specific input type.</typeparam>
public sealed class IngestionPipelineBuilder<TInput>
{
    private readonly List<IIngestionMiddleware<TInput>> _middlewares = new();

    /// <summary>
    /// Appends a middleware to the chain. Middlewares execute in registration order: the first
    /// <c>Use</c> call wraps the outermost layer.
    /// </summary>
    public IngestionPipelineBuilder<TInput> Use(IIngestionMiddleware<TInput> middleware)
    {
        _middlewares.Add(middleware);
        return this;
    }

    /// <summary>
    /// Produces an <see cref="IngestionDelegate{TInput}"/> that invokes the registered
    /// middlewares in order and finally calls <paramref name="terminal"/>. Each call returns a
    /// fresh composed delegate — callers may <c>Build</c> multiple times independently.
    /// </summary>
    public IngestionDelegate<TInput> Build(IngestionDelegate<TInput> terminal)
    {
        IngestionDelegate<TInput> current = terminal;
        for (int i = _middlewares.Count - 1; i >= 0; i--)
        {
            var mw = _middlewares[i];
            var next = current;
            current = (input, ctx, ct) => mw.InvokeAsync(input, ctx, next, ct);
        }
        return current;
    }
}
