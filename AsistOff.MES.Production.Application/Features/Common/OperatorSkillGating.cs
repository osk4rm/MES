using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;

namespace AsistOff.MES.Production.Application.Features.Common;

/// <summary>
/// Operator skill gating shared by confirmation validation and the
/// dispatch / shift-queue read models (issue #397).
///
/// Recipe operations declare skill needs as free text:
/// <see cref="ResourceRequirement.RequiredCapability"/> stores the skill
/// dictionary display string (<c>"CODE — Name"</c>, em dash) chosen in the
/// recipe editor, while <see cref="ResourceRequirement.RequiredRole"/> is a
/// free-text role that doubles as a skill-code candidate. Only candidates
/// that resolve to a real <c>config.Skills</c> row are enforced; legacy
/// free-text capabilities that match no skill code are ignored so historic
/// orders keep confirming.
/// </summary>
internal static class OperatorSkillGating
{
    private const string DisplaySeparator = " — ";

    /// <summary>
    /// Collects the distinct required skill-code candidates from resource
    /// requirements: the code prefix of <c>RequiredCapability</c> (part
    /// before <c>" — "</c>, or the whole trimmed value when there is no
    /// separator) plus the trimmed <c>RequiredRole</c> value.
    /// </summary>
    internal static IReadOnlyCollection<string> ExtractRequiredSkillCodes(
        IEnumerable<ResourceRequirement> requirements)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var requirement in requirements)
        {
            var capabilityCode = ExtractCapabilityCode(requirement.RequiredCapability);
            if (capabilityCode is not null)
                codes.Add(capabilityCode);

            if (!string.IsNullOrWhiteSpace(requirement.RequiredRole))
                codes.Add(requirement.RequiredRole.Trim());
        }

        return codes;
    }

    internal static string? ExtractCapabilityCode(string? requiredCapability)
    {
        if (string.IsNullOrWhiteSpace(requiredCapability))
            return null;

        var trimmed = requiredCapability.Trim();
        var separator = trimmed.IndexOf(DisplaySeparator, StringComparison.Ordinal);
        var code = separator < 0 ? trimmed : trimmed[..separator].Trim();

        return string.IsNullOrEmpty(code) ? null : code;
    }

    /// <summary>
    /// Required codes no operator in <paramref name="heldByOperator"/> holds.
    /// An operator is qualified for an order only when they hold every
    /// required code. Empty when nothing is required.
    /// </summary>
    internal static IReadOnlyCollection<string> FindMissingCodes(
        IReadOnlyCollection<string> requiredCodes,
        IReadOnlyCollection<string> heldCodes)
    {
        if (requiredCodes.Count == 0)
            return [];

        var held = new HashSet<string>(heldCodes, StringComparer.OrdinalIgnoreCase);
        return requiredCodes.Where(code => !held.Contains(code)).ToList();
    }

    /// <summary>
    /// True when at least one of the given operators holds every required
    /// code (vacuously true when nothing is required).
    /// </summary>
    internal static bool HasQualifiedOperator(
        IReadOnlyCollection<string> requiredCodes,
        IEnumerable<IReadOnlyCollection<string>> heldByOperator)
    {
        if (requiredCodes.Count == 0)
            return true;

        return heldByOperator.Any(held =>
            requiredCodes.All(code => held.Contains(code, StringComparer.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Required (skill-row-backed) skill codes per recipe version, for the
    /// dispatch / shift-queue read models. Versions without skill-backed
    /// requirements map to an empty set and are never flagged.
    /// </summary>
    internal static async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<string>>> LoadRequiredSkillsByVersionAsync(
        IOperationNodesRepository operationNodesRepository,
        ISkillsRepository skillsRepository,
        IReadOnlyCollection<Guid> recipeVersionIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, IReadOnlyCollection<string>>();
        if (recipeVersionIds.Count == 0)
            return result;

        var operations = await operationNodesRepository.ListForVersionsAsync(
            recipeVersionIds, cancellationToken);

        var candidatesByVersion = operations
            .GroupBy(x => x.RecipeVersionId)
            .ToDictionary(
                g => g.Key,
                g => ExtractRequiredSkillCodes(g.SelectMany(x => x.ResourceRequirements)));

        var allCandidates = candidatesByVersion.Values
            .SelectMany(x => x)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var knownCodes = (await skillsRepository.ListByCodesAsync(allCandidates, cancellationToken))
            .Select(x => x.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var versionId in recipeVersionIds.Distinct())
        {
            if (!candidatesByVersion.TryGetValue(versionId, out var candidates))
                candidates = [];
            result[versionId] = candidates.Where(knownCodes.Contains).ToList();
        }

        return result;
    }
}
