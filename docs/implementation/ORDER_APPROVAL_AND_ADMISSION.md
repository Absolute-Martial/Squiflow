# Order approval, continuation and admission

**Status:** COM-012 and the bounded COM-013 consumer are locally `PRODUCTION_HONEST`, `BLOCKED = none` for their declared scope. The [qualification receipt](../review/COM_012_013_IMPLEMENTATION_RECEIPT.md) records the exact receiving source and checks. Product remains `v0.0.1`. The [owner-accepted contract](../review/COM_012_013_DECISION_PROPOSAL.md) selects the exact scope; this document owns its operational meaning.

## Applicability and retained facts

`RequireApprovalForProgramOrders` defaults false. A program-attributed draft pinned to a policy requiring approval needs an independent approval of its exact revision before commitment. Approval binds its priced lines, total/currency, customer attribution, quotation origin, external reference and retained profile/policy. Approval itself neither revises nor commits an Order. Quotation customer acceptance, price override authority and commercial-source revalidation keep their existing meanings.

New typed policies and profiles use `tenant-policy/v2` and `tenant-profile/v2`. Supported historical v1 profiles remain readable and impose no approval requirement; their reference setting remains effective. An unprofiled draft remains fail-closed until explicit authorized optional-baseline assignment under the [reference-policy owner](ORDER_PROGRAM_REFERENCE_POLICY.md). A legacy baseline must be optional for both settings. No old policy, pin, Order or command receipt is rewritten. Existing `profiles.policy.edit/publish` delegates are authorized to manage the new typed setting, while private profile publication/activation remains separate.

Any business draft revision, including reference editing, makes an earlier approval stale for admission. New publication/activation affects new work and preserves existing pins. Later suspension of an approver does not erase a valid historical decision; making/replaying a decision checks that actor's current authority, while commitment checks its own actor's current authority. No approval-revocation operation is introduced. A later request or rejection of unchanged content does not retroactively revoke an earlier valid approval of that same exact subject.

## Actors and continuation

One named active member with `orders.approvals.view`, `orders.approvals.decide` and `orders.view` can decide an assigned request. The requester and the account that last created/revised/reference-edited the exact subject revision cannot normally approve it. Missing or contradictory provenance fails closed. Owner status supplies no business grant, and combining multiple permissions in one role does not bypass the domain self-approval check.

Requests have separate positive revisions and states Pending, Approved, Rejected, Canceled and Superseded. Submission requires expected Order revision. Decisions, reassignment and cancellation require expected request revision. A fresh submission may atomically supersede a stale pending request only with its expected request revision; a current pending request cannot be replaced silently. Terminal facts stay immutable. Rejection retains the Order draft and requires a reason; a later submission creates a separate request.

An authorized requester may cancel their own pending request under `orders.approvals.submit`, even after viewing rights are removed. The current initial Owner with `orders.approvals.recover` may reassign or cancel under the existing recent Tenant Web authentication checks; protected recovery inspection also requires the appropriate viewing rights. Recovery never approves the Owner's own work. If no independent eligible actor exists, work remains pending with an actionable reason or is canceled until the existing protected identity/membership/grant/Owner recovery path restores an eligible actor. Uninterrupted progress without an available eligible actor is not claimed.

Pending work is durably discoverable in a bounded protected API inbox. Approval-view alone reveals only IDs, assignments, revision/state and stale/unavailable reasons for the caller's submitted/assigned work. Current Owner recovery visibility additionally requires recovery authority. Full subject and history require Order-view too. Inbox/command summaries omit prices, lines, reference text and recorded decision reasons. Queries never secretly supersede stale work. Discovery does not require step-up; recovery mutations do. No timer, automatic approval, notification channel or Web/Workstation UI is introduced.

Reasons preserve case/internal whitespace after outer trim, contain 1–512 Unicode scalar values when required, and reject malformed UTF-16 and controls before trim. Payloads are bounded at 8 KiB. Inbox pages contain at most 50 items; history pages at most 10. Cursors are bounded, tenant-bound and treated as untrusted input. Business reasons and subjects are absent from ordinary diagnostics.

## Protected HTTP contract

All routes are under `/api/v1/tenants/{tenantId}` and require current active account/membership plus the named independently delegated permission. Mutations require one bounded `Idempotency-Key`; command replies contain only request/Order IDs, revisions and state. Responses are `no-store`.

| Route | Authority and input |
|---|---|
| `POST /orders/{orderId}/approvals` | Submit + approval-view + Order-view; `expectedOrderRevision`, `assignedAccountId`, optional `expectedPendingRequestRevision` for explicit stale replacement. |
| `GET /order-approvals` | Approval-view; `limit`, optional tenant-bound `after`, optional `recovery=true` with current Owner recovery authority. |
| `GET /order-approvals/{requestId}` and `GET .../history` | Approval-view + Order-view and requester/assignee/current authorized Owner scope; history accepts `limit` and `beforeRevision`. |
| `POST .../{requestId}/approve` or `/reject` | Decide + approval-view + Order-view; `expectedRequestRevision`, optional `reason` (required for rejection). |
| `POST .../{requestId}/reassign` | Current Owner + recovery + recent Tenant Web authentication; `expectedRequestRevision`, `assignedAccountId`, required `reason`. |
| `POST .../{requestId}/cancel` | Submit and actual requester identity; `expectedRequestRevision`, required `reason`. |
| `POST .../{requestId}/recover-cancel` | Current Owner + recovery + recent Tenant Web authentication; `expectedRequestRevision`, required `reason`. |
| Existing `POST /orders/{orderId}/commit` | Current `orders.commit`; expected Order revision; denies missing/stale approval with `409 order_approval_required` or `order_approval_stale`. |

The existing Order action guide reports missing/stale approval only after current commit permission passes. It is advisory: commitment always repeats admission under the transaction lock.

## Transaction, compatibility and deployment

Orders owns approval meaning, immutable subjects/history/receipts and commitment evidence in its existing capability and PostgreSQL adapter. It reuses central PostgreSQL, forced tenant RLS, embedded parameterized SQL, native row locks and existing host/provider mechanisms. No new vendor dependency, project, database, process or generic workflow engine is earned. A host port supplies current membership, independent OpenFGA decisions, initial-Owner and recent authentication evidence; authorization administration continues to own grants rather than business approvals.

Submission, transitions and commitment lock the same Order row. State/assignment changes and their semantic receipt are atomic; concurrent decisions/reassignment/revision have one valid winner. Receipt failure or cancellation before commit rolls back effects. Response loss after commit requires authorized replay of the original semantic result. Request, Order and authorization revisions remain distinct. Commitment validates exact approved subject, retained policy/reference and current commercial compatibility before its one frozen-Order effect, then retains immutable approval evidence with the commitment receipt. PostgreSQL additionally guards immutable approval subjects/events and required commitment evidence.

New Orders adapter timestamps use UTC microsecond precision so relational headers and JSON receipts agree after restart. Historical receipt timestamps are compared at their stored database precision without rewriting those receipts. Exact subjects compare decimal values rather than JSON number formatting; changing a price, quantity or other subject fact still invalidates approval.

Deploy the ordered DbMigrator first, apply the checked-in CoreApi grant script, publish/pin the revised immutable OpenFGA model, explicitly delegate approval rights, then publish/activate the required profile for new work. New writer receipt versions 7/8 retain explicit policy/commit evidence; supported historical versions and literal receipts stay readable. Unknown, contradictory or incomplete stored authority fails closed. Safe rollback selects a compatible retained profile for new work; destructive removal of retained schema/evidence is rejected.

## Evidence and requalification

The unchanged normal `./eng/verify.sh` passed 1,282/1,282 tests across 23 projects on 2026-10-09, with zero failures/skips and zero Release warnings/errors. Independent CoreApi, AdminApi and DbMigrator build/publish checks passed on the identical frozen source. Permanent guards are the Orders approval unit/native suites, Profiles v1/v2 provider fixtures, CoreApi protected endpoint tests, native OpenFGA checks, actual HTTP/PostgreSQL/OpenFGA admission journey, database-role provisioning guards and the unchanged normal repository gate. Affected CoreApi/AdminApi/DbMigrator hosts require independent build/publish checks. Local checks do not qualify remote CI, deployment, live identity/storage providers or the complete product.

Requalify on approval/policy/schema meaning, permission/model changes, eligibility/self-approval/recovery rules, subject/provenance/receipt interpretation, row-lock or transaction ownership, migration/grants, resource/disclosure bounds and quotation/commercial-fact protection. Fulfillment, credit, invoice persistence, payment, notifications, dynamic forms, runtime implementation switching and general OPS-013 audit remain separate responsibilities.
