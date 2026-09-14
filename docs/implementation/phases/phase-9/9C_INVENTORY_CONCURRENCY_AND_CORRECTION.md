# Phase 9C — Inventory Concurrency and Correction

## Scope

Implement only the inventory modes required by actual business flows, such as precise quantity, availability-only, service/non-stock and damaged/unusable adjustment.

Do not introduce reservation/MRP/BOM/lot/wastage systems without a real requirement.

Inventory concepts, catalog links, read views or simple non-authoritative UI may exist before Phase 9. Phase 9 is the point where authoritative shared-stock mutation must satisfy the protected concurrency/correction model below.

## Authority

Shared current stock is centrally authoritative. Workstation local state may support UX/provisional intent but cannot become final current stock authority while offline.

## Concurrency

Use the database/application mechanism required by the inventory invariant: expected version, atomic conditional update, constraint/isolation/lock as justified. Generic last-write-wins is not acceptable.

## Correction

Stock mistakes/returns/damage use explainable adjustments/corrections rather than silent history rewrite.

## Tests

Concurrent final-unit sale/adjustment, duplicate operation, stale Workstation stock, server response loss, rejected pending adjustment, correction after fulfillment.

## Exit gate

Concurrent authoritative inventory operations preserve the invariant and historical correction trail.