# SquiFlow Current Implementation Truth

**Product version:** `v0.0.1`, locked until the complete production-capable product gate

**Repository name:** internal development codename; public runtime identity is configuration-owned

**Current state:** backend/API baseline plus the bounded commercial continuation implemented; complete product qualification remains separate

## Current inventory

The three count lines below are machine-read by `Application.Architecture.Tests` (`ReadCount` matches `^label: N$`). Keep each on its own line in exactly this form.

```text
production projects: 24
test projects:       22
executable hosts:    4
solution files:      1
repository build/test contract: present
BLOCKED: COM-004 external Hugging Face provider qualification (OPS-007/OPS-008), with local raw-source contracts qualified; existing ADM-004 live ZITADEL topology evidence, ADM-006/ADM-009–ADM-012 gate-owner qualification and OPS-017 quality-gate expansion remain separate pending claims
```

## What exists

| Area | Projects | Current responsibility (declared narrow scope) |
|---|---|---|
| Object storage | `Application.ObjectStorage` | Host-neutral `IObjectStore` request/result boundary for bounded retained objects; no provider SDK/API types or provider qualification claim. |
| ApplicationProfiles | `Application.Profiles` | Bounded feature definition, dependency-graph validation and deterministic, dependency-closed effective-selection compilation with stable catalog/selection fingerprints. No production feature catalog and no durable tenant profile authority. |
| Branding | `Application.Branding` | Validated, deployment-supplied public identity (name, legal identity, theme, links). No codename fallback. |
| IdentityAccess | `Application.IdentityAccess`, `.Postgres` | Durable binding of one or more exact OIDC `(issuer, subject)` identities to a stable application account, including caller-scoped onboarding/link receipts and concurrency-safe identity uniqueness. Stores no password, role or permission authority. |
| Tenancy | `Application.Tenancy`, `.Postgres` | Tenant registry, revision-checked membership/tenant lifecycle, membership-derived immutable `TenantContext`, durable tenant-authorization revision/proposals/evidence, bounded direct business grants, custom-role metadata/assignments, and atomic initial-Owner handoff. Capability permission semantics remain a stable compiled catalog; provider-specific OpenFGA mutation stays in the CoreApi adapter. |
| PlatformAdministration | `Application.PlatformAdministration`, `.Postgres` | One-time initial Platform Admin bootstrap authority plus request-time active principal/Admin-device resolution and retained AdminApi access audit. Ongoing operator, device and role lifecycle remains absent. |
| Customers | `Application.Customers`, `.Postgres` | Tenant-owned customer organizations/programs and individual billing records with contact/availability edits, representative relationships, duplicate review/keep-separate/consolidation redirects, and bounded customer-import plans/work results. Commands retain caller-scoped semantic idempotency; issued facts are not rewritten. |
| Catalog | `Application.Catalog`, `.Postgres` | Tenant product/service and unit identity, rename/retirement history, immutable direct conversions, availability-only state and bounded frozen catalog line facts. Numerical inventory remains separate. |
| Orders | `Application.Orders`, `.Postgres` | Manual or separate catalog-priced drafts: create/read/browse/revise/abandon/commit with immutable receipts/history and optional customer/program attribution. Catalog-priced commitment revalidates current compatible publication/policy under an effect-transaction-bound pin; committed facts never reprice. |
| Pricing | `Application.Pricing`, `.Postgres` | Versioned tenant price drafts/publication/retirement, deterministic source precedence, typed missing/expired/conflict outcomes, override evidence and explainable candidate selection. |
| Quotations | `Application.Quotations`, `.Postgres` | Optional supplied-price or Catalog/Pricing drafts, revision-checked immutable issuance, tenant-wide numbering and protected bounded history. Drafting/issuance/history is locally qualified; responses/conversion are not yet introduced. |
| Invoices | `Application.Invoices` | Host-neutral invoice-issue orchestration over committed Orders: current billing-authority port, caller-scoped idempotency/replay contract, NPR/revision/arithmetic validation, debtor resolution through Customers public queries, immutable retained issued-fact contracts and explicit safe outcomes. No PostgreSQL adapter, numbering/date policy implementation, HTTP surface or durable invoice runtime is introduced. |
| CoreApi | `Application.CoreApi` | ASP.NET Core host: protected capability routes, JWT validation, membership-derived resource authorization with pinned OpenFGA checks, Autofac composition, shared bounded Npgsql pool, safe failure/admission/budget/health behavior, the conditional Hugging Face Storage Bucket adapter, and dormant profile-runtime mechanics. Explicitly enabled autonomous customer-import execution awaits bounded tenant discovery/row batches and drains through standard hosting; no generic Worker/scheduler is implied. |
| AdminApi | `Application.AdminApi` | Private ASP.NET Core host with exact ZITADEL identity, active registered Admin-device certificate, pinned-model OpenFGA authorization, retained protected-access audit, tenant provisioning, verified import/link of existing ZITADEL human identities into stable local accounts, bounded tenant/account/membership registry reads, and a bounded cooperative deadline on explicitly classified protected routes. |
| AdminBootstrap | `Application.AdminBootstrap` | One-shot private infrastructure executable. It prepares/reuses the durable bootstrap intent, writes the initial administrator relation to the explicitly pinned platform OpenFGA model with duplicate-safe semantics, confirms access at higher consistency, then marks local bootstrap complete. It exposes no HTTP bootstrap endpoint. |
| DatabaseMigrator | `Application.DatabaseMigrator` | One-shot ordered migration of IdentityAccess, Tenancy, PlatformAdministration, Customers, Catalog, Pricing, Orders and Quotations under an advisory lock. |

Each PostgreSQL adapter owns its context factory, migration files, runtime DI registration and persistence code. The migrator owns only the advisory lock, command/exit behavior and ordering. Capability projects stay host- and provider-neutral.

## Implemented HTTP surface (CoreApi)

The quotation draft/issue/detail/history routes and their exact accepted contract
are owned by [`QUOTATIONS_AND_CONVERSION.md`](docs/implementation/QUOTATIONS_AND_CONVERSION.md);
COM-009 is `PRODUCTION_HONEST` for that declared bounded scope, with its
[qualification receipt](docs/review/COM_009_IMPLEMENTATION_RECEIPT.md).

All protected responses are `no-store`, including authentication failures and handled errors. `GET /openapi/v1.json` describes the executable contract using the configured public brand; its OpenID Connect scheme is attached only to operations that require authorization.

Unhandled transient PostgreSQL failures return safe `503` / `database_unavailable`; non-transient provider and other unexpected faults return safe `500` / `internal_error`. A process-local, queue-free protected-request concurrency cap returns `503` / `api_capacity_exceeded` before authentication/authorization/handler work when full. The checked-in cap is 32, deployment-configurable from 1–256; it is not a throughput target, tenant quota or cross-replica fairness guarantee. After current membership, a separate queue-free per-tenant cap (default 8, configurable 1–256) returns no-store `429` / `tenant_capacity_exceeded` before OpenFGA/business work; bounded partitions retain only active leases. Native provider logs are disabled to avoid SQL/exception disclosure. Focused owners: `docs/implementation/CORE_API_FAILURE_CONTRACT.md` and `docs/implementation/CORE_API_ADMISSION_CONTRACT.md`.

| Route | Authority |
|---|---|
| `GET /health/live` | None. Dependency-free liveness. |
| `GET /health/ready` | None. Status-only, five-second single-flight cached check of primary PostgreSQL connectivity and read access to the configured OpenFGA model. Does not replace migration verification or an authorized business smoke journey. |
| `GET /openapi/v1.json` | None. Classified contract description. |
| `GET /api/v1/application/bootstrap` | None. Bounded public white-label identity with its own bounded cache/ETag policy. |
| `GET /api/v1/account` | JWT (configured HTTPS issuer, exact audience, signature, lifetime) and an active bound account. |
| `GET /api/v1/account/tenants` | JWT; returns only the account's current active memberships. |
| `GET /api/v1/tenants/{tenantId}/workspace` | Current membership plus persisted `workspace_viewer`. |
| `POST`, `GET /api/v1/tenants/{tenantId}/customers/organizations`; `GET .../organizations/{organizationId}` | Current membership plus distinct customer create/view permissions. Create is idempotent. |
| `POST`, `GET .../organizations/{organizationId}/programs`; `GET .../programs/{programId}` | As above. Create is idempotent. |
| `POST`, `GET /api/v1/tenants/{tenantId}/orders`; `GET .../orders/{orderId}` | Current membership plus `order_creator` plus `manual_pricer` (create) or `order_viewer` (detail and browse). Create is idempotent. |
| `POST /api/v1/tenants/{tenantId}/orders/price-preview` | Current membership plus `order_creator` and `manual_pricer`; bounded non-persisting calculation of supplied draft prices. No Idempotency-Key required. |
| `GET /api/v1/tenants/{tenantId}/orders/{orderId}/history` | Current membership plus `order_viewer`; bounded retained snapshots and actors from committed draft receipts. |
| `GET /api/v1/tenants/{tenantId}/orders/{orderId}/actions` | Current membership plus `order_viewer`; explains observed lifecycle and independently checked edit-plus-pricing/abandon/commit permissions without granting command authority. |
| `PUT /api/v1/tenants/{tenantId}/orders/{orderId}/draft` | Current membership plus `order_editor` and `manual_pricer`; requires `expectedRevision`; idempotent. |
| `POST /api/v1/tenants/{tenantId}/orders/{orderId}/abandon` | Current membership plus `order_abandoner`; requires `expectedRevision`; idempotent. |

| `POST /api/v1/tenants/{tenantId}/orders/{orderId}/commit` | Current membership plus `order_committer`; expectedRevision and caller-scoped idempotency; freezes priced facts without billing or fulfillment effects. |
| `POST /api/v1/tenants/{tenantId}/customers/individuals`; `GET .../individuals/{individualId}` | Current membership plus independent `individual_creator` or `individual_viewer`; no login binding or debtor assignment. |
| `POST .../individuals/{individualId}/contact` | Current membership plus independent `individual_contact_editor`; expectedRevision and idempotency; updates bounded display-name/email/phone facts with attributable change metadata. |
| `POST .../individuals/{individualId}/availability` | Current membership plus `individual_availability_editor`; expectedRevision and idempotency; contact-free transition response. |
| `POST .../organizations/{organizationId}/representatives`; `GET .../representatives/{representativeId}`; `POST .../representatives/{representativeId}/unlink` | Current membership plus independent `representative_manager` or `representative_viewer`; active individual and valid organization/program parent required; relationship reads do not disclose linked contact fields. |

Every tenant route captures the route tenant only as an untrusted candidate (Finbuckle), establishes the current account, derives `TenantContext` from current membership, and only then checks OpenFGA. OpenFGA receives verified membership only as a contextual tuple, uses opaque GUID-based tuple identifiers, checks with `HIGHER_CONSISTENCY`, and fails closed with a bounded safe `503` when the provider is unavailable.

## Current Phase 1 platform-registry slice

GATE-001 is accepted for its declared backend-entry scope on baseline `7deba82d4c1a15dfc63acd660c8d99209b97f9c1`; its retained decision is `docs/production-completion-review/evidence/GATE-001-DECISION-7deba82.md`. ADM-006 is now implemented in the working tree with separate tenant/account/membership read permissions, bounded resource-typed v1 keyset pagination, current tenant/membership lifecycle revisions, no provider identity payloads, and mandatory durable Platform Admin access-audit evidence for registry admission while registry/business state remains read-only. AdminApi now fails startup for access-classification/authentication/platform-permission/audit-declaration mismatch and its post-authentication middleware enforces the permission and audit operation declared by trusted endpoint metadata before the handler runs. CoreApi likewise executes the validated application-authorization contract in a pre-handler middleware after resolving the current tenant boundary, rather than relying on an independent in-handler permission constant.

The new ADM-006 source is not yet `PRODUCTION_HONEST` in this local receiving environment because its newly authored real PostgreSQL/OpenFGA tests and the exact post-change `./eng/verify.sh` cannot execute without the locked NuGet cache and Docker. Focused owner: `docs/implementation/ADMIN_API_PLATFORM_REGISTRY_READS.md`.

## Current Phase 1 tenant-authorization administration slice

ADM-005 is resolved as **import/link-only**: SquiFlow does not create or invite ZITADEL humans in `v0.0.1`, so provider-side account-creation/reconciliation dependencies are not activated. ADM-007 defines global local-account suspension/reactivation as future Platform Admin/security authority while tenant-local loss of access remains membership lifecycle. ADM-008 selects the initial delegation contract: the current protected initial Owner is the only tenant role administrator; exercise and delegation are separate; delegation is bounded to the compiled business-permission catalog; direct grants plus multiple custom roles compose by allow-union; role administration, platform authority and arbitrary OpenFGA relations cannot be delegated through custom roles.

ADM-009 through ADM-012 are implemented in the current working tree as one Tenancy-owned authorization-administration state machine. PostgreSQL retains per-tenant authorization revision, immutable proposal/evidence records, direct-grant state, bounded custom-role definitions/assignments and replay-safe Owner-transfer receipts. CoreApi is the provider adapter: it writes/deletes tuples only against the explicitly pinned OpenFGA model, observes the requested provider state before marking a proposal Applied, and rechecks the original delegating Owner immediately before provider mutation. Reconciliation is bounded synchronous/manual in this slice; no background Worker/OPS-003 dependency is activated. Owner handoff requires the current Owner plus the high-risk admission contract in `docs/security/HIGH_RISK_ACTION_ADMISSION.md`. Focused owner: `docs/implementation/TENANT_AUTHORIZATION_ADMINISTRATION.md`.

The host-neutral Tenancy core compiles on .NET SDK 10.0.401 with zero warnings/errors. The new PostgreSQL/OpenFGA/CoreApi tests are authored but are not yet dynamically qualified in this local environment; do not infer production acceptance until those real-boundary tests and the exact full repository gate run successfully.

## Implemented HTTP surface (AdminApi)

Every protected AdminApi route requires the configured OIDC identity, HTTPS, an active principal-bound Admin-device certificate fingerprint, and a current higher-consistency permission from the pinned platform OpenFGA model. Authorization attempts append bounded PostgreSQL audit evidence and protected responses are `no-store`.
A required `AdminApi:ProtectedRequestTimeoutSeconds` value (1–120; checked in as 30) applies only to the explicitly classified protected platform routes. It cooperatively cancels existing request operations and can return safe `504` / `request_timeout` Problem Details before headers start; public health routes keep their existing behavior. The focused owner is `docs/implementation/ADMIN_API_REQUEST_BUDGETS.md`. The cooperative request-budget scope is `PRODUCTION_HONEST`. The public-authentication gap corrected through trusted endpoint metadata in commit `6735370` has its retained ADM-003 review receipt at `docs/production-completion-review/evidence/ADM-003-INDEPENDENT-REVIEW-RECEIPT.md`, and GATE-001 is accepted for baseline `7deba82d4c1a15dfc63acd660c8d99209b97f9c1`. The current `BLOCKED` responsibility is the post-GATE ADM-006 source described above, which requires its own receiving provider regressions and exact integrated gate.

| Route | Authority and effect |
|---|---|
| `GET /health/live` | Dependency-free liveness. |
| `GET /health/ready` | Status-only PostgreSQL and platform OpenFGA readiness. |
| `GET /api/v1/platform/access` | `can_access_admin`; verifies the complete request-time Admin boundary. |
| `GET /api/v1/platform/tenants`; `GET /api/v1/platform/tenants/{tenantId}` | `can_read_tenants`; bounded keyset browse or current lifecycle detail with durable Platform Admin access-audit evidence. |
| `GET /api/v1/platform/accounts`; `GET /api/v1/platform/accounts/{accountId}` | `can_read_accounts`; bounded keyset browse or local-account detail without provider identity payloads, with durable access-audit evidence. |
| `GET /api/v1/platform/tenants/{tenantId}/memberships`; `GET .../memberships/{accountId}` | `can_read_memberships`; bounded keyset browse or current lifecycle detail/revision with durable access-audit evidence. |
| `POST /api/v1/platform/tenants` | `can_provision_tenant`; caller-scoped semantic idempotency and retained tenant-provisioning receipt. |
| `POST /api/v1/platform/accounts` | `can_onboard_account`; verifies an existing human ZITADEL subject, then atomically creates the stable local account, first identity binding and retained receipt. |
| `POST /api/v1/platform/accounts/{accountId}/identities` | `can_link_identity`; verifies another human ZITADEL subject, requires an active local account, then atomically creates the unique binding and retained receipt. |
| `POST /api/v1/platform/tenants/{tenantId}/memberships`; `POST .../memberships/initial-owner` | `can_manage_memberships`; invites an active account or creates the one protected initial-Owner membership with caller-scoped idempotency. |
| `POST .../memberships/{accountId}/{activate\|suspend\|remove}` | `can_manage_memberships`; expected-revision membership transition with retained actor/device receipt. |
| `POST /api/v1/platform/tenants/{tenantId}/lifecycle/{suspend\|reactivate}` | `can_manage_tenant_lifecycle`; expected-revision tenant transition. Suspension immediately blocks membership-derived tenant access. |

The exact identity-import/linking scope, provider boundary, failure contract, evidence and non-claims are owned by `docs/implementation/ADMIN_API_IDENTITY_IMPORT_AND_LINKING.md`.

The JSON-kind validation and safe malformed-input/provider-response behavior are
owned by `docs/implementation/ADMIN_API_JSON_BOUNDARY_CONTRACT.md`. Its normal
parallel `./eng/verify.sh` qualification on 2026-10-03 passed all **640 tests**
across 15 suites with zero failures/skips and zero Release warnings/errors,
including real PostgreSQL/OpenFGA membership lifecycle qualification. This is
local evidence for the declared scopes, not deployment or whole-backend readiness.

## Behavior that is guaranteed

- **Tenant isolation:** explicit tenant SQL predicates plus forced PostgreSQL RLS with transaction-local tenant context. Orders and Customers data is tenant-owned.
- **Idempotency:** Customer organization/program creates and Order create/revise/abandon use caller-scoped semantic idempotency keys with durable receipts.
- **Order draft contract:** browse is bounded newest-first keyset pagination with a versioned tenant-bound cursor and no count or text-search claim. Revision replaces the entire priced draft only while it is a draft and the expected revision matches, increments revision and preserves earlier receipts. Abandonment is a one-way `draft` to `abandoned` transition that retains priced content and the original create receipt, records the abandoning account and authoritative timestamp, and increments revision. Detail and browse expose current lifecycle state. Abandonment does not delete the draft or reverse financial, inventory or fulfillment effects.
- **Detail consistency:** draft detail reads its header and lines from one `RepeatableRead` snapshot, so a concurrent revision is never observed as a mixed header and lines (regression guard: `DraftDetailReadsHeaderAndLinesFromOneSnapshotDuringConcurrentRevision`).
- **Attribution, not billing:** the organization/program association on a draft is an attribution contract, not legal debtor, account billing, credit, invoice or settlement authority.
- **Order-entry price preview:** a protected, bounded preview returns normalized supplied lines and totals through the same calculator as creation, without saving or reading an order. It does not select/approve prices or introduce customer policy, tax, stock or financial effects. Its focused owner is `docs/implementation/PRICING_COMPONENT_BOUNDARY.md`.
- **Retained draft history:** current viewers can browse the priced snapshots, actors and recorded times of successful draft commands. The read reuses immutable durable receipts, uses one database statement snapshot and performs no mutation. Its focused owner is `docs/implementation/ORDER_DRAFT_HISTORY.md`; general audit and override reasons remain absent.
- **Draft action guidance:** a current viewer can discover whether revision/abandonment is available and why, using the existing lifecycle and distinct current write permissions. The response is an observation; commands recheck authority and expected revision. Its focused owner is `docs/implementation/ORDER_DRAFT_ACTION_GUIDANCE.md`; broader customer-specific workflow remains absent.
- **Verification:** `Application.Architecture.Tests` checks the dependency graph, provider isolation, solution membership and the inventory counts above.
- **Database grants:** grants for the restricted CoreApi login are a separate provisioner artifact under `deploy/database/`. No production role-creation or credential-rotation automation is claimed.

## Required deployment configuration

A deployment must provide the public Branding values, Authentication authority/audience, exact `AllowedHosts`, the primary database connection, and exact OpenFGA API/store/model/credential configuration. Checked-in configuration exposes bounded authentication, authorization, bootstrap-cache and database/profile-runtime resource policies for deliberate deployment review and override. Blank, permissive or unsafe configuration fails startup. DbMigrator additionally requires a deployment-specific nonzero advisory-lock key and an explicit bounded lock timeout.

## Explicitly `NOT_INTRODUCED`

No production feature catalog; durable Tenant Application Profile authority; production profile-specific resolution (no production endpoint acquires the internal tenant-keyed profile-runtime registry); live ZITADEL-instance/service-account qualification; provider-side user creation or reconciliation; tenant-to-ZITADEL organization mapping; login/callback/session flows; general Owner/Staff permissions or custom-role modeling; ongoing platform/tenant role administration and tuple reconciliation; authorization revision; normal Admin-device registration/revocation/rotation lifecycle; tenant/Workstation device lifecycle; a broader order/business lifecycle (recorded quotation responses/conversion, admission, fulfillment, durable invoice persistence/number allocation, payment, credit, return, refund); an external PostgreSQL pooler; Worker; Web UI; Workstation; a general ApplicationKernel/module runtime.

## Excluded from the inventory

The ignored `reference-sources/snapshots/` research workspace may contain upstream `.csproj`, source and test files at pinned revisions. They are external evidence only: not application projects, not referenced by product code, and not counted above.

The deleted `PartyKind` slice and its executable scaffolding were purged on 2026-09-17. The decision and exact removed inventory are recorded in `docs/decisions/CURRENT_IMPLEMENTATION_PURGE_2026-09-17.md`.

## Current gate state

Phase 0A remains the qualified reset baseline. Its enduring guarantees still govern new work: current authority must be distinguishable from history, boundaries must be earned by real responsibility, and every introduced claim needs falsifiable evidence plus a lasting regression guard.

The former Phase 0B Parties qualification is retired historical evidence. It does not describe the current tree and does not authorize recreation of its enum, projects, solution, package files, tests, or CI configuration.

The bounded ApplicationProfiles feature compiler, deployment-wide public Branding contract, CoreApi bootstrap/liveness paths, classified OpenAPI v1 document, configured JWT validation, active-account resolution, current active-membership listing, exact account/membership queries, immutable membership-derived `TenantContext`, Finbuckle route-candidate plumbing, pinned-model OpenFGA workspace, Orders and Customers permissions, immutable tenant-owned customer organization/program create/read/browse, optional customer/program-attributed priced order-draft create/read/browse/revise/abandon, caller-scoped semantic idempotency, explicit tenant SQL plus forced PostgreSQL RLS, shared bounded/resetting CoreApi Npgsql data source, and the ordered one-shot PostgreSQL migrator are `PRODUCTION_HONEST` for their declared narrow scopes. The Customer organization/program and Orders attribution evidence and regression guards are owned by `docs/implementation/CUSTOMER_ORGANIZATION_PROGRAM_ATTRIBUTION_SLICE.md`; the Orders draft/lifecycle contract remains owned by `docs/implementation/ORDER_DRAFT_INTAKE_SLICE.md`, including the separate `order_editor` and `order_abandoner` permissions, revision-checked changes, lifecycle metadata and scoped persistence rights. That owner defines host-neutral, ASP.NET pipeline, OpenFGA and PostgreSQL evidence for create, read, browse, revise and abandon. The local `COLLECT_COVERAGE=1 ./eng/verify.sh` run on 2026-09-24 passed locked restore, formatting, Release build, all 307 tests and coverage report generation. Remote CI and a production deployment are separate claims. Tenant-specific branding, real ZITADEL topology/flows, broader OpenFGA roles/administration and the broader business lifecycle remain `NOT_INTRODUCED`. For that dated 2026-09-24 scope, `BLOCKED = none`; this historical statement does not override the current blocker above.

The host-neutral `Application.Invoices` slice is `PRODUCTION_HONEST` for its deliberately partial scope: current billing-authority orchestration, caller-scoped replay/idempotency flow, committed-Order/revision/NPR checks, decimal 19,4 `ToEven` verification, tenant-scoped debtor resolution and immutable issue facts. On current HEAD the focused invoice suite passed 25/25 and the architecture suite passed 14/14 before the combined-gate transport was lost. PostgreSQL persistence, numbering/allocation, business-date policy, HTTP/OpenFGA adapters and durable invoice runtime remain `NOT_INTRODUCED`; the focused owner is `docs/implementation/INVOICE_ISSUE_CONTRACT.md`, and the current tracked evidence summary is `docs/production-completion-review/evidence/COM-020.md`.

Historical AdminApi evidence before the retained GATE-001 owner acceptance (the pending statements in this paragraph describe that earlier snapshot, not the current combined local gate):

The private AdminApi request boundary, tenant provisioning command, and the exact existing-ZITADEL-human identity import/link slice are `PRODUCTION_HONEST` for their declared narrow scopes. The account operation atomically owns local account/binding/receipt effects; the link operation serializes against account availability and atomically owns binding/receipt effects. Both recheck request-time principal, registered device and pinned OpenFGA authority, preserve caller-scoped semantic idempotency, and return safe provider failures. Revision-checked AdminApi membership and tenant suspension/reactivation operations, including one protected initial-Owner bootstrap designation, are `PRODUCTION_HONEST` for the narrow scope in `docs/implementation/ADMIN_API_MEMBERSHIP_LIFECYCLE.md`. The protected-request cooperative deadline is `PRODUCTION_HONEST`; current-head AdminApi and Tenancy PostgreSQL suites passed 87/87 and 19/19 respectively. The explicitly public-health bearer exclusion is implemented in commit `6735370` with focused current-head evidence, but its required independent post-fix ADM-003 review receipt is absent from the repository, so that slice remains acceptance-pending/`BLOCKED` for GATE-001. The scope and retry semantics are owned by `docs/implementation/ADMIN_API_REQUEST_BUDGETS.md`, with tracked current-head evidence in `docs/production-completion-review/evidence/ADM-002-ADM-003.md`. GATE-001 remains `BLOCKED` pending both that independent receipt and the exact combined normal repository gate. Provider-side creation/reconciliation, live ZITADEL deployment qualification, general roles and ongoing operator/Admin-device lifecycle remain `NOT_INTRODUCED`. Identity evidence and requalification triggers remain owned by `docs/implementation/ADMIN_API_IDENTITY_IMPORT_AND_LINKING.md`.

## Active implementation rule

Dated `BLOCKED = none` statements below apply only to the exact source and scope of those historical runs. They do not override the current blocker declared at the top of this file.

The 2026-10-01 local full parallel `./eng/verify.sh` run passed locked restore,
format verification, Release build and all **330 tests**, with zero failures or
skips. This qualifies the narrow failure/admission contracts above and the root
version-marker guard. It also reverified the existing Orders snapshot-read
regression under actual PostgreSQL. Successful customer organization/program
creates and Orders draft create/revise/abandon now emit safe structured outcome
logs and bounded-label counters, distinguishing commits from replays. Optional
diagnostic sink failures cannot invalidate those completed outcomes. Scope,
regression evidence and non-claims are owned by
`docs/implementation/CORE_API_MUTATION_DIAGNOSTICS.md`; this is best-effort
diagnostics, not durable audit. Coverage was not collected in this run;
the earlier coverage result above remains historical. No remote CI, production
deployment or complete commercial-backend qualification is inferred from this run.

The subsequent 2026-10-02 local full parallel `./eng/verify.sh` run passed all
**334 tests**, with zero failures or skips, after locked restore, formatting and
Release build. It qualifies canonical, culture-independent customer pagination
validation under the existing v1 cursor format; the focused Customers owner
records compatibility and regression evidence. Existing PostgreSQL guarantees
were reverified. This run did not collect coverage or qualify a deployment.

A later 2026-10-02 local full parallel `./eng/verify.sh` run passed all **339
tests**, with zero failures or skips and zero Release build warnings/errors.
Customers runtime SQL is now adapter-owned embedded resources, matching Orders;
an architecture guard prevents inline runtime SQL drift in those adapters.
Real PostgreSQL tests verify completed/disposed session rejection, rollback on
disposal, transaction-local tenant cleanup through a one-connection pool and
direct adapter page-size bounds before connection acquisition. The focused
Customers owner records those guarantees and requalification triggers. This
run did not collect coverage or qualify remote CI or production deployment.

The protected CoreApi pipeline now has a configurable cooperative request budget
(`RequestBudget:ProtectedRequestTimeoutSeconds`, required 1–120; default 30).
Deadline cancellation before headers start returns safe `504` / `request_timeout`
when it escapes as cancellation, preserves protected no-store and releases
admission capacity as the pipeline unwinds. Public routes retain their existing
policies. The focused owner `docs/implementation/CORE_API_REQUEST_BUDGETS.md`
records limits, retry semantics and guards; this does not guarantee forced
termination or rollback. Its 2026-10-02 local full parallel gate passed all **348
tests**, zero failures/skips and zero build warnings/errors. Coverage, remote CI
and deployment remain separate claims.

CoreApi now validates every mapped route's access classification against explicit
authentication metadata before serving requests. Invalid or anonymous-overridden
protected declarations fail startup. Real-host tests also verify anonymous denial
and no-store across every current protected route/method with forged admin headers.
The focused owner is `docs/implementation/CORE_API_ENDPOINT_ACCESS_GUARD.md`.
The 2026-10-02 full parallel gate passed **351 tests**, zero failures/skips and
zero Release warnings/errors. This was an existing-host safeguard and, at that
gate, AdminApi was not yet introduced. The subsequently implemented private
AdminApi boundary and its narrow identity-import/linking scope are recorded in
`docs/implementation/ADMIN_API_IDENTITY_IMPORT_AND_LINKING.md`; the expanded
delivery sequence remains in `docs/implementation/DELIVERY_PLAN.md`.

The end-to-end business scope is now mapped in
`docs/implementation/BUSINESS_OPERATION_END_TO_END.md`, including conditional
pricing, quotation/approval, fulfillment/outsourcing, purchasing/stock,
receivable/payable, settlement and correction responsibilities. Current supplied
draft selling-price calculations are separated from order-intent assembly in
`Application.Orders/Pricing/OrderDraftPriceCalculator.cs`; the focused boundary
is `docs/implementation/PRICING_COMPONENT_BOUNDARY.md`. No new lifecycle state,
price-selection authority or customer-specific workflow is introduced. The
2026-10-02 full parallel gate passed all **353 tests**, zero failures/skips and
zero Release warnings/errors. `BLOCKED = none`; the business map is scope guidance,
not a completed commercial backend. Coverage, remote CI and deployment were not
qualified by this run.

CoreApi now exposes a protected, non-persisting order-entry price preview using
the same calculator as draft creation. The focused pricing owner above records
authorization, resource bounds, parity and no-mutation guards. The 2026-10-02
normal parallel `./eng/verify.sh` run passed all **373 tests**, zero failures/skips
and zero Release warnings/errors. The owner selected operator-entered prices
first with controlled overrides; separate manual pricing authority is now implemented as described below; richer
reason/approval/ceiling policy remains unintroduced. Product
version remains **v0.0.1**; remote CI, coverage and deployment remain separate.

### COM-002 customer contacts and representative relationships, 2026-10-06

The current working tree contains the bounded Customers contact-edit and
organization/program representative relationship slice. It is `PRODUCTION_HONEST`
only for the focused scope in `docs/implementation/CUSTOMER_CONTACTS_REPRESENTATIVES_SLICE.md`:
revision-checked contact changes, active-individual relationship validation,
tenant-owned persistence/RLS, independent OpenFGA permissions, replay-safe receipts,
and protected CoreApi reads/mutations. It does not introduce duplicate resolution,
onboarding import, catalog identity or adaptive price selection.

The exact normal `./eng/verify.sh` run on 2026-10-06 passed locked restore, formatting,
the Release build with zero warnings/errors, and all **789 tests** across the solution
with zero failures/skips. `git diff --check` is clean. This is local receiving-tree
evidence only; it does not qualify remote CI, deployment, or the unresolved COM-003,
COM-004, COM-005, COM-006 and dependent COM-007 decisions.

### COM-003–COM-007 continuation, 2026-10-07

The owner closed the commercial decisions on 2026-10-06; they are no longer
undecided prerequisites. The current uncommitted tree composes duplicate review/
manual forward canonicalization, bounded CSV planning and durable autonomous row
execution, Catalog units/conversions/availability, Pricing publication/policy/
selection and a separate catalog-priced Orders path. All remain tenant-scoped with
independent permissions, retained receipts/history and immutable committed facts.
Focused owners are `CUSTOMER_DUPLICATES_AND_IMPORTS_SLICE.md`,
`CATALOG_AND_UNIT_BOUNDARY.md`, `PRICING_POLICY_AND_PUBLICATION.md` and
`ORDER_CATALOG_PRICED_DRAFTS.md` under `docs/implementation/`.

The original COM-001/002 behavior and manual-entry compatibility are preserved.
The first combined gate passed restore/format/build but failed three old manual
HTTP tests because new strict DTO attributes rejected formerly ignored input.
That narrowing was reverted; a new regression proves ignored fields cannot attach
server-owned commercial facts. A separate red-first pure/PostgreSQL regression
found price selection ignoring conversion revision; filtering now precedes all
candidate bounds and incompatible unit meaning cannot reuse an old price.

  An earlier retained snapshot passed the normal `./eng/verify.sh` with locked restore,
  formatting, a Release build with zero warnings/errors and all **1044 tests** across 20/20
  test projects, zero failures/skips. Retained log:
  `docs/production-completion-review/evidence/GATE-COMBINED-03406c4-WORKTREE-1044.log`.

  Historical project summaries for that earlier run (not the final qualification below):

  | test project | passed | seconds |
  |---|---|---|
  | AdminApi | 96 | 39 |
  | AdminBootstrap | 3 | 12 |
  | Architecture | 16 | 0 |
  | Branding | 11 | 0 |
  | Catalog.Postgres | 8 | 17 |
  | Catalog | 18 | 0 |
  | CoreApi | 457 | 128 |
  | Customers.Postgres | 51 | 144 |
  | Customers | 46 | 0 |
  | IdentityAccess.Postgres | 20 | 17 |
  | IdentityAccess | 12 | 0 |
  | Invoices | 25 | 0 |
  | Orders.Postgres | 71 | 196 |
  | Orders | 104 | 0 |
  | PlatformAdministration | 4 | 0 |
  | Pricing.Postgres | 11 | 32 |
  | Pricing | 11 | 0 |
  | Profiles | 18 | 0 |
  | Tenancy.Postgres | 26 | 41 |
  | Tenancy | 36 | 0 |
  | **total** | **1044** | **628** |

  An earlier CoreApi run failed two `OrderDraftBodyBoundsTests` cases with
  `AddressInUseException`, then the same suite passed on a re-run. Those outcomes remain
  historical; a passing retry does not prove the race absent. The incoming gate runs
  project processes sequentially, but this does not itself prove cross-class host-port
  races are eliminated. This qualification did not alter test parallelism.
The declared non-retaining Customers, Catalog, Pricing and catalog-priced Orders
runtime scopes are `PRODUCTION_HONEST` for this local receiving evidence.
COM-004 local completion and its separate external-provider blocker are recorded below. Evidence and recurring guard details
are recorded in `docs/development-tasks/TASK_STATUS.md`.
Local evidence does not constitute product-owner/reviewer acceptance, remote CI,
live identity-provider or deployment qualification.

### Current COM-004 local qualification, 2026-10-07

COM-004 implementation and local qualification are **COMPLETE** for the declared
capability, CoreApi and PostgreSQL contracts. Raw-source lifecycle, expiry-aware
reads, fenced retirement/recovery and hosted source-only tenant execution are
locally `PRODUCTION_HONEST`. Production/provider qualification remains `BLOCKED`
only on the live OPS-007/OPS-008 Hugging Face evidence; this is not an unresolved
local retention or recovery implementation claim.

The fresh exact `./eng/verify.sh` completed with **exit 0**, **1045 passed / 0 failed /
0 skipped** across **20 test projects**, and a Release build with **0 warnings /
0 errors**. The final gate includes CoreApi **458/458** and Customers PostgreSQL
**51/51**. Standalone pre-upload-fix suites passed 457/457 and 51/51; they are
supporting earlier evidence, not substitutes for the final gate. Post-fix standalone
formatter verification and `git diff --check` passed. Complete local results, source
hashes, original failures and the safety review are retained under
`artifacts/verification/com004-current-20261007/` (`RESULTS.json`, `gate.log`,
`gate-source.json`, `STATIC-REVIEW.md`).

Live Hugging Face upload, download, delete, conditional write, redirects/timeouts, finite capacity, asymmetric provider/database failure and remote reconciliation remain unrun and NOT qualified. Checked-in `ObjectStorage.Enabled=false`; no nonempty relevant provider runtime configuration variables were visible. No private credential, substitute provider or fallback byte archive was introduced.

This is local dirty-tree qualification, not commit/merge/PR integration, remote CI,
coverage, live identity-provider qualification, deployment readiness or production
acceptance. Incoming `eng/verify.sh` runs its unit/container project groups
sequentially; this pass did not change that policy or serialize test cases to hide
failures. Historical gate totals are preserved below only as historical evidence.

Default raw-source retention remains seven days; archive is explicitly elected and
has no expiry. Current tenant/import authority precedes metadata/provider access.
Retirement recovery is generation/lease-ID fenced and excludes active/unknown
reservations; stale/repeated completion cannot release usage twice. Metadata,
manifest, plan, decisions and results remain after byte retirement. Nonempty
lifecycle/accounting downgrade is refused rather than discarding facts.

The current safety review reproduced and fixed an upload-stream digest disposal
fault with one permanent controlled regression. The four adapter regressions now
pass, including successful streamed PUT/read-back and caller stream ownership.
The local provider substitute is not evidence of live Hugging Face behavior.

Other absent scopes include numerical inventory, committed quotation/agreement
authority, discount/approval workflows, invoice persistence, Web and Workstation.

To exercise the implemented path: apply all registered migrations and
`deploy/database/grant-core-api-runtime.sql`, publish/pin the updated authorization
model and grant only intended capability tuples. Explicitly set
`CustomerImports__Execution__Enabled=true` for autonomous import acceptance; it
fails safe 503 while disabled/starting/unavailable/draining. Use the canonical
`modules/customers/Application.Customers/customer-import-v1-template.csv`.
Pricing requires a published tenant policy and active compatible Catalog identities.

### Retained draft history and order-entry boundaries, 2026-10-02

Protected order history is `PRODUCTION_HONEST` within
`docs/implementation/ORDER_DRAFT_HISTORY.md`: bounded revision pages reuse
successful immutable receipts, retain historical prices/actors and enforce current
viewer authority. Create/revise now also bound the actual request stream,
preserve native supported charsets, and return safe 413/400 responses for
oversized bodies/unsupported charset names. Their owner and recurring HTTP
regression evidence remain in `ORDER_DRAFT_INTAKE_SLICE.md`.

The final normal parallel `./eng/verify.sh` passed all **413 tests** in twelve test
projects, with zero failures/skips and zero Release warnings/errors. A preceding
attempt exposed Testcontainers image-parser startup timeouts; fixed-image fixtures
now use the supported structured-image API, with unchanged provider versions and
no test retries or serialization. See `docs/testing/VERIFICATION_STRATEGY.md`.
`BLOCKED = none` for these declared scopes. Acceptance, adaptive price-source/approval policy,
fulfillment, billing/invoices and settlement remain `NOT_INTRODUCED`; this does not
qualify the complete commercial backend, remote CI or a production deployment.

### Draft action guidance and separate manual price authority, 2026-10-02

The protected draft action guide explains the observed revision and current
revision/abandonment availability without granting execution authority; its owner
is `docs/implementation/ORDER_DRAFT_ACTION_GUIDANCE.md`. Manual price entry now
requires its own `manual_pricer` grant alongside create for initial entry/preview,
or edit for full priced replacement, including unchanged submitted prices and
idempotent retries. Read and abandonment remain independent. The pricing owner
above records authority, historical facts, failure/recovery and model rollout.

Both declared scopes are `PRODUCTION_HONEST`, `BLOCKED = none`. The final normal
parallel `./eng/verify.sh` passed all **450 tests**, locked restore, formatting
and Release build with zero failures/skips and zero warnings/errors, including
actual PostgreSQL and OpenFGA regressions. Deployments must publish/pin the updated
model and explicitly grant intended pricing tuples. Acceptance, adaptive pricing
policy/reasons/approvals, fulfillment, debtor/invoices and settlement remain
`NOT_INTRODUCED`; this does not qualify the whole backend or production deployment.
Product version remains **v0.0.1**.

The next slice starts from a useful application responsibility, not a phase label or deleted project shape. Before custom infrastructure is written, use `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md` to decide whether to:

1. use a focused maintained package;
2. adapt bounded source when its license permits the intended use and distribution;
3. reuse tests, failure cases, or algorithms while retaining repository ownership; or
4. keep the source as reference with a concrete rejection reason.

For any selected source, record its immutable revision, exact inspected types/tests, license, entry mode, framework assumptions, repository-owned authority, known gaps, exit path, and repository-owned evidence.

License is not an exclusion filter for internal research. Research access does not itself authorize copying, dependency adoption or distribution; those decisions record and satisfy the applicable obligations when they occur.

The current source-owned backend base decision and subsystem ledger are in `docs/review/FULLSTACKHERO_BACKEND_ADOPTION_LEDGER.md`. Continue by admitting one real responsibility at a time; a narrow scope is allowed, while prototype-grade depth for an introduced claim is not.

## Verification

Repository verification is:

```bash
./eng/verify.sh
```

A selected executable can be built and verified independently of other hosts:

```bash
./eng/build-host.sh core-api --publish
./eng/verify-host.sh core-api
```

These commands use the host project graph, not the entire solution. Shared
libraries remain real dependencies. The scope, other implemented targets and
CI failure boundaries are owned by `docs/implementation/INDEPENDENT_HOST_BUILDS.md`.

The script restores from committed NuGet lockfiles, audits dependencies, verifies formatting, builds the solution in Release configuration and runs the complete test suite. `COLLECT_COVERAGE=1 ./eng/verify.sh` also produces an ignored local Coverlet/ReportGenerator report under `artifacts/coverage/report/`. Provider tests require Docker because they run PostgreSQL 17 through Testcontainers. The current environment may require writable `NUGET_PACKAGES` and `NUGET_HTTP_CACHE_PATH` locations. GitHub and GitLab verification wrappers invoke the same script with coverage and retain its report; their execution is not claimed until a remote run is inspected. The current testing-tool scope and security scan configuration are recorded in `docs/testing/VERIFICATION_STRATEGY.md`.

## Authority

Read in this order:

1. this file for current implementation inventory;
2. `docs/decisions/CURRENT_IMPLEMENTATION_PURGE_2026-09-17.md` for the purge decision;
3. `docs/implementation/phases/phase-0/0A_BASELINE_STATUS.md` for the qualified reset guarantees;
4. `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md` for source-first routing;
5. the focused owner and current decision record for the responsibility being introduced.

Historical implementation, phase, review, branch, and CI records remain evidence and context only when they conflict with this current inventory.
