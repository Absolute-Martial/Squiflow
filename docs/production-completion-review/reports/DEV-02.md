# DEV-02 — Transaction and compatibility skeptic

Reviewer: `/root/development_persistence_review`, GPT-6 Luna (high). Read-only review; root-edited summary. Dynamic checks: NOT_RUN. No concrete existing Orders/Customers transaction or tenant-session defect found in the bounded inspection.

| Finding | Classification / unit | Evidence | Production acceptance / guard |
|---|---|---|---|
| Corrected neutral invoice behavior is not independently/currently qualified | EVIDENCE_GAP; COM-020/GATE-001 | `docs/implementation/INVOICE_ISSUE_CONTRACT.md:4`; `README.IMPLEMENTATION.md:132` | Independent hostile port/fact review and current normal gate; no persistence claim from in-memory tests |
| Four accepted choices are prerequisites of durable invoice adapter | OPEN_DECISION + IMPLEMENTATION_GAP; COM-019 then COM-021 | `docs/implementation/INVOICE_ISSUE_CONTRACT.md:231` | Real PostgreSQL numbering/allocation/receipt races, rollback, ambiguous commit, RLS and privilege-denial tests |
| Orders receipts have forward-reader compatibility, not proved mixed-version rollout/rollback | EVIDENCE_GAP for deployment; OPS-015/020/021 | `docs/implementation/ORDER_DRAFT_INTAKE_SLICE.md:88` | Qualify a drained upgrade or compatible reader strategy and explicitly exercise old binary after new receipt writes. Root maps Orders compatibility to OPS-020/021, not invoice-only COM-021 |
| Detail reads already use one RepeatableRead snapshot | No current defect established | `modules/orders/Application.Orders.Postgres/Persistence/PostgresOrderDraftStore.Queries.cs:37`; `tests/integration/Application.Orders.Postgres.Tests/OrderMigrationAndRlsTests.cs:183` | Preserve deterministic concurrent header/line regression; historical ReadCommitted concern is obsolete |
| No outbox for current business commands; owner explicitly excludes Worker consequences | NOT_INTRODUCED; OPS-001/002/003 | `docs/implementation/ORDER_DRAFT_INTAKE_SLICE.md:189` | First selected consequence commits with effect/receipt; restart discovers work even if wakeup never happened |
| Narrow runtime grants/session/RLS exist, broader deployment profile remains unqualified | EVIDENCE_GAP for future full profile; OPS-016 | `deploy/database/grant-core-api-runtime.sql:18`; `docs/implementation/ORDER_DRAFT_INTAKE_SLICE.md:107` | Real separate-login negative privileges and registered-module checks for exact delivered schema |

Requalify on transaction/session, historical format, grants/migrations or rollout changes. No generic repository, second data service, new DB provider or rewrite is justified by this review.
