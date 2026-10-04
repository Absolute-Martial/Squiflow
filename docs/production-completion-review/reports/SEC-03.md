# SEC-03 — Adversarial quality examiner

Reviewer: `/root/quality_hostile_review`, GPT-6 Luna (high). Read-only review; root-edited summary. Dynamic checks: NOT_RUN. No current contention diagnosis or production timeout race reproduced.

| Finding | Classification / unit | Evidence | Falsifiable acceptance / guard |
|---|---|---|---|
| Admin request-budget cleanup cancels then awaits the pending task without a bound, so finally can defeat the test watchdog | Confirmed static TEST_HARNESS_GAP; bounded ADM-002 follow-up [HAR-001](../assignments/HAR-001.md) | `tests/integration/Application.AdminApi.Tests/RequestBudgetTests.cs:483`; same file `:491–500` | Noncooperative test dependency leaves pending request: cleanup/test terminates finitely and reports failure; distinguish 504 deadline from caller cancellation |
| Incoming 30-second watchdog/warmup/cleanup edits differ from independently reviewed test source | Current EVIDENCE_GAP; BAS-001/ADM-002/003/GATE-001 | `tests/integration/Application.AdminApi.Tests/RequestBudgetTests.cs:30`; `tests/integration/Application.AdminApi.Tests/PublicHealthBearerReviewTests.cs:20` | Fresh focused and normal parallel runs linked to current source digests; complete failures/skips/results; no automatic retry masking |

Root independently inspected `DrainAsync` and confirmed the unbounded awaits, including cancellation completion. This does not prove a production deadline failure. Bounded cleanup must also address cancellation callback/host disposal and observation of late completion; simply timing out an await can leave work running. No production deadline increase, blanket serialization or swallowed unexpected failure is justified.

The earlier rotating WaitAsync(5s) failure could reflect contention, a race or another resource problem. Existing warmup/wider watchdog edits are not proof of diagnosis or a passed fix. Capture the same-source failure/stage signals and verify the measured request separately from host startup.

Coverage notes remain scoped: emitted branch counts do not cover every exception filter/race ordering, real provider cancellation, live OIDC, TLS or deployment. Requalify after changed harness, deadline/cancellation pipeline, provider/host dependencies or material runtime configuration.
