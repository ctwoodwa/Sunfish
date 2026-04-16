// ----------------------------------------------------------------------------------
// FORWARD REFERENCE STUBS — DO NOT USE IN PRODUCTION CODE
//
// These placeholder types exist only to allow Sunfish.Foundation to compile while
// their real implementations are being migrated in later tasks:
//
//   ScenarioStatus / AllocationSetType  → Task 7 (Sunfish.Foundation.BusinessLogic.Enums)
//   SunfishTheme                        → Task 5 (Sunfish.Foundation.Configuration)
//
// When the real types are in place:
//   1. Delete this file.
//   2. Restore the commented-out `using Sunfish.Foundation.BusinessLogic.Enums;`
//      in AllocationSchedulerModels.cs.
//   3. Restore the `using Sunfish.Foundation.Configuration;` in ThemeChangedEventArgs.cs.
// ----------------------------------------------------------------------------------

namespace Sunfish.Foundation.Models.ForwardRefs;

/// <summary>Placeholder — real type arrives in Task 7 at Sunfish.Foundation.BusinessLogic.Enums.</summary>
public enum ScenarioStatus
{
    Draft,
    Shared,
    Approved,
    Promoted,
    Rejected
}

/// <summary>Placeholder — real type arrives in Task 7 at Sunfish.Foundation.BusinessLogic.Enums.</summary>
public enum AllocationSetType
{
    Baseline,
    Scenario
}

/// <summary>Placeholder — real type arrives in Task 5 at Sunfish.Foundation.Configuration.</summary>
public record SunfishTheme;
