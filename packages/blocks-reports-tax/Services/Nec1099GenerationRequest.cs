using Sunfish.Blocks.TaxReporting.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.TaxReporting.Services;

/// <summary>
/// Input model for generating a Form 1099-NEC report via
/// <see cref="ITaxReportingService.Generate1099NecAsync"/>.
/// </summary>
/// <param name="Year">The tax year this 1099-NEC covers.</param>
/// <param name="Recipients">
/// All potential recipients. The service filters out any rows with
/// <see cref="Nec1099Recipient.TotalPaid"/> below the $600 IRS threshold.
/// </param>
/// <param name="PropertyId">
/// Optional property scope. <see langword="null"/> for an aggregate payer report.
/// </param>
public sealed record Nec1099GenerationRequest(
    TaxYear Year,
    IReadOnlyList<Nec1099Recipient> Recipients,
    EntityId? PropertyId = null);
