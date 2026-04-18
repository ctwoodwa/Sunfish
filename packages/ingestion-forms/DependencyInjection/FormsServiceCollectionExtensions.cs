using Microsoft.Extensions.DependencyInjection;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Core.DependencyInjection;

namespace Sunfish.Ingestion.Forms.DependencyInjection;

public static class FormsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FormIngestionPipeline{TModel}"/> as the open-generic
    /// <see cref="IIngestionPipeline{TInput}"/> implementation for any <c>FormSubmission&lt;TModel&gt;</c>.
    /// </summary>
    public static IngestionBuilder WithForms(this IngestionBuilder builder)
    {
        builder.Services.AddSingleton(typeof(IIngestionPipeline<>), typeof(FormIngestionPipeline<>));
        return builder;
    }
}
