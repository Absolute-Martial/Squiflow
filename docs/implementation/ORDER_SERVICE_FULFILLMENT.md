# Order service fulfillment and customer pickup

**Owner:** Orders owns committed Order facts. This document owns the bounded service-work and pickup responsibility introduced for COM-014/COM-015.
**Qualification:** locally `PRODUCTION_HONEST` for the declared scope on the final source in [the receiving receipt](../review/COM_014_015_IMPLEMENTATION_RECEIPT.md); `BLOCKED = none` for this slice. This is not a deployment or whole-product qualification.
**Decision basis:** the owner-accepted [COM-014/015 decision proposal](../review/COM_014_015_DECISION_PROPOSAL.md). It does not expand the separately qualified COM-012/013 scope or change product version `v0.0.1`.

## Declared first scope

The only introduced case is one already committed, program-attributed Order containing exactly one catalog-priced line for an in-house, non-stock service. Its ordered quantity uses the line's existing unit identity, unit code, and precision. The service must have no unit conversion. At work start, CoreApi resolves the committed Order through the Orders boundary and verifies the item's current service/non-stock classification through the public Catalog boundary. The resulting source facts are retained with fulfillment; later catalog edits do not reinterpret started work.

Orders remains authoritative for the committed line, price, currency, attribution, and Order revision. Fulfillment stores the source Order revision and line identity as an immutable snapshot. Starting, completing, handing off, resolving a disposition, or correcting an entry cannot revise the Order or change its financial facts. Fulfillment is not a billing, inventory, invoice, payment, acceptance, or settlement authority.

This is an implemented software case, not evidence that a customer journey has been observed. It excludes quotations, multiple lines, artwork, uploads, new artwork approval, suppliers, stock movement, deposits, invoice or document creation, external notifications, customer portals, delivery, returns, and legal proof of receipt. Those cases need separate accepted contracts before implementation.

## Facts and transitions

The durable event history records an explicit start, good-completion quantities, pickup quantities, dispositions, resolutions, and linked corrections. Work and pickup remain separate: finishing work does not record a pickup, and a pickup does not imply payment, acceptance, or settlement. The current stage is derived from the started fact, unresolved disposition, and net quantities:

| Stage | Derived condition |
|---|---|
| `NotStarted` | No active start fact remains, including after correcting an erroneous start. |
| `InProgress` | Work is started, no disposition blocks it, and completed quantity is below the committed quantity. |
| `Paused` | A stopped, disputed, or defective disposition is unresolved. This takes precedence over quantity-based stages. |
| `ReadyForPickup` | Completed quantity equals the committed quantity, with no unresolved disposition, but pickup is incomplete. |
| `HandedOff` | Completed and handed-off quantities both equal the committed quantity, with no unresolved disposition. |

Completion and handoff quantities must be positive, exact at the retained unit precision (0–4 decimal places), and no greater than their remaining eligible quantities. No rounding, tolerance, unit conversion, or over-completion is allowed. A disposition records a required reason and optional affected quantity no greater than the ordered quantity. It changes no completion or handoff quantity. While unresolved it blocks both further completion and pickup. Resolution is a separate attributable event with a required reason and adds no quantity.

Pickup is operator-recorded evidence: server UTC time, account, quantity, retained unit, and a required recipient label of 1–128 Unicode scalar values. Reasons for dispositions, resolutions, and corrections are 1–512 Unicode scalar values. Text is outer-trimmed; malformed UTF-16 and control characters are rejected. Recipient labels and reasons are retained in protected event history and are excluded from ordinary detail and progress-inbox projections and diagnostics.

Events and command receipts are immutable. A correction requires the separate correction permission, current initial-Owner status, recent strong authentication, expected fulfillment revision, target event, and a bounded reason. It appends a linked correction and optional replacement; it does not rewrite or delete the original. Corrections must keep net good completion within `0..ordered quantity` and net handoff within `0..net good completion`, and cannot silently invalidate dependent facts. The correction authority represents a recorded-entry correction. It does not prove that an entry was physically false, establish return of picked-up goods, or authorize a customer return or legal/payment remedy.

Every accepted command advances the fulfillment revision, independently of the frozen Order revision. Start also names the expected committed Order revision. All other commands name the expected fulfillment revision and one caller-scoped, operation-specific idempotency key. Repeating the same key and intent replays its retained result; reusing it with changed intent conflicts. PostgreSQL serializes work mutations for the tenant Order, so one concurrent command wins a revision and stale commands receive a conflict.

## Authority and API ownership

CoreApi is the only introduced transport. `Application.Fulfillment` owns transition rules and provider-neutral contracts; `Application.Fulfillment.Postgres` owns tenant-scoped durable events, receipts, and the derived work projection; Orders and Catalog remain accessed through their existing public seams. The AdminApi, Web, Workstation, SyncApi, Worker, and a general workflow runtime do not own or redefine this capability.

Four independent delegated permissions are required, with no automatic Owner grant and no implicit Own/Assigned scope:

| Permission | Purpose | OpenFGA relation |
|---|---|---|
| `orders.fulfillment.view` | Read progress detail, inbox, and protected event history. | `order_fulfillment_viewer` |
| `orders.fulfillment.work.record` | Start, complete, block, and resolve work. | `work_recorder` |
| `orders.fulfillment.handoff.record` | Record pickup quantities and recipient labels. | `handoff_recorder` |
| `orders.fulfillment.correct` | Correct event-entry mistakes, in addition to initial-Owner and step-up requirements. | `corrector` |

OpenFGA checks use the pinned authorization model and intersect each grant with its tenant `member` relation. The existing adapter supplies the member contextual tuple only after the caller resolves current central membership; the raw-model regression also verifies persisted member intersection. CoreApi also resolves current active central membership. Commands and replays recheck authority around the durable mutation; reads require current view authority. Each accepted event records the tenant authorization revision observed for the command. A changing authorization revision during evaluation fails closed. Correction admission additionally requires the configured Tenant Web client (`azp`), configured strong-authentication context (`acr`), and recent provider `auth_time` (default maximum age five minutes, with bounded configuration).

HTTP routes are:

| Method and path suffix | Permission | Effect |
|---|---|---|
| `POST /api/v1/tenants/{tenantId}/orders/{orderId}/fulfillment/start` | work.record | Start against an expected committed Order revision. |
| `POST .../fulfillment/complete` | work.record | Append a good-completion quantity. |
| `POST .../fulfillment/handoffs` | handoff.record | Append pickup quantity and recipient label. |
| `POST .../fulfillment/dispositions` | work.record | Record a stopped, disputed, or defective blocker. |
| `POST .../fulfillment/resolve` | work.record | Resolve the active blocker with a reason. |
| `POST .../fulfillment/corrections` | correct | Append a correction, with high-risk admission. |
| `GET .../fulfillment` | view | Read minimum progress detail, without financial facts. |
| `GET /api/v1/tenants/{tenantId}/fulfillment` | view | Read a bounded progress inbox, without recipient labels or reasons. |
| `GET .../fulfillment/history` | view | Read bounded protected event history, including recipient labels and reasons. |

Mutation bodies are limited to 8 KiB. Inbox pages are limited to 50 and history pages to 10 events. Error responses do not disclose provider details, event reasons, or recipient labels. Order financial detail remains behind the existing Orders authority.

## Persistence, failure, and recovery

PostgreSQL is the durable authority. The `fulfillment` schema retains one work projection per tenant Order, immutable events, activity/correction linkage, and caller-scoped command receipts. Tenant-scoped keys and foreign keys bind events to their work; forced row-level security requires the current tenant setting for every table. Database guards keep source facts immutable, require consecutive projection revisions, restrict projection updates to their allowed columns, and require the new projection, event, and receipt to agree atomically at commit. The CoreApi runtime grant is limited to the required schema/table operations and named projection/activity update columns.

The store locks receipt and work mutation scope in a PostgreSQL transaction. A failed transition or stale revision writes no event or receipt. A committed event and receipt survive host restart and support replay. There is no timer, background resolver, in-memory queue, or best-effort fallback. If PostgreSQL or authorization is unavailable, the request fails; the caller may retry with the same key and intent after service recovery. Changed intent under an existing key conflicts. A retained blocker requires an explicit authorized resolution or correction; no automatic process clears it.

Operational diagnostics may record safe command outcomes and identifiers needed for troubleshooting, but must not contain recipient text or event reasons. A database restore must preserve the work projection, event history, activity links, and receipts together; a partial restore is not a supported recovery. Schema contraction is unsupported because these facts are retained.

## Qualification record

Integrated receiving is complete: the unchanged normal repository gate passed 1,347/1,347 tests across all 25 projects, with zero failures/skips/warnings/errors; CoreApi, AdminApi and DbMigrator independent publications passed on the same final runtime source. The required evidence is tracked in [COM-014/015 implementation receipt](../review/COM_014_015_IMPLEMENTATION_RECEIPT.md). Qualification requires the selected native PostgreSQL/OpenFGA regressions, the exact normal repository verification gate, and focused CoreApi/migrator publication checks to pass against the integrated source. A source implementation or a successful subset alone does not qualify the responsibility.

| Claim and owner | Falsifiable evidence | Recurring guard | Requalification trigger |
|---|---|---|---|
| Source eligibility and immutable committed Order facts — Orders and Catalog adapters | Native committed one-line program service case, uncommitted/manual-priced rejection and unchanged Order detail; pure source matrix separately rejects multi-line, converted, stock/product and unattributed facts. | Native CoreApi/PostgreSQL receiving test and Fulfillment source-rule matrix. | Source eligibility logic, Order/Catalog contracts, or commitment semantics change. |
| Exact quantities, stages, blockers, and resolution — `Application.Fulfillment` | Exact precision and zero/excess rejection; partial/split work and pickup; each blocker denies both mutations; resolution changes no quantities. | Fulfillment rule tests and native receiving journey. | Quantity, stage, disposition, or transition logic changes. |
| Durability, serialization, replay, and tenant isolation — `Application.Fulfillment.Postgres` | PostgreSQL race with two actors; replay after host restart; changed-intent conflict; cross-tenant access; transaction/receipt/projection consistency and RLS checks. | Native PostgreSQL receiving test and repository gate. | Migration, SQL, grants, RLS, provider, or receipt format changes. |
| Current membership and delegated authority — CoreApi/OpenFGA | Independent grants, absent grants, tenant member intersection, membership suspension, tuple revocation, current authorization revision, and correction step-up/initial-Owner checks. | Native OpenFGA/CoreApi authorization tests. | Permission catalog, pinned model, membership source, authorization adapter, or high-risk admission changes. |
| Protected history and safe projections — CoreApi | Detail/inbox omit financial fields, reasons, and recipient labels; history is bounded and authorized and retains correction linkage; diagnostics and errors contain no sensitive text. | Native HTTP pipeline and logging regression checks. | Response DTOs, diagnostics, pagination, or logging changes. |
| Integrated executable and migration surface — repository owners | Exact `./eng/verify.sh` run plus focused CoreApi and DbMigrator Release publications, with inspected outputs and artifact references. | Repository verification contract. | Any change to this runtime, its projects, migration, or verification inputs. |

Each row is supported by the inspected evidence in the receipt and its named recurring guards. This scope does not qualify broader production, delivery, returns, payment, customer evidence, or other COM-014/015 journeys.

## Tenant Web operator presentation

The `Fulfillment` Web page presents the existing service-work contract through
`BusinessPage`, `BusinessApiClient` and `BusinessShell`. Its directory and
Order-detail routes are `/workspace/{tenantId}/fulfillment` and
`/workspace/{tenantId}/fulfillment/orders/{orderId}`. It provides progress inbox,
protected history, start against the committed Order revision, exact completion,
pickup recipient evidence, disposition/resolution, and linked correction with
optional completion, handoff or disposition replacement. The page does not own
source eligibility, quantities, authorization or state transitions.

Viewing, work recording, handoff and correction remain independent server rights.
Loaded detail and successful command receipts supply the expected fulfillment
revision; an operator without view permission may enter a known current revision.
A successful command clears old projections and displays its retained receipt;
loading current facts is a separate authorized action. Protected reasons and
recipient labels are encoded and appear in history, rather than the inbox.

An uncertain save locks new commands and inputs until the exact frozen request
is retried. The existing envelope retains body, key, tenant and resource within
that page/session; it is not durable offline storage or restart recovery. Tenant
navigation resets presentation state. The existing fresh-identity POST includes
antiforgery protection and only its supported tenant field. It currently returns
to Billing; the page explains that return and the need to navigate back. The
server remains authoritative for current initial Owner, correction permission
and recent trusted authentication. A typed `Correct fulfillment` confirmation
reviews user intent and grants no authority.

The recurring presentation guards are focused operations tests in
`Application.Web.Tests` and `eng/test-operations-web.cjs`. The browser guard drives
the actual Blazor host against controlled identity and HTTP contract fixtures,
including uncertain saves, current revisions, encoded history, independent
permission failure, malformed responses, typed corrections and 320px reflow/axe
checks. It does not prove PostgreSQL, OpenFGA, live identity or deployment behavior.
Requalify after route/DTO, command-envelope, revision, permission, correction or
shared component changes. Integration of the shared path allowlist/navigation
and the final normal repository gate remains with the integrator; this isolated
presentation evidence does not change backend or whole-product qualification.
