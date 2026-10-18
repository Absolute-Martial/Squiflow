# Tenant authorization administration

**Product version:** `v0.0.1`

**Task owners:** `ADM-008`, `ADM-009`, `ADM-010`, `ADM-011`, `ADM-012`

## 1. Accepted Owner / Staff / delegation contract (ADM-008)

The previously accepted defaults remain:

- **Initial Owner default:** team/role administration only. The bootstrap `is_initial_owner` designation supplies role-administration authority but no implicit workspace/Customers/Orders business rights.
- **New Staff default:** read-only workspace, current Customers read surfaces and Orders read surfaces. Automatic materialization of that template is a separate concern and is not invented by this slice.

Exercise authority and delegation authority are different. For `v0.0.1`, the protected current **initial Owner is the only tenant role delegator**. Its delegation ceiling is the explicit `TenantPermissionCatalog`: only stable, compiled SquiFlow business permission IDs marked delegable may be proposed. The Owner may grant a catalog permission to an active member (including itself) even if the Owner does not currently exercise that business permission, because the accepted Owner default is administration rather than business inheritance.

The ceiling can never create:

- Platform Admin authority;
- `roles.manage` as a tenant-created business permission;
- arbitrary OpenFGA relation names/model edits;
- cross-tenant authority;
- DB/infrastructure/provider credentials;
- bypass of tenant membership or domain/resource state.

Multiple direct/custom-role grants use OpenFGA allow-union semantics. Removing one source does not imply effective denial if another valid source still grants the same operation. There is no explicit-deny role language in this slice.

The current catalog contains only implemented capability permissions. It does not accept unknown/unshipped permission IDs. Future feature-dependent permissions remain subject to the established rule: disabled capability makes the operation ineffective without deleting historical role/grant evidence; reactivation must re-evaluate effective authority deliberately.

## 2. Durable proposal state (ADM-009)

Tenancy owns the authorization-administration durable state. Hosts do not copy grant meaning.

Each mutation starts as an immutable semantic proposal with:

- proposal ID;
- tenant ID;
- requesting account ID;
- caller-scoped `Idempotency-Key` and semantic fingerprint;
- expected `TenantAuthorizationRevision`;
- bounded typed target/permission/role fields;
- `Pending | Applied | Failed | Uncertain` status;
- attempt count/failure code;
- retained transition events containing no provider credential/token.

The declared arbitration rule is **one Pending/Uncertain authorization proposal per tenant**. This intentionally serializes cross-system authority changes while keeping ordinary business requests concurrent. A second proposal at the same authorization revision conflicts until the active proposal reaches a terminal state. Reconciliation retries preserve the last provider failure code while incrementing the bounded attempt count, so an `Uncertain` outcome is not erased before convergence or terminal failure.

A fresh tenant's authorization revision is `1`. A proposal never advances it. Only locally recording a provider-observed effect advances it, exactly once. Duplicate direct grants and duplicate direct revokes are rejected as semantic conflicts before persistence, so a no-op provider relation cannot churn `TenantAuthorizationRevision`.

## 3. Provider reconciliation (ADM-010)

CoreApi adapts the durable proposal to the explicitly pinned tenant OpenFGA store/model.

Manual bounded reconciliation is selected for this release. There is no automatic background executor, so ADM-009–012 do **not** activate conditional `OPS-003`.

Reconciliation owns a PostgreSQL session advisory lock for the tenant, on one
borrowed connection. After acquisition it reads the proposal again; a terminal
proposal returns without a provider call. The same connection serves current
original-Owner checks, role assignment reads, dispatch evidence, provider outcome
recording and local completion. It never acquires a nested pool lease, including
at maximum pool size one. Acquisition (including pool wait) has a ten-second
budget and honours caller cancellation. Cleanup has a separate five-second budget;
failed/cancelled acquisition or uncertain unlock evicts the connection before disposal.
This requires direct/session-affine PostgreSQL; transaction-pooling remapping is
not qualified. It serializes healthy requests across replicas; it is not a
remote-provider fence.

The existing retained `failure_code` field also carries reserved `execution_*`
protocol outcomes; these are durable facts, not freely replaceable diagnostics:

| Nonterminal outcome | Meaning and permitted continuation |
|---|---|
| `execution_in_flight` | Committed before sending a provider mutation. A new session cannot prove that remote request stopped; retain the Uncertain slot, with no new mutation or completion. |
| `execution_observed` | The provider operation returned and the intended state was observed at higher consistency, durably recorded before local completion. A current original Owner may complete locally without repeating the write. |
| `execution_finished:<code>` | The mutation call returned successfully, but subsequent observation failed. No mutation remains in flight from that call; current original-Owner authority may repeat the same semantic attempt within the dispatch budget. |
| `execution_unresolved:<code>` | Transport/cancellation or another outcome lacks proof that all remote mutations finished. Hold the slot; do not dispatch, complete, or terminally fail automatically. |

Reconciliation has a separate DI-owned OpenFGA client that forces SDK `MaxRetry=0`
regardless of the shared Check retry configuration. The provider rejects an
ordinary SDK client; the owned client also disables HTTP redirects and bounds its
owned transport by the configured request timeout. A successful final response from a retrying writer
would not prove an earlier timed-out attempt drained. The shared authorization
Check client keeps its configured retry policy.

The provider uses a single atomic Write request (transaction splitting disabled,
no more than 100 tuples; the role/assignment ceilings fit that bound), explicit
store/model pin, duplicate-write/missing-delete tolerance and higher-consistency
observations. A fresh known rejection can become Failed only with proof that no
mutation applied: local pre-dispatch rejection, or an atomic Write validation/model
rejection without SDK retries and without an earlier possible effect. A rejection
on a later attempt cannot erase an earlier effect. Readback rejection after a
successful mutation is not proof of no effect. Validation or not-found rejection of
that readback retains the finished marker and permits bounded retry by the current
original Owner; it does not become unknown-lifetime quarantine or prove completion.

Dispatch increments the attempt counter, up to 20; status-only calls do not earn
provider evidence. Local completion is permitted only after the durable observed
outcome, rechecks the original Owner, and atomically advances the authorization
revision once. It preserves replay identity/events. Lower store methods cannot
replace retained execution outcomes with generic failures, terminalize earlier
unknown attempts, or complete an unobserved effect.

Loss of delegation before any attempt causes Failed with no provider mutation.
After a possible effect, loss of delegation retains Pending/Uncertain; it never
issues a new unauthorized grant. An observed effect can complete after the same
original Owner and tenant become active again. No recovery identity or break-glass
authority is added.

**Connection-loss and interruption limit:** a PostgreSQL advisory lock disappears
when its backend dies. It cannot fence a provider request already sent over HTTP.
The durable in-flight outcome therefore remains authoritative even after a new
session acquires the lock. A positive/negative provider read alone does not prove
that an old delayed write cannot apply later. Similarly, a write timeout with no
known response is quarantined, even if a test/operator later observes the tuple.
Crashes between remote completion and persistence of `execution_observed` can also
require this conservative hold. There is no automatic timeout expiry, guessed
completion, Worker, force-success route or new operational repair ceremony.
Draining/proving an unresolved external lifetime and any deliberate repair require
a separately accepted operational procedure; this implementation does not claim
general automatic recovery of that case.

Existing terminal records from an older writer are not silently rewritten or
presumed repaired; retained historical effects need integrator disposition before
activation.

Older Pending/Uncertain proposals with prior attempts but no new durable outcome
are treated conservatively as unknown; a status counter or historical timeout is
not upgraded to proof. Deployment must drain old reconciliation executors before
using this protocol and must not roll back to an old writer that ignores these
outcomes while such proposals remain. No mixed-old/new-writer guarantee is made.

A provider relation never bypasses tenant admission. Existing computed capability relations continue to intersect the raw permission relation with verified current `tenant#member` context.

## 4. Revocation and Owner handoff (ADM-011)

Revocation uses the same proposal protocol with a delete plus higher-consistency negative observation. Applied is not returned before the relation is known absent. Existing committed business history is unaffected by a later permission removal.

Membership suspension/removal is an independent denial layer: a remaining OpenFGA tuple cannot make an inactive membership effective.

### Owner handoff

Owner handoff is a separate local Tenancy transaction, not an arbitrary role tuple edit.

It requires:

- no `Pending` or `Uncertain` tenant authorization proposal; this prevents Owner transfer from racing an already-authorized provider reconciliation;
- ordinary current authenticated account/tenant membership resolution;
- current initial-Owner authorization from the database;
- ADM-034 recent/strong Tenant Web authentication evidence;
- expected tenant revision;
- active target membership;
- semantic `Idempotency-Key`.

The transaction locks the tenant/current Owner/target, moves exactly one `is_initial_owner` designation, increments both membership revisions, increments the tenant revision, increments `TenantAuthorizationRevision`, and stores an immutable replay receipt. The old Owner immediately loses role-administration authority. The new Owner becomes the protected recoverable administrator and therefore cannot be suspended/removed through the existing membership lifecycle until another guarded handoff succeeds.

## 5. One bounded custom-role lifecycle (ADM-012)

A custom role has tenant-scoped stable identity, plain-text bounded name, revision, active/retired state and 1..32 supported permission IDs. Current ceilings:

- maximum 50 active custom roles per tenant;
- maximum 32 permissions per role;
- maximum 50 active assignments per role;
- exactly one active authorization reconciliation per tenant.

Role identity is represented in OpenFGA as `role:{tenantN}_{roleN}`. The model has one stable `role#assignee` userset; tenant raw permission relations admit `role#assignee` as a subject. **No authorization model is deployed per custom role or tenant.**

Lifecycle:

- create: persist proposal, ensure role userset→tenant permission tuples, then create local active role at revision 1;
- revise: expected role revision, diff old/new permission tuples, then increment local role revision;
- assign/unassign: mutate one `user -> role#assignee` relationship and preserve assignment history/revision;
- retire: remove role→tenant permission tuples plus locally known active assignee tuples, then retire local role and mark active assignments removed while preserving history.

Role IDs from another tenant do not resolve. Unknown permission IDs fail before persistence. A retired role cannot be revised/assigned. Concurrent role revisions cannot both complete because the tenant proposal arbitration and expected role revision must both match.

## 6. HTTP surface

CoreApi owns the server-authoritative tenant surface under:

`/api/v1/tenants/{tenantId}/authorization`

Implemented operations include current state/catalog/roles, direct grant/revoke proposals, proposal status/manual reconcile, role create/revise/retire, one assignment/unassignment operation and guarded initial-Owner transfer.

Every route uses the established CoreApi tenant boundary and `AuthorizedTenantRoleAdministration` application requirement. That requirement checks the current active membership-derived `TenantContext` and current initial-Owner state. Reconciliation repeats the original delegator check immediately before provider mutation.

Payloads are bounded, use stable typed permission IDs and expose no arbitrary OpenFGA tuple/model editor.

## 7. Database privilege boundary

The CoreApi runtime role receives only the additional privileges needed by this capability:

- SELECT/INSERT and narrow UPDATE columns on the authorization-admin state tables;
- INSERT-only access to immutable authorization events (no runtime SELECT/UPDATE/DELETE);
- tenant `revision` update only;
- membership `revision` and `is_initial_owner` update only;
- no DELETE/TRUNCATE/REFERENCES/TRIGGER privileges on authorization-admin tables;
- no broad Tenancy table mutation or schema ownership.

The provisioning script independently rejects unsafe existing role attributes/membership/ownership and verifies the exact allowed UPDATE columns.

## 8. Evidence and non-claims

Regression guards cover host-neutral permission/intents, PostgreSQL idempotency/arbitration/revision/role/Owner semantics, restricted runtime grants, high-risk claim validation and real OpenFGA grant/revoke/custom-role/pinned-model behavior. The dedicated CoreApi `TenantAuthorizationReconciliationNativeTests` additionally joins real restricted-role PostgreSQL and pinned OpenFGA with two executors/pools 1 and 2, local-completion fault/restart, authority loss/reactivation, actual applied-write HTTP timeout, backend termination while a write is in flight, and cancelled lock-wait cleanup. Controlled-port `TenantAuthorizationReconcilerTests` test orchestration; they are not substitutes for those native runs. Their actual commands/exits and proof limits belong to the accompanying fix handoff; authoring these tests does not independently qualify this source.

Until those new tests execute in an environment with the locked NuGet graph, PostgreSQL 17 and OpenFGA, this source is **implemented / dynamic acceptance pending**. An earlier GATE-001 run does not qualify code introduced after that baseline.

This slice does not introduce automatic Worker reconciliation, tenant Web/Admin Web UI, branch/resource-scoped custom roles, explicit deny, provider identity lifecycle, default Staff tuple materialization, arbitrary permission definitions, or background role cleanup.

## 9. Requalification triggers

Requalify on execution-lock/connection lifetime, retained outcome-code meaning, provider write atomicity/retries, timeout/cancellation and quarantine/recovery policy, permission catalog IDs/relations, delegation ceiling, role/assignment ceilings, proposal arbitration, authorization revision semantics, OpenFGA model/store ID, consistency mode, Owner-transfer/step-up policy, membership semantics, runtime DB grants, or activation of automatic background reconciliation.
