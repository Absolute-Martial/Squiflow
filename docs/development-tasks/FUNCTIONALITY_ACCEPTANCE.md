# Connected functionality acceptance

Use this with [operating flows](OPERATION_FLOWS.md), [task index](TASK_INDEX.md) and [coverage](COVERAGE_MATRIX.md). These are future acceptance obligations, not test results. Exact financial/date/numbering and policy expectations come from accepted focused owners. An open decision blocks the affected scenario; do not guess its expected outcome to write a passing test.

For each executed scenario record: compatible source/configuration digest, task IDs, actor/permissions, initial authoritative state, request/semantic key/revision, observed HTTP or browser result, resulting durable facts, denied side effects, evidence location and regression guard. Keep customer content, tokens, secrets and certificates out of evidence exports. A mock may prove a host response but cannot qualify database, identity, provider, browser or restore behavior.

| Scenario | Functionality and falsifiable assertions | Owning assignments |
|---|---|---|
| F01 Private administration entry | Exact admin identity plus registered active device plus current platform permission succeeds; tenant Owner, wrong device and forged forwarded headers cannot enter | ADM-001/004/015/016/034; UIA-001/002 |
| F02 Onboard a tenant and operator | Provision tenant, bind verified identity and establish membership through owned paths; same-key retry cannot create another account/tenant/member; conflicting identity ownership fails safely | ADM-001/004/006; UIA-004/005 |
| F03 Authority changes | Pending grant/revoke is not reported applied before external state is known; replay/restart converges; membership suspension blocks access independently of tuples; last Owner/device safeguards remain intact | ADM-008–017/032/034 |
| F04 Tenant/customer boundary | Same-looking customer/order identifiers in different tenants never cross reads, commands, caches, documents or statements; individual billing identity never becomes a login account | COM-001/002/020; WEB-003 |
| F05 Personalized pricing | Effective supported customer/program policy resolves deterministically; retained source/revision explains price; initial and later manual entry require independent pricing authority; unauthorized/over-limit overrides cannot commit | COM-005–008; WEB-004/015 |
| F06 Direct order | Create, preview, revise and commit without a quotation; stale expected revision and changed-intent key conflict; committed facts reject draft replacement and retain historical prices | COM-001/013; WEB-004/005 |
| F07 Optional quotation | Issue immutable offer revision, accept/reject/expire under the selected date policy and retry conversion; concurrent/repeated acceptance cannot manufacture duplicate order effects | COM-009/010; WEB-006 |
| F08 Guided approval | Missing required information/authority yields explainable blocked action; approval is durable and version-bound; rejection/reassignment/unavailable actor have a recovery path; notification failure cannot erase continuation | COM-011/012/013; WEB-006/016 |
| F09 Partial actual fulfillment | Record supported work and handoff quantities, artwork approval and discrepancy; duplicate/stale/concurrent commands cannot exceed accepted quantities; invoicing or payment alone cannot mark work delivered | COM-014/015; WEB-007 |
| F10 Invoice debtor alternatives | Organization default, exact independent program and explicit individual choices preserve source tenancy; issuing operator is not inferred as debtor; rename/default changes do not alter issued facts | COM-019–022; WEB-010 |
| F11 Financial fact lock and number race | NPR and ToEven four-decimal line arithmetic survive serialization; source revision, debtor, date, reference and receipt commit atomically; race/failure obey accepted allocation/numbering rules | COM-020/021/022 |
| F12 Partial payment and settlement | Record recognized payment, allocate partial amounts, retain unapplied funds and read consistent outstanding balance; concurrent allocation, over-allocation and cross-debtor allocation obey accepted rules; retries do not double-pay | COM-023–025; WEB-010 |
| F13 Correction branches | Before-effect cancellation differs from credit/return/refund after an effect; partial correction retains remaining balances/quantities; no issued invoice rewrite or assumed external refund success | COM-028–030; WEB-011 |
| F14 Historical explanation | Current authorized viewer retrieves original prices, debtor, actors and document revision after policy/customer changes; history read neither mutates state nor leaks other tenant data | COM-031; OPS-009/013; WEB-011 |
| F15 Unknown mutation outcome | Lose response or hit deadline after possible commit; unchanged same-key replay returns retained effect under current authority, while a denial/provider outage never authorizes a replacement key | ADM-002; COM-013/021/024/025; OPS-021 |
| F16 Crash/restart and competing workers | Kill before dispatch, during execution and after effect; stale lease/fencing owner cannot finalize newer work; retry/quarantine/fairness remain bounded; actor supervision is not financial retry authority | OPS-001–006 |
| F17 File/document/notification failure | Oversize/unsafe content is rejected or quarantined; hash/tenant ownership agrees; lost provider acknowledgement becomes explicit uncertainty; committed commercial state survives rendering or delivery failure | OPS-007–010; COM-015 |
| F18 Usage and operational evidence | Competing final-unit reservations do not bypass strict accepted limit; restored/reconciled counters agree; disabled telemetry cannot erase authoritative usage/audit; metrics contain no unbounded tenant/content labels | OPS-011–014 |
| F19 Web security and interruption | OIDC callback/CSRF/session expiry and cross-tenant switching are safe; circuit/reconnect/browser loss shows no false success; no business/token authority in browser storage; keyboard/labels/focus remain usable | WEB-001/002/014; UIA-001/002/008/009 |
| F20 Independent hosts | Tenant Web/CoreApi failure does not grant, impersonate or become an ordinary runtime dependency of AdminApi/Admin Web; each introduced host builds from its graph and interoperates through owned external contracts | OPS-017/021; UIA-009 |
| F21 Recovery and release | Restore effects and receipts together with object, authorization, usage and selected job state; selected provider migration and compatible rollback/roll-forward pass an authenticated smoke journey | OPS-015–020; GATE-005 |

## Conditional scenario selection

Select and prove purchasing/payable/partial supplier payment (COM-016), outsourced work/cost linkage (COM-017), inventory tracking modes and damage races (COM-018), provider payment uncertainty/reconciliation (COM-026), shared credit exposure (COM-027), custom domains (ADM-027), privileged support/key operations (ADM-028–030) and external customer portal authority (WEB-013) when included in the supported release case. Mark absence with an accountable reason and activation trigger; never substitute a happy-path direct sale for a selected complex case.

## Functionality review order

1. Static source review traces request → authority → capability → transaction/provider → receipt → response. Check every sibling entry point and historical format, not only a screen.
2. Pure tests verify business rules; real database tests verify transactions, races, RLS and restricted roles; provider tests verify actual authority and unknown outcomes.
3. HTTP integration verifies ordering, bounded parsing, no-store, safe errors and executable contracts.
4. Browser tests verify the same real API journey, session security, guidance and recovery; UI tests cannot redefine commercial truth.
5. Target load/fault/restore/release drills verify operating claims. Inspect exact outputs and maintain the recurring guards.

The scenario matrix checks connected ability and operability, not merely endpoint counts. Complete development requires accepted evidence for all required and selected conditional cases under the gates; writing this file closes none of them.
