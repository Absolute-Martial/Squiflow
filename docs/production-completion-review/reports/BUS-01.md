# BUS-01 — Skeptical sales operator

Reviewer: `/root/business_sales_review`, GPT-6 Luna (high). Read-only review; root-edited summary of returned findings. Dynamic checks: NOT_RUN. No defect established in the existing declared priced-draft/direct-commit path.

| Finding | Classification / existing unit | Evidence | Production falsifier |
|---|---|---|---|
| Contacts/representatives and duplicate resolution are absent; create idempotency is not customer deduplication | IMPLEMENTATION_GAP; COM-002/003, with owner decision on keep-separate/consolidate | `README.IMPLEMENTATION.md:31`; `docs/development-tasks/COVERAGE_MATRIX.md:30` | Equal names remain signals; concurrent consolidation/replay cannot create cycles or destroy prior history |
| Calculation/manual entry do not implement catalog identity or published price selection | IMPLEMENTATION_GAP + OPEN_DECISION; COM-005–008 | `docs/implementation/PRICING_COMPONENT_BOUNDARY.md:71` | Conflicting/expired/missing source rejected; concurrent publication selects an explainable revision; committed prices remain unchanged |
| Optional quotation use still requires an actual quotation capability | IMPLEMENTATION_GAP + OPEN_DECISION; COM-009/010 after price policy | `docs/development-tasks/OPERATION_FLOWS.md:32` | Accept exact valid revision; competing conversions produce one consistent order link; direct orders stay usable |
| Customer-specific approval/admission is not current direct commitment | OPEN_DECISION + IMPLEMENTATION_GAP; COM-011–013 after profile/permission prerequisites | `docs/decisions/OPEN_DECISIONS.md:168`; `docs/implementation/ORDER_COMMITMENT_SLICE.md:60` | No stale/revoked approval; missing sole approver leaves explicit recovery, never automatic approval |
| Actual work/partial handoff remains absent | IMPLEMENTATION_GAP; COM-014/015 after policy admission | `docs/implementation/ORDER_COMMITMENT_SLICE.md:60` | Concurrent partial completion cannot overfulfill; artwork approval identifies one usable revision; payment never implies delivery |

The catalog already covers these outcomes. Reuse its units rather than adding duplicate projects/tasks. Permanent guards belong to capability unit tests plus real database/API races and connected COM-032 journeys. Requalify on relevant policy, lifecycle, persistence, permission or API changes. This review does not qualify a live business deployment.
