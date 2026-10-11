# PR #5 review resolution

This integrates the saved commercial checkpoint and reviewed stacked PR histories
without rewriting their published commits. It preserves v0.0.1, existing Pricing
v1 PascalCase wire values, immutable commercial history and the disabled storage
default. Owner decisions are recorded in the focused Customers/Pricing owners and
`PR_5_RECOVERY_AND_CONSOLIDATED_PRICING_DECISION_PROPOSAL.md`.

| Review thread suffix | Disposition and permanent regression |
|---|---|
| K1MU | Expired unknown/default/archive staging is privately discovered, fenced and terminalized; accounting releases only after a verified marker. `UnacceptedArchiveAndDefaultStagingAreDiscoveredAndOnlyReleaseAllowanceAfterVerifiedRetirement`. |
| K1MY | Retain the tenant canonicalization gate shared by creation and import duplicate checks. `DuplicateAppearingAfterAcceptanceRejectsTheImplicitlyNewRowInsteadOfInsertingASecondOne` forces the concurrent interleaving in PostgreSQL. |
| K1Ma | Retain the accepted economic-family rule: conversion revision is compatibility evidence, and overlapping revisions require explicit same-family supersession. `ConversionRevisionChangeRequiresExplicitSupersessionWithinTheEconomicFamily` proves both the conflict and replacement path. Adding an independent overlap dimension would change that contract. |
| K1Mc | Retain owner-only CSV staging creation. The real-file `ImplicitIdentityRetryPublishesTheOriginalUploadAfterAnUnknownPut` regression checks Unix mode 0600 during upload. |
| K1Mg | Retain explicit refusal of redirected invoice debtors. `ConsolidatedAwayIndividualIsNotASelectableDebtor` proves issuance has no effect; the survivor remains selectable. Customers PostgreSQL regressions retain physical source/redirect facts. |
| K1Mj | Retain controlled business refusal of inactive/current-redirected customer selections. `InactiveOrRedirectedCanonicalCustomerIsABusinessRejectionNotAnInternalFault`. |
| K1Mm | Retain executor admission checks before protected manual batch parsing/claiming. `RunBatchRefusesWorkWhileTheExecutorDoesNotAdmitItBeforeParsingOrClaiming`. |
| K1Mq | Retain explicit v1 Pricing status/scope/publication mappings and PascalCase values. `PricingEndpointTests` preserves the existing contract; the independently owned Order DTO remains lowercase and omits precedence. |
| K1Mr | Retain response/deadline ownership until a body stream is transferred successfully. `FailedBodyOpeningDisposesTheHttpResponse`. |
| K1Mt | Retain rejection of exact dot/dot-dot key segments before URI construction. `ObjectKeysRejectSegmentsThatUriNormalizationWouldAlias`. |
| K6ih | Matching retries recover the original import ID/key and advance upload ownership. `ImplicitIdentityRetryPublishesTheOriginalUploadAfterAnUnknownPut`. |
| K6ik | Reservation keys are tenant/account/key scoped; leases prevent stale completion/failure/accounting. `UploadRetriesRecoverTheOriginalIdentityAndDifferentCallersCanUseTheSameKey` and `ExpiredUploadOwnershipCannotCompleteOrReleaseASuccessorsAllowance`. |
| K6in | Tenant-scoped migration backfill works under forced RLS without superuser/bypass authority. `DuplicateSignalsBackfillVisitsEveryTenantUnderARestrictedForcedRlsMigrator`. The new recovery migration separately preserves attributable legacy receipts and leaves ambiguity unowned. |
| K6ip | Current host authority precedes saved receipt reads; exact authorized create/publish retries precede current reference availability checks. `RetiredReferencesPreserveSavedRetriesAndHistoryButRefuseNewEffects` plus caller-scoped PostgreSQL receipt regressions. |
| K6iu | Historical reference checks retain tenant ownership and pinned compatibility while permitting retired catalog/customer history. The same host regression proves history remains readable and new effects remain refused. |
| K6iy | Exact evidence ranks ahead of broad fuzzy-prefix candidates before the SQL bound. `ExactDuplicateEvidenceIsNotCrowdedOutByANewerFuzzyPrefixAtLimitOne`. |
| K6i2 | Returned read streams own their deadline through body consumption and distinguish caller cancellation. `BodyReadsRetainTheDeadlineAndObserveIndependentCallerCancellation`. |

Publication failure rolls back rows, receipt, source link/state and accounting
in one real PostgreSQL transaction. Proven-absent retries reacquire quota. Cleanup
is an atomic conditional replacement/creation of a verified permanent empty
marker: stale cleanup and late conditional uploads cannot remove or replace a
newer marker. Provider scope changes fail closed. Monitoring has no mutation
authority; authorization outages and failing diagnostic subscribers cannot skip
recovery. Restricted runtime grants preserve immutable reservation provenance.

The Order schema fixtures include supported receipt versions 1–8 and preserve
required quantity arithmetic/rounding while omitting only the supported additive
legacy policy version. The Order commitment mutation gate explicitly selects its
project; its two targeted mutants were killed.

Receiving verification: the exact normal `./eng/verify.sh` gate is required before
merge. Focused evidence and the mutation report are retained under
`artifacts/pr5-review-20261011/`. Live Hugging Face provider evidence remains
unrun and blocked; these local regressions do not qualify a live service.
