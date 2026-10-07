# Commercial starting point — COM-001 preservation map

**Task:** COM-001
**Scope:** source-backed inventory only; no new commercial business effect is introduced by this document.

This map records the current commercial entry points that later Phase 02 work must preserve. Current focused owners and source outrank the planning catalog when the catalog status is stale.

## Preserved customer responsibilities

- Application.Customers owns tenant-scoped customer meaning. It does not restore the former generic Parties model.
- Customer organizations and child programs have stable identities and immutable parentage. CoreApi exposes create/read/bounded-browse operations under independent organization/program permissions.
- Customer organization/program creation uses caller/account-scoped semantic idempotency. The PostgreSQL adapter commits the entity and receipt together.
- Customers PostgreSQL sessions set transaction-local tenant context; runtime SQL also carries explicit tenant predicates and the tables use enabled/forced RLS.
- Individual customer records are separate from IdentityAccess login accounts. The current record contains bounded display name, optional email/phone, active/inactive availability and a positive revision.
- Individual creation and availability changes retain caller-scoped receipts. Availability mutation is expected-revision checked and records the changing account/time.
- Current individual creation, viewing and availability mutation use separate individual_creator, individual_viewer and individual_availability_editor relations. Availability responses deliberately do not disclose contact fields.
- Equal names, email addresses or phone numbers are not current uniqueness constraints. No duplicate/customer-merge behavior is currently implied.

Focused owners:

- docs/implementation/CUSTOMER_ORGANIZATION_PROGRAM_ATTRIBUTION_SLICE.md
- docs/implementation/CUSTOMER_INDIVIDUAL_BILLING_RECORD_SLICE.md

Recurring evidence already present:

- tests/unit/Application.Customers.Tests
- tests/integration/Application.Customers.Postgres.Tests
- tests/integration/Application.CoreApi.Tests

## Preserved order and pricing responsibilities

- Orders owns priced draft create/read/browse/revise/abandon and direct commitment.
- Order creation and price preview require create plus manual-pricing authority. Full priced replacement requires edit plus manual-pricing authority, including replay.
- Direct commitment freezes the current priced content and customer/program attribution, advances revision and records the committing account/time. It does not issue an invoice, reserve/fulfil work or record payment.
- Existing order receipts, PostgreSQL tenant predicates/RLS and historical read contracts remain authoritative for the effects they already protect.
- Draft customer/program context is attribution only. It is not legal debtor, account balance, credit or settlement authority.
- Existing decimal arithmetic remains decimal 19,4 with each line rounded to four decimals using ToEven before document summation.

Focused owners:

- docs/implementation/ORDER_COMMITMENT_SLICE.md
- docs/implementation/ORDER_DRAFT_INTAKE_SLICE.md
- docs/implementation/PRICING_COMPONENT_BOUNDARY.md

## Incoming invoice responsibility

Application.Invoices is present and must not be recreated. Its current qualified meaning is host-neutral invoice-issue orchestration over committed Orders: current billing-authority and issue-store ports, replay/idempotency flow, committed-order/revision/arithmetic validation, Customers-backed debtor resolution and immutable issued-fact contracts.

PostgreSQL invoice persistence, numbering/business-date implementation, an invoice HTTP/OpenFGA surface and a durable invoice runtime are not introduced by the current slice. Those absences are distinct from the implemented host-neutral orchestration.

Focused owner: docs/implementation/INVOICE_ISSUE_CONTRACT.md.

## Permission and provider sequencing to preserve

Protected CoreApi customer/order operations establish a validated account and current tenant membership before the operation-specific pinned-model OpenFGA check. Current permission is rechecked on retry; a retained receipt does not bypass revoked authority. Provider failure fails closed.

Capability business behavior remains host-neutral. CoreApi establishes transport/security context and maps explicit contracts; it does not become the Customers/Orders business implementation.

## Phase 02 dependency disposition

- **COM-002:** dependencies are satisfied by current authority. GATE-001 is accepted in current repository instructions, and the current decision/security owners record the first ADM-008 delegation contract as selected/implemented even though the original planning task file still says DECISION_REQUIRED.
- **COM-003:** still requires an owner decision for matching inputs, survivor authority and whether the first release consolidates or only records keep-separate resolution. No merge semantics are inferred here.
- **COM-004:** remains conditional. OPEN_DECISIONS.md still lists initial customer-data import/migration scope, and no accepted input sample/encoding/column/version/batch contract is present. No generic ETL or guessed CSV contract is authorized.

## Material responsibility states

| Responsibility | State after COM-001 |
|---|---|
| Existing Customers organization/program operations | PRODUCTION_HONEST within their focused owner scope |
| Existing individual create/read/availability operations | PRODUCTION_HONEST within their focused owner scope |
| Existing priced Order draft/direct commitment behavior | PRODUCTION_HONEST within its focused owner scope |
| Host-neutral invoice issue orchestration | PRODUCTION_HONEST within its deliberately partial focused owner scope |
| Customer contact editing and representative relationships | NOT_INTRODUCED before COM-002 |
| Duplicate suggestion/consolidation | NOT_INTRODUCED; owner decision required before COM-003 |
| Customer onboarding import | NOT_INTRODUCED; conditional input contract absent before COM-004 |

## Requalification triggers

Re-run the owning focused regressions and normal repository gate when customer record shape/lifecycle, organization/program parentage, permission relations/model, receipt formats, RLS/SQL/grants, order commitment/pricing semantics or invoice issue contracts change. A future merge/import must additionally qualify its own reference/history and retry/recovery guarantees.

## Verification

Source inspection completed against the current local tree. Dynamic verification is recorded only after an inspected ./eng/verify.sh run; no historical test result is promoted to evidence for this task.
