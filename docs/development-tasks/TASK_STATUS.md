# Current task status and completed-work reconciliation

This is an orchestration snapshot, not a runtime qualification owner. Read [baseline notes](BASELINE_NOTES.md), [source observations](source-observations.json) and [verification result](VERIFICATION_RESULT.md). HEAD alone does not identify uncommitted work. Your newer ZIP can supersede this snapshot; repeat BAS-001 before assignment.

| Work observed in source | Assignment treatment | Remaining work / evidence |
|---|---|---|
| AdminApi JSON-kind correction at the baseline commit | Preserve; no duplicate implementation task | Existing guards recur during baseline verification |
| Membership invitation, transitions, initial Owner and tenant suspension/reactivation | ADM-001 is VERIFY_EXISTING | Inspect real PostgreSQL/OpenFGA regressions on the supplied source; resolve conflicting blocked wording through evidence |
| Incoming AdminApi request-timeout configuration, pipeline and RequestBudgetTests | ADM-002 is VERIFY_EXISTING | Qualify timeout, caller cancellation, no-store, permit release, audit and replay; do not reimplement the middleware |
| Orders direct commitment and independent Customers individual billing records | COM-001 preserves these | Broader pricing, fulfillment, billing and settlement remain distinct tasks |
| Invoice issue owner and accepted/open decision updates | COM-019 closes only remaining decisions | Allocation/cardinality, organization-reference anchor/gaps, business-date authority and inactive-individual eligibility remain owner decisions unless a newer accepted record resolves them |
| Incoming Application.Invoices and its unit-test project | COM-020 is VERIFY_EXISTING | Review/test present invariants and then add only accepted contract deltas; no second project or reconstruction |
| Invoice store port and retained-fact contracts | Existing neutral boundaries are inputs | They do not establish PostgreSQL invoice issuance, numbering, a receivable, billing authority at HTTP ingress or a working issued-invoice journey; COM-021–025 cover those effects |
| No implemented tenant or Admin Web runtime observed | WEB/UIA remain assignment candidates | Topology decisions can precede backend completion; runtime work waits the named gates |

Do not mark a task accepted because an endpoint, folder, test file or owner document exists. Acceptance requires the relevant property, reviewer and exact baseline evidence. A passing local repository command does not qualify a live identity provider, browser/device topology, deployment or recovery drill.

The [session template](SESSION_TEMPLATE.json) records PENDING, ASSIGNED, SUBMITTED, REVIEWED, ACCEPTED, EVIDENCE_PENDING or NEEDS_CORRECTION. Catalog statuses describe scheduling and stay separate from these live outcomes. Keep accepted decisions, returned ZIP digests and failed/unrun checks with each handoff. If only part of a task is complete, assign its remaining bounded slice under ORCHESTRATOR.md rather than rebuilding the whole task.

The catalog agents reached a usage limit after writing their sections. Their written files were subsequently inspected and validated; a failed review agent supplies no review evidence. The catalog does not change production-owner gate states or resolve your open business decisions.

Fresh receiving-environment evidence: AdminApi 56/56 and Invoices 15/15 passed, but the normal repository gate returned exit 1 with 11 IdentityAccess PostgreSQL tests failing during Testcontainers ResourceReaper initialization. Keep ADM-002/COM-020 as verification assignments with this evidence; neither the whole baseline nor durable invoice operation is accepted. OPS-022 is a new required correction/qualification task before GATE-001. Do not repeat completed middleware or neutral invoice implementation to repair the unrelated harness.

Subsequent independent [ADM-002 review](../../artifacts/verification/adm-002/reviewer/REVIEW.md) supports the bounded OCE/IO deadline correction: 39 focused passes, three fresh 11-case passes, and 23 routing/budget passes, with their explicit coverage limits. It separately reproduced bearer-bearing public liveness invoking native authentication outside the protected deadline: 0 passed / 1 failed / 0 skipped, exit 1. This activates the bounded [ADM-003.public-auth correction](../../artifacts/orchestration/assignments/ADM-003-public-auth.md) before GATE-001. [OPS-022 root focused checks](../../artifacts/verification/ops-022/root-final-review.json) now record final actual startup/abort/SIGTERM cleanup and separate synthetic optimized negative validation; the combined normal gate/coverage remains pending and the original failed gate is preserved. Live assignment and build-lease state belongs to `artifacts/orchestration/foundation-session.json`.
