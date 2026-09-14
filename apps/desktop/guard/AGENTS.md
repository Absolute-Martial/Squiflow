# Guard Instructions

These rules apply below `apps/desktop/guard/` in addition to the root instructions.

## Role

`SquiFlow.Guard` is a small independent Workstation supervision/recovery coordinator. Its process boundary exists because it must be able to observe/recover Workstation failure from outside the Workstation process.

## Guard may own

- launching/supervising Workstation;
- bounded restart budgets/backoff;
- intentional-shutdown distinction;
- bounded lifecycle/crash/resource evidence;
- update handoff/coordination when that phase is implemented;
- triggering/coordination of separate maintenance/diagnostics helpers when those boundaries are later earned.

## Guard must remain independent of business meaning

- It does not decide Orders/Customers/Inventory rules.
- It does not query/mutate business SQLite state directly as an application service.
- It does not access PostgreSQL.
- It does not perform OpenFGA business authorization.
- It does not become a server Worker/scheduler.
- It does not permanently hold Workstation DEKs, central KEKs, OpenBao tokens, or recovery shares.

## Reliability rules

- Restart/backoff loops must be bounded and observable.
- Distinguish expected user/application shutdown from crash loops.
- Guard failure must not corrupt Workstation business state.
- Workstation failure must not require Guard to reconstruct business state from its own memory.
- Heavy work should move to an on-demand helper process only when isolation/resource behavior earns that process boundary.

## DO NOT

- Do not add business module references merely to “help” recovery.
- Do not add ORM/central DB dependencies.
- Do not make Guard a key vault, migration engine, backup engine, telemetry daemon, document processor, or synchronization engine.
- Do not optimize for an arbitrary tiny memory number by deleting required supervision/recovery behavior; measure real resource use.

## Validation

Test Workstation crash with Guard alive, Guard crash with Workstation alive, both restarted, repeated startup failure reaching the restart budget, intentional shutdown, invalid Workstation executable path/configuration, and bounded evidence under crash loops.
