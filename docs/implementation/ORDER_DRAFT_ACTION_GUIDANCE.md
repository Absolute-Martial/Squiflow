# Current order-draft action guidance

Owner: `Application.Orders` for lifecycle guidance; CoreApi for the current
membership/permission adaptation and HTTP contract. Product version stays v0.0.1.
The bounded current-draft guidance is `PRODUCTION_HONEST`; `BLOCKED = none`.
Broader customer/program workflow policy, acceptance, stored price/override policy,
fulfillment and financial operations remain `NOT_INTRODUCED`.

## Declared responsibility

An operator reviewing a priced draft needs to know which implemented change they
can attempt and why another is unavailable. The guide reuses
`OrderDraftLifecycle.AssessRevise` and `AssessAbandon`; it does not maintain a
second state-transition table. Its host-neutral result includes the observed
revision and exactly two actions, `revise` and `abandon`, each with availability
and a stable unavailability code. Lifecycle prohibition takes priority over a
missing permission: an abandoned draft cannot be revised or abandoned again.
Unsupported persisted states or nonpositive revisions fail safely rather than
being treated as a usable draft.

`GET /api/v1/tenants/{tenantId}/orders/{orderId}/actions` requires current active
account binding, current tenant membership and `can_view_orders` before reading
an order or probing its write permissions. An editor/abandoner without view
permission cannot use this read route; the existing command permission contracts
are independently enforced. A missing or foreign order returns the same safe 404. The response
contains `orderId`, `observedRevision` and `actions`; each action has `action`,
`available` and nullable `unavailabilityCode` (`permission_required` or
`order_already_abandoned`). It exposes no tuple, token, receipt, customer payload
or provider diagnostics. Public OpenAPI documents the protected read and failures.

For lifecycle-eligible actions the host independently checks current
`can_edit_order` plus `can_apply_manual_price` for revision, and
`can_abandon_order` for abandonment using the existing pinned-model,
higher-consistency adapter. It does not infer them from create/view permission,
JWT roles, feature visibility or earlier guidance. Abandoned drafts need neither
write or pricing lookup because the lifecycle already prohibits both changes.
Any required permission-provider outage returns safe 503 and no partial guide;
an outage is not represented as a definitive permission denial. Every response
is no-store through the classified central middleware. Existing global admission,
cooperative request budgets, provider deadlines and SQL timeouts apply.

## Guidance is an observation, not a grant

The order is read through the existing tenant-scoped, repeatable-read detail
operation. No new SQL, table, receipt, schema, grant, cache or background work is
introduced. The response is bounded to two actions and reuses that read's resource
bounds. This avoids another persistence path solely to save loading the current
bounded priced snapshot. A measured need may earn a lighter query later.

Permissions and order state are not one distributed atomic snapshot. Concurrent
changes can make guidance stale even before the client receives it. The reported
revision is explicitly `observedRevision`, not a promise that the order or grants
remain unchanged. Commands still require a fresh permission decision, their
`expectedRevision` and a semantic Idempotency-Key. Guidance is not submitted as
proof, persisted as an approval or trusted by command execution. Re-read after a
conflict/authority change; do not blindly retry with a new key or silently replace
an operator's intended expected revision.

The two actions express the currently implemented business lifecycle, not a
hardcoded customer workflow. No customer name, GUID, category or billing choice
selects behavior. Custom stages, required supplementary information, approvals,
assignment/escalation and published customer/program policies remain absent and
must join this guidance through their owning qualified capability. The route
must not present acceptance, quotation, invoice or payment actions before those
operations exist. The permission control for manual price entry is owned by
`PRICING_COMPONENT_BOUNDARY.md`; reasons, ceilings, approvals and adaptive
price-source policy remain outside this guide.

## Source admission and permanent evidence

The current application-owned lifecycle is the behavior source. The host reuses
the already admitted native ASP.NET resource authorization and current OpenFGA
adapter; no workflow engine, UI action registry, script evaluator, new package or
provider-neutral authorization framework is needed. Existing PostgreSQL detail,
RLS and mutation integration tests remain the provider regression guards.

| Claim | Falsifiable recurring evidence under `./eng/verify.sh` |
|---|---|
| Lifecycle/permission composition and fail-closed invalid state/revision | `OrderDraftActionGuideTests` |
| Separate edit/abandon authority, bounded shape, no mutation | `DraftGuidanceUsesDistinctCurrentPermissionsWithoutChangingTheOrder` |
| Current membership/view authority before storage/write probes | `MembershipAndViewPermissionAreRequiredBeforeReadingOrProbingWritePermissions`; classified anonymous-route guard |
| Required provider outage produces no partial guide | `PermissionProviderOutageReturnsNoPartialGuide` |
| Revocation remains authoritative at execution | `GuidanceDoesNotGrantCommandAuthorityAndIsRecomputedAfterRevocation` |
| A stale revision cannot bypass concurrency; terminal actions need no write probes | `StaleGuidanceCannotSkipRevisionChecksAndAbandonedOrdersNeedNoWritePermissionProbe` |
| Tenant-scoped existence and no foreign write probes | `ForeignAndMissingOrdersHaveTheSameNotFoundResultWithoutWritePermissionProbes`; existing actual PostgreSQL isolation/detail tests |
| Stable protected OpenAPI surface | `OpenApiDescribesProtectedNonMutatingGuidanceAndSafeFailures` |

Host tests use controlled account/membership/permission/store seams to exercise
HTTP decisions. They do not prove real OIDC deployment, distributed revocation
atomicity or database behavior; existing real-provider tests own those declared
adapter guarantees. Requalify on lifecycle/status changes, permission relations,
new actions or policy authority, snapshot/persistence changes, cache or batching,
provider/framework changes or a second host consuming the guide. This slice does
not qualify the complete commercial backend or production deployment.

The final 2026-10-02 normal parallel `./eng/verify.sh` passed all **450 tests**
across twelve test projects, locked restore, format verification and Release build,
with zero failures/skips and zero build warnings/errors. Guidance adds ten pure
lifecycle/permission cases and fourteen HTTP cases, including manual-pricing
outage and terminal-probe behavior. Existing real PostgreSQL/OpenFGA tests also
passed. This qualifies the declared guidance only, not customer-specific workflow,
remote CI or deployment.
