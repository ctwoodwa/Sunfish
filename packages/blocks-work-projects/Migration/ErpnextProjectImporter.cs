using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Blocks.WorkProjects.Services;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Migration;

/// <summary>
/// Default <see cref="IErpnextProjectImporter"/>. Looks up existing
/// projects by external-ref tag and compares embedded modified-key
/// tags to decide <see cref="ImportOutcomeKind.Skipped"/> vs
/// <see cref="ImportOutcomeKind.Updated"/>.
/// </summary>
public sealed class ErpnextProjectImporter : IErpnextProjectImporter
{
    private const string ExternalRefPrefix = "externalRef:erpnext:";
    private const string ModifiedKeyPrefix = "erpnextModified:";

    private readonly IProjectService _projectService;
    private readonly InMemoryProjectRepository _projects;
    private readonly Func<Instant> _now;

    public ErpnextProjectImporter(
        IProjectService projectService,
        InMemoryProjectRepository projects,
        Func<Instant>? now = null)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _projects       = projects       ?? throw new ArgumentNullException(nameof(projects));
        _now            = now ?? (() => Instant.Now);
    }

    /// <inheritdoc />
    public async Task<ImportOutcome<Project>> UpsertFromErpnextAsync(
        ErpnextProjectSource source,
        TenantId tenantId,
        Guid ownerPartyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrWhiteSpace(source.Name))
            return new ImportOutcome<Project>(ImportOutcomeKind.Failed, null, "ERPNext Project name is empty.");

        var externalRefTag = ExternalRefPrefix + source.Name;
        var existing = FindExistingByExternalRef(tenantId, externalRefTag);

        if (existing is not null)
        {
            var currentModified = existing.Tags.FirstOrDefault(t => t.StartsWith(ModifiedKeyPrefix, StringComparison.Ordinal));
            var incoming = ModifiedKeyPrefix + source.Modified;
            if (string.Equals(currentModified, incoming, StringComparison.Ordinal))
                return new ImportOutcome<Project>(ImportOutcomeKind.Skipped, existing, "ERPNext Modified key unchanged.");

            // v1 update-path: refresh planned dates + modified-key tag.
            existing.UpdatePlannedDates(source.ExpectedStartDate, source.ExpectedEndDate, ownerPartyId, _now());
            ReplaceTag(existing, ModifiedKeyPrefix, incoming);
            _projects.Upsert(existing);
            return new ImportOutcome<Project>(ImportOutcomeKind.Updated, existing, null);
        }

        var kind = MapKind(source.ProjectType);
        var created = await _projectService.CreateAsync(
            tenantId:          tenantId,
            name:              string.IsNullOrWhiteSpace(source.ProjectName) ? source.Name : source.ProjectName,
            kind:              kind,
            priority:          Priority.Normal,
            ownerPartyId:      ownerPartyId,
            createdBy:         ownerPartyId,
            description:       null,
            propertyId:        null,
            customerPartyId:   null,
            parentProjectId:   null,
            plannedStartDate:  source.ExpectedStartDate,
            plannedEndDate:    source.ExpectedEndDate,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        AddTags(created, externalRefTag, ModifiedKeyPrefix + source.Modified);
        _projects.Upsert(created);

        return new ImportOutcome<Project>(ImportOutcomeKind.Inserted, created, null);
    }

    private Project? FindExistingByExternalRef(TenantId tenantId, string externalRefTag)
        => _projects.ListByTenant(tenantId)
            .FirstOrDefault(p => p.Tags.Contains(externalRefTag, StringComparer.Ordinal));

    private static ProjectKind MapKind(string? erpnextProjectType)
        // H3: promote to Remodel only when source carries project_type = "Remodel".
        => string.Equals(erpnextProjectType, "Remodel", StringComparison.OrdinalIgnoreCase)
            ? ProjectKind.Remodel
            : ProjectKind.Generic;

    private static void AddTags(Project project, params string[] newTags)
    {
        var merged = project.Tags.ToList();
        foreach (var t in newTags)
            if (!merged.Contains(t, StringComparer.Ordinal)) merged.Add(t);
        project.OverwriteTags(merged);
    }

    private static void ReplaceTag(Project project, string prefix, string newValue)
    {
        var merged = project.Tags.Where(t => !t.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        merged.Add(newValue);
        project.OverwriteTags(merged);
    }
}
