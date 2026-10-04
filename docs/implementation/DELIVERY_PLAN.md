# Delivery Plan (living queue)

**Product version:** v0.0.1
**Authority:** none. This is a working queue, not a specification. Current truth is `README.IMPLEMENTATION.md`; gate rules are `PHASE_GATE_PRODUCTION_HONESTY.md` and `PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`. If this file conflicts with a focused owner, the owner governs.
**Last reconciled:** 2026-10-02 against the code in `modules/`, `services/`, `tests/`.

## How to use this file

1. Every candidate below is `NOT_INTRODUCED`. Listing it is not authorization to build it.
2. When a candidate is picked up, first write or update its focused owner document (as `ORDER_DRAFT_INTAKE_SLICE.md` does), declare exact scope and non-claims, then implement it to `PRODUCTION_HONEST` in one coherent change.
3. A started responsibility is never carried forward here as `NOT_INTRODUCED`. Remove its entry and point to its owner.
4. Update this file only for ordering, dependencies and blocking decisions. Do not add evidence matrices, cadences, phase numbers or pre-written failure inventories; those are derived when the responsibility is real.

## 1. Hardening of already-introduced responsibilities

These are defects or gaps in claims that already exist, so the gate rules apply now. They are not "later hardening".

| # | Item | Evidence | State |
|---|---|---|---|
| H1 | Order draft detail could combine an old header with new lines during a concurrent revision. | Reproduced deterministically by `DraftDetailReadsHeaderAndLinesFromOneSnapshotDuringConcurrentRevision`; fixed by a `RepeatableRead` snapshot session in `Application.Orders.Postgres`. | Fixed 2026-09-30, guarded by that test |
| H2 | Defined safe HTTP database-failure responses and prevented raw provider diagnostic disclosure. | `CORE_API_FAILURE_CONTRACT.md`; real PostgreSQL outage/timeout/schema and hostile trace-parent tests. | Fixed 2026-10-01; full gate passed 324 tests |
| H3 | Safe observed outcomes for customer creates and Orders draft create/revise/abandon. | `CORE_API_MUTATION_DIAGNOSTICS.md`; all five operations/replays, negative paths, disclosure checks and diagnostic-sink failure tests. | Fixed 2026-10-01; full gate passed 330 tests; best-effort diagnostics, not durable audit |
| H4 | Root version markers must agree with the product lock. | `ProductVersionMarkersMatchTheLockedProductVersion` guards both markers against `LockedProductVersion`. | Fixed 2026-10-01; v0.0.1 restored and guarded |
| H5 | Customers cursor strictness, tenant-session completion and adapter page-size gaps. | Canonical cursor host regressions; real PostgreSQL completion, disposal, single-connection pool reuse and direct browse-bound tests; SQL resource architecture guard. | Fixed 2026-10-02; full gate passed 339 tests. Distinct payloads and DB isolation needs remain capability-owned; no speculative shared abstraction introduced |
| H6 | Every PostgreSQL test class starts its own container (about 50 startups), which slows the suite. | `tests/integration/*/PostgresTestDatabase.cs`. | Open; not a correctness issue |
| H7 | Protected HTTP work lacked a process-wide admission bound. | `CORE_API_ADMISSION_CONTRACT.md`; real-host saturation and permit recovery tests. | Fixed 2026-10-01; per-tenant quotas and cross-replica fairness are separate work |
| H8 | Protected requests lacked an overall cooperative processing budget. | `CORE_API_REQUEST_BUDGETS.md`; real-host deadlines, client cancellation, capacity recovery, slow body, disclosure and OpenAPI regressions. | Fixed 2026-10-02; full gate passed 348 tests; no forced termination or rollback claim |

## 2. Trust and platform candidates

| # | Candidate | Trigger | Depends on | Blocking decisions (from `OPEN_DECISIONS.md`) |
|---|---|---|---|---|
| T1 | Real ZITADEL login and session | Real users need supported Web or Workstation sign-in | Existing JWT validation and account binding | Web render mode and session topology; session-revocation persistence; Back-Channel Logout |
| T2 | Account and tenant provisioning | A real tenant and user must be onboarded | T1 | Tenant-to-ZITADEL-organization mapping; onboarding and import scope |
| T3 | Owner/Staff roles, OpenFGA tuple administration and reconciliation | A tenant must manage who can do what | T2 | Staff permission defaults; first Owner/Staff OpenFGA model; durable protocol across SquiFlow metadata and OpenFGA writes |
| T4 | Authorization revision | An operation or snapshot must detect changed authority | T3 | Persistence type of `TenantAuthorizationRevision`; consistency policy per operation class |
| T5 | Audit trail | First administration or money-adjacent change needs attributable history | Defined actors (T3) | None listed; derive from the relevant owner when triggered |
| T6 | Outbox and Worker | A real operation needs durable asynchronous effects | The owning transaction and its failure semantics | First real workload and runtime details |
| T7 | Web shell | The chosen first journey needs a usable Web client | T1 and authorization for each exposed operation | Render mode and session topology; CSRF mechanism |
| T8 | Files and documents | A slice needs retained customer documents or generated records | The using slice | File scanning and quarantine mechanism |
| T9 | Observability beyond current metrics | A production claim needs diagnosis or operating thresholds | The runtime being observed | Numeric operational targets |

Current observability is `ILogger`, `System.Diagnostics.Metrics` (profile runtime, authorization and mutation outcome meters) and the two health endpoints. Mutation metrics have finite operation/outcome/channel labels; identifiers remain operator log fields. There is no Serilog, OpenTelemetry or OTLP pipeline in code, whatever the observability documents describe as direction.

## 3. Commercial order flow (owner-selected direction, 2026-09-24)

The 2026-10-02 owner refinement requires reviewing the complete business journey
before adding further isolated states. `BUSINESS_OPERATION_END_TO_END.md` is the
source-backed responsibility/dependency inventory, including purchasing,
outsourcing, conditional inventory and corrections. `PRICING_COMPONENT_BOUNDARY.md`
owns separate price selection, override authority, selling-price calculation and
historical retention. Customer/program-adaptive guidance is required; one fixed
tenant workflow is not the product. The started acceptance implementation was
removed during this review and remains `NOT_INTRODUCED`.

An organization with program/account billing is the first case to develop. The order below is a dependency guide, not a mandatory workflow: payment may precede fulfillment, and a walk-in sale may need no quotation.

| # | Candidate | Trigger | Depends on | Blocking decisions |
|---|---|---|---|---|
| C0 | First product promise (PFQ-001 to PFQ-004) | Before any new long-lived state is hardened into a contract | Owner and customer facts | The promise itself; see Section 5 |
| C1 | Quotation | The journey needs an offer before commitment | C0; draft pricing; customer context | First outcome; money rounding and tax; quotations are optional; direct orders are accepted |
| C2 | Order admission | A real actor commits quoted or direct work | C0; C1 if quotation is required; T3 for admission authority | Admission effects; historical price retention; Order/Request/Job/Sale terminology |
| C3 | Fulfillment | The promised outcome requires recording production, handoff or pickup | C2 | How the work is actually performed (PFQ-003); Fulfilled/Completed/Ready/Delivered terms |
| C4 | Invoice and receivable | The journey needs an issued balance | C2; debtor selection | Individual billing-account identity and lifecycle; invoice numbering, tax, retention |
| C5 | Payment and credit | Settlement or credit must be recorded | C4 | Allocation across organization, program and individual debtors; current-exposure authority |
| C6 | Return, cancellation and refund | A real post-effect correction occurs | C3, C4, C5 as they exist | Corrections, credit and reversal rules |

## 4. Suggested order (subject to owner decisions)

```text
Now        End-to-end business review; separate pricing; first customer policy; C0 discovery
Then       T1 + T7 together (login is useless without a client, a client is useless without login)
           C1 or C2, whichever C0 shows the first customer needs
After      T2, T3, T4 as the first real tenant needs them
           C3 to C6 one step at a time, each earned by the previous step
On demand  T5, T6, T8, T9 when their trigger is real
```

### Backend completion scope requested 2026-10-02

The owner requested completion of the whole backend, rather than treating each
isolated hardening fix as the finished product. Use the tracks below to keep that
request visible. These are responsibilities to qualify, not new projects or
permission to silently settle the open business decisions above.

| Track | Backend outcome still required | Qualification boundary |
|---|---|---|
| Identity and tenant administration | Supported account/tenant provisioning, membership lifecycle, separate administration authority, role/relation administration and reconciliation | Actual configured identity provider and OpenFGA; no manual unrestricted SQL as the normal operating path |
| Tenant personalization | Durable published profile revision, bounded supported feature/settings/implementation selection, activation/rollback and real API/Worker tenant-runtime acquisition | One real supported variant; current authorization and tenant-safe lifetimes; cross-tenant and replacement/eviction evidence |
| Commercial operations | Direct order acceptance, optional quotation, fulfillment, debtor selection, invoice/receivable, payment/allocation and relevant cancellation/correction/refund paths | Capability-owned invariants, durable idempotency/concurrency, issue-time retained facts and real PostgreSQL tests for each introduced state |
| Supporting operations | Audit, documents/attachments, notifications, scheduler and durable Worker effects where the commercial path needs them | Named first workload, least privilege, crash/restart recovery, bounded processing and reconcilable external effects |
| API integration | Stable versioned contracts, errors, permissions, pagination, retries, request bounds and the selected client authentication topology | Consumer-oriented contract tests and a reproducible authenticated business smoke journey; frontend implementation is separate |
| Production operation | Reproducible deployment, separate identities/secrets, TLS/proxy policy, observability/export, restore/recovery and release/rollback | Actual target infrastructure, credential configuration, full CI and recovery/performance evidence; local tests alone do not qualify deployment |

Inventory/suppliers/purchasing, general reporting, arbitrary workflow/forms and
other catalog entries are not silently considered complete or mandatory merely
because a noun was listed earlier. Their exact supported workload must be named
before its module is introduced. Web UI and Workstation/Sync implementation stay
outside this backend-only execution request; any earned API/Worker responsibility
continues to use the same capability meaning.

Backend completion is not achieved until the accepted commercial outcome can be
performed end-to-end through owned APIs and the operating/recovery claims are
qualified. No track in this table is closed by adding folders or passing unit
tests alone.

### Delivery sequence accepted 2026-10-02

The owner expanded the request beyond backend-only work and selected this order:

1. Finish backend responsibilities, beginning with a useful private Admin API slice.
2. Develop the customer Web frontend against qualified backend contracts.
3. Develop the separate Platform Admin Web frontend.
4. Develop Workstation and its required device/local-storage/synchronization boundaries.

This replaces the earlier backend-only exclusion for subsequent work; it does
not qualify any of those absent runtimes. Customer Web and Platform Admin Web
remain separate presentation/security surfaces. The Admin API must enter owned
capabilities directly, never proxy its ordinary execution through CoreApi.

The initial Platform Admin bootstrap/device decision is now closed and its first
backend component is implemented separately from any HTTP control plane:
`Application.AdminBootstrap` performs one-time private bootstrap of the exact
administrator identity plus registered Admin-device mTLS certificate fingerprint,
reconciles the pinned platform OpenFGA administrator relation, and retains
authoritative PostgreSQL bootstrap audit. It does not reopen on normal access loss
and does not create a public/tenant first-admin endpoint.

The next Admin component is the private `AdminApi` host with request-time ZITADEL
identity, active registered-device certificate validation, current platform
authorization, admission/error/health boundaries and authoritative audit. Tenant
provisioning/activation remains the recommended first Admin API business operation
after that security boundary exists. CoreApi startup route classification remains
an existing-host safeguard, not Admin API implementation.

## 5. Owner inputs that unblock the most work

1. **First customer ecosystem (PFQ-001/007):** who is the buyer, administrator and daily operator for the organization-with-program case?
2. **First promised outcome (PFQ-002):** starting condition and observable completion (fulfilled, delivered, invoiced, paid or settled).
3. **Resolved 2026-10-01:** direct orders are allowed; quotations are optional.
4. **Debtor and billing account:** individual means a separate customer/person billing record (accepted 2026-10-01), not a sign-in account. Assignment authority, lifecycle and issue-time bill-to lock remain to qualify.
5. **Money and tax:** tax is excluded from the first invoice scope (accepted 2026-10-01). First invoice arithmetic retains decimal 19,4 and line `ToEven` rounding to four decimals followed by summation; prices and debtor freeze at issuance (accepted 2026-10-02). Later jurisdictional and correction/payment rules remain open.
6. **Currency:** per-document currency (current code) or a tenant default (`CROSS_CUTTING_BUSINESS_PRIMITIVES.md` assumes `Tenant.DefaultCurrencyCode`, which the code does not have).
7. **Offline scope (PFQ-008):** which work must survive loss of connectivity.
8. **Web render and session topology** (unblocks T1 and T7).

## 6. Delegation guide

| Work | Suitable for | Must be reviewed by |
|---|---|---|
| Read-only audits, inventories, doc cross-checks, research notes | Smaller, cheaper models | The accountable owner or a stronger model |
| Tests that follow an existing pattern, mechanical refactors with a fixed write scope | Mid-tier models | A stronger model for anything touching tenancy, idempotency or persistence |
| New authority, money, lifecycle or terminology semantics; identity, provisioning, role and tuple reconciliation; authorization revision; audit and outbox guarantees; financial invariants | Strongest model plus the owner | Owner sign-off |

Every delegated task states an exact write scope, forbids creating projects or restoring purged code, forbids editing gate states or implementation-truth documents unless named, and requires `./eng/verify.sh` with only inspected results reported.

## 7. Known documentation debt

The architecture and operations documents overlap heavily and still describe several directions as if current (for example planned `SquiFlow.*` project names, Hugging Face and Kaggle bootstrap storage, a Serilog/OTLP pipeline). Consolidation needs owner decisions on host naming (CoreApi versus WebApi), whether the listed infrastructure is deployed fact or accepted direction, and the history-archive scope. Until decided, treat `README.IMPLEMENTATION.md` and the code as the only statements of what exists.
