namespace Sunfish.Ingestion.Forms;

/// <summary>
/// A form submission captured by a <c>FormBlock&lt;TModel&gt;</c>. The model holds the
/// user's entered values; the block's state holder records submission lifecycle.
/// </summary>
public sealed record FormSubmission<TModel>(
    TModel Model,
    string SchemaId,
    DateTime SubmittedAtUtc) where TModel : class;
