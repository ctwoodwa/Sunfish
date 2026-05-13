using Microsoft.CodeAnalysis;

namespace Sunfish.Foundation.ShipsOffice.Analyzers;

internal static class Diagnostics
{
    public const string PermissionCheckMissingId = "SUNFISH_SHIPSOFFICE_PERM001";

    public static readonly DiagnosticDescriptor PermissionCheckMissing = new(
        id: PermissionCheckMissingId,
        title: "Ship's Office data-provider call lacks preceding permission check",
        messageFormat: "Call to '{0}' is not preceded by IPermissionResolver.AuthorizeAsync(ShipAction.ViewShipsOffice). Per ADR 0083 §2 caller-contract: callers MUST verify ViewShipsOffice before calling the data provider.",
        category: "SunfishShipsOffice",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Per ADR 0083 §2 + the IShipsOfficeDataProvider XML doc caller-contract: any call site invoking GetSnapshotAsync or SearchAsync on IShipsOfficeDataProvider must be preceded by a verifiable IPermissionResolver.AuthorizeAsync(ShipAction.ViewShipsOffice) call in the same method or constructor. Enforcement is enforced by analyzer SUNFISH_SHIPSOFFICE_PERM001 (Phase 2d).",
        helpLinkUri: "https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0083-ships-office-content-aggregation.md");
}
