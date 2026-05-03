using Microsoft.Extensions.DependencyInjection;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Core.DependencyInjection;

namespace Sunfish.Ingestion.Forms.DependencyInjection;

public static class FormsServiceCollectionExtensions
{
    /// <summary>
    /// Enables the forms-ingestion modality. Concrete pipeline bindings for specific
    /// model types are registered via <see cref="WithFormModel{TModel}"/>.
    /// <see cref="FormIngestionPipeline{TModel}"/> implements
    /// <see cref="IIngestionPipeline{TInput}"/> of <c>FormSubmission&lt;TModel&gt;</c>, not of
    /// the model type directly, so an open-generic registration cannot infer the envelope —
    /// per-model opt-in keeps the DI shape correct.
    /// </summary>
    public static IngestionBuilder WithForms(this IngestionBuilder builder) => builder;

    /// <summary>
    /// Registers <see cref="FormIngestionPipeline{TModel}"/> as the pipeline for
    /// <c>FormSubmission&lt;TModel&gt;</c>.
    /// </summary>
    public static IngestionBuilder WithFormModel<TModel>(this IngestionBuilder builder)
        where TModel : class
    {
        builder.Services.AddSingleton<IIngestionPipeline<FormSubmission<TModel>>, FormIngestionPipeline<TModel>>();
        return builder;
    }
}
