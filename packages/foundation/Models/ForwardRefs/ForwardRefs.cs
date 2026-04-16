// ----------------------------------------------------------------------------------
// FORWARD REFERENCE STUBS — DO NOT USE IN PRODUCTION CODE
//
// These placeholder types exist only to allow Sunfish.Foundation to compile while
// their real implementations are being migrated in later tasks:
//
//   ScenarioStatus / AllocationSetType  → Task 7 (Sunfish.Foundation.BusinessLogic.Enums)
//
// When Task 7 is complete:
//   1. Delete this file.
//   2. Restore the commented-out `using Sunfish.Foundation.BusinessLogic.Enums;`
//      in AllocationSchedulerModels.cs.
// ----------------------------------------------------------------------------------

namespace Sunfish.Foundation.Models.ForwardRefs;

/// <summary>
/// Placeholder — real type arrives in Task 7 at Sunfish.Foundation.BusinessLogic.Enums.
/// Members must stay in parity with Marilo.Core.Enums.ScenarioStatus.
/// </summary>
public enum ScenarioStatus
{
    Draft,      // source: Marilo.Core.Enums.ScenarioStatus.Draft
    Shared,     // source: Marilo.Core.Enums.ScenarioStatus.Shared
    Approved,   // source: Marilo.Core.Enums.ScenarioStatus.Approved
    Promoted,   // source: Marilo.Core.Enums.ScenarioStatus.Promoted
    Rejected    // source: Marilo.Core.Enums.ScenarioStatus.Rejected
}

/// <summary>
/// Placeholder — real type arrives in Task 7 at Sunfish.Foundation.BusinessLogic.Enums.
/// Members must stay in parity with Marilo.Core.Enums.AllocationSetType.
/// </summary>
public enum AllocationSetType
{
    Baseline,   // source: Marilo.Core.Enums.AllocationSetType.Baseline
    Scenario    // source: Marilo.Core.Enums.AllocationSetType.Scenario
}

