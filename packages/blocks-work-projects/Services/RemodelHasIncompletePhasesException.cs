using Sunfish.Blocks.WorkProjects.Models;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Thrown when <see cref="IRemodelProjectService.CapitalizeAsync"/>
/// is invoked while one or more phases is still in
/// <see cref="PhaseStatus.Planned"/> or <see cref="PhaseStatus.Active"/>.
/// </summary>
public sealed class RemodelHasIncompletePhasesException : InvalidOperationException
{
    public RemodelProjectId RemodelProjectId { get; }

    public RemodelHasIncompletePhasesException(RemodelProjectId remodelProjectId)
        : base($"Cannot capitalize RemodelProject {remodelProjectId.Value} while phases are still planned or active.")
    {
        RemodelProjectId = remodelProjectId;
    }
}
