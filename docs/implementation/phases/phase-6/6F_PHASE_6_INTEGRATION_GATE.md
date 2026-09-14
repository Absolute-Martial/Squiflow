# Phase 6F — Integrated Phase-6 Gate

Phase 6 is complete enough when:

- Admin Web talks directly to an independent private Admin API;
- Admin authentication/device/private-network/application authorization are separate gates;
- material Admin operations have durable authoritative audit;
- highest-risk operations can require JIT/physical/recovery/four-eyes controls according to policy;
- Worker accepts only durably recorded work and implements bounded claim/retry/quarantine/drain behavior;
- scheduler, if introduced, creates durable occurrences/jobs rather than being truth itself;
- Core/Admin/Worker failure-independence tests pass;
- background/provider consequences use owning capability semantics and idempotency;
- no generic privileged force-success/raw-SQL/raw-counter bypass exists.

Passing Phase 6 qualifies the independent Admin/Worker process foundations for current workloads. Admin and Worker remain expandable; later phases may add more controls/workload classes without changing these foundations.