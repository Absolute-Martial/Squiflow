namespace Application.Pricing;

public static class PriceSelectionEngine
{
    public static PriceResolution Resolve(
        PriceSelectionRequest request,
        IReadOnlyList<PriceRevision> candidates,
        PricingOverridePolicy overridePolicy,
        PriceOverrideRequest? overrideRequest = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(overridePolicy);
        if (candidates.Count > 64)
            throw new ArgumentException("Candidate selection is bounded to 64 facts.", nameof(candidates));
        if (overridePolicy.PolicyRevision != request.PolicyRevision)
            throw new ArgumentException("The selection request and override policy revisions must match.", nameof(overridePolicy));
        if (candidates.Any(candidate => candidate.TenantId != request.TenantId))
            throw new ArgumentException("Price candidates must belong to the selection tenant.", nameof(candidates));

        var matching = candidates
            .Where(candidate =>
                candidate.TenantId == request.TenantId &&
                candidate.Key.ItemId == request.ItemId &&
                candidate.Key.UnitId == request.UnitId &&
                candidate.Key.UnitConversionRevision == request.Context.UnitConversionRevision &&
                candidate.Key.CurrencyCode == request.CurrencyCode &&
                candidate.State == PricePublicationState.Published &&
                request.Context.Matches(candidate.Key.Scope))
            .OrderBy(candidate => candidate.Key.Scope.Precedence)
            .ThenBy(candidate => candidate.RevisionId)
            .ToArray();

        var explanationCandidates = matching
            .Select(candidate => ToCandidate(candidate, null))
            .ToArray();
        var active = matching.Where(candidate => candidate.Validity.Contains(request.At)).ToArray();
        PriceRevision? selected = null;
        PriceResolution baseResolution;

        if (active.Length == 0)
        {
            var expired = matching.Any(candidate => candidate.Validity.HasExpiredAt(request.At));
            var explanation = Explain(
                request,
                explanationCandidates,
                selected,
                null,
                overridePolicy);
            baseResolution = expired
                ? new PriceExpired(explanation)
                : new PriceMissing(explanation);
        }
        else
        {
            var winningPrecedence = active.Min(candidate => candidate.Key.Scope.Precedence);
            var winning = active.Where(candidate => candidate.Key.Scope.Precedence == winningPrecedence).ToArray();
            if (winning.Length != 1)
            {
                var conflictExplanation = Explain(
                    request,
                    winning.Select(candidate => ToCandidate(candidate, null)).ToArray(),
                    selected,
                    null,
                    overridePolicy);
                baseResolution = new PriceConflict(conflictExplanation);
            }
            else
            {
                selected = winning[0];
                var resolvedExplanation = Explain(
                    request,
                    matching.Select(candidate => ToCandidate(candidate, candidate == selected ? candidate.BaseUnitPrice : null)).ToArray(),
                    selected,
                    null,
                    overridePolicy);
                baseResolution = new PriceResolved(resolvedExplanation, selected.BaseUnitPrice, selected);
            }
        }

        return overrideRequest is null ? baseResolution : ApplyOverride(baseResolution, overrideRequest);
    }

    public static bool RequiresElevatedOverride(PriceResolution baseResolution, decimal unitPrice) =>
        baseResolution is PriceResolved resolved &&
        !(resolved.Explanation.Policy ?? throw new InvalidOperationException("Selection policy evidence is required."))
            .Contains(unitPrice, resolved.BasePrice.BaseUnitPrice);

    public static PriceResolution ApplyOverride(PriceResolution baseResolution, PriceOverrideRequest overrideRequest)
    {
        ArgumentNullException.ThrowIfNull(baseResolution);
        ArgumentNullException.ThrowIfNull(overrideRequest);

        if (baseResolution is not PriceResolved resolved)
        {
            return new PriceOverrideRequired(
                baseResolution.Explanation,
                "A published, unambiguous base price is required before an override can be applied.");
        }
        if (resolved.AppliedOverride is not null)
            throw new ArgumentException("Apply an override only to an unmodified base selection.", nameof(baseResolution));

        var hasReason = !string.IsNullOrWhiteSpace(overrideRequest.Reason);
        var beyondPolicy = RequiresElevatedOverride(baseResolution, overrideRequest.UnitPrice);
        // Keep the authority check explicit: a client-provided price or reason never grants either capability.
        if (!hasReason ||
            !overrideRequest.CanOverride ||
            (beyondPolicy && !overrideRequest.CanOverrideBeyondPolicy))
        {
            return new PriceOverrideRequired(
                resolved.Explanation,
                beyondPolicy
                    ? "The override is outside policy and requires Pricing.OverrideBeyondPolicy and a reason."
                    : "Pricing.Override and a non-blank reason are required for a manual override.");
        }

        var evidence = new PriceOverrideEvidence(
            overrideRequest.UnitPrice,
            overrideRequest.Reason,
            beyondPolicy,
            overrideRequest.ApprovalReference,
            overrideRequest.ApprovedByAccountId);
        var finalExplanation = resolved.Explanation with
        {
            SelectedPrice = overrideRequest.UnitPrice,
            Override = evidence,
        };
        return new PriceResolved(finalExplanation, overrideRequest.UnitPrice, resolved.BasePrice, evidence);
    }

    private static PriceSelectionExplanation Explain(
        PriceSelectionRequest request,
        IReadOnlyList<PriceCandidate> candidates,
        PriceRevision? selected,
        PriceOverrideEvidence? overrideEvidence,
        PricingOverridePolicy policy) =>
        new(
            selected?.BaseUnitPrice,
            selected?.RevisionId,
            selected?.RevisionNumber,
            selected?.Key.Scope,
            Array.AsReadOnly(candidates.ToArray()),
            overrideEvidence,
            null,
            policy.PolicyRevision,
            request.At,
             request.Context,
             selected?.PriceId,
            selected is null ? "No unique current applicable published fact." : "Unique current fact at the highest applicable precedence.",
            policy);

    private static PriceCandidate ToCandidate(PriceRevision candidate, decimal? visiblePrice) =>
        new(
            candidate.RevisionId,
            candidate.RevisionNumber,
            candidate.Key.Scope,
            candidate.State,
            candidate.Validity,
            visiblePrice);
}
