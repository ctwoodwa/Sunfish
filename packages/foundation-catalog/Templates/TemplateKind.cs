namespace Sunfish.Foundation.Catalog.Templates;

/// <summary>
/// Classifies a template so renderers, validators, and admin UIs can
/// dispatch to the right handler. More kinds may be added; keep stable values.
/// </summary>
public enum TemplateKind
{
    /// <summary>Data-entry form; rendered by a form adapter.</summary>
    Form = 0,

    /// <summary>Due-diligence checklist with requests, evidence, and approvals.</summary>
    DiligenceChecklist = 1,

    /// <summary>Report definition consumed by the reporting pipeline.</summary>
    Report = 2,

    /// <summary>Notification template (email, SMS, in-app).</summary>
    Notification = 3,

    /// <summary>Document template (letters, contracts, generated PDFs).</summary>
    Document = 4,
}
