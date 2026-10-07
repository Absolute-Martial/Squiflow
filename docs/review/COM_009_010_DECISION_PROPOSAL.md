# COM-009 / COM-010 quotation decision proposal

**Status:** PROPOSED; owner acceptance required before affected code/contracts.
**Baseline:** `03406c45c69f5012e19402827e3cdcfba88d41c3` plus the captured
2026-10-07 incoming commercial source manifest. Product remains `v0.0.1`.
**Runtime state:** quotation issuance, responses and conversion are
`NOT_INTRODUCED`. This proposal introduces no runtime or persistence contract.

The next requested commercial sequence is COM-009 followed by COM-010. Existing
COM-006/007 pricing and the COM-008 gap review are inputs, rather than features
to rebuild. The current focused pricing owners are
[`PRICING_POLICY_AND_PUBLICATION.md`](../implementation/PRICING_POLICY_AND_PUBLICATION.md)
and [`ORDER_CATALOG_PRICED_DRAFTS.md`](../implementation/ORDER_CATALOG_PRICED_DRAFTS.md).
Quotations stay optional; direct Orders remain independently usable.

## Proposed first issuance contract (COM-009)

| Decision | Recommended bounded contract |
|---|---|
| Fields | Summary; explicit currency; 1–100 priced lines; optional existing individual and/or organization/program attribution; required expiry instant; optional bounded offer terms. Attribution does not designate a legal debtor. |
| Prices and units | Two explicit entry modes, matching existing Orders: supplied-price lines with independent manual-pricing authority, or Catalog/Pricing-selected lines with retained item/unit/conversion/source/policy facts and independently authorized overrides. No client supplies retained evidence or approval assertions. |
| Arithmetic | Preserve decimal(19,4), four-decimal `ToEven` line rounding and sum of rounded lines. Extract the existing arithmetic into the earned neutral pricing boundary when the second consumer is implemented; preserve Orders validation, fingerprints and historical receipt compatibility. Currency remains explicit; invoice-only NPR scope does not silently restrict offers. |
| Numbering | Server-created quote GUID; a unique monotonically allocated tenant-wide number at first issue; per-quotation issued revision number and immutable revision GUID. Later issues keep the family number and append a revision. No yearly reset, organization sequence, fiscal numbering or gap-free promise. Printable formatting is presentation. |
| Validity and timezone | Issue time is authoritative UTC and becomes `validFrom`; the operator supplies `validUntil` with an explicit offset, normalized to UTC at PostgreSQL microsecond precision. Require `validUntil > issuedAt`; validity is `[issuedAt, validUntil)`. No inferred local midnight or default lifetime. Tenant business-calendar/date-only rules remain open until earned. |
| Authority | Independent quotation create, edit, view and issue permissions, in addition to the applicable manual-pricing or Catalog/Pricing permissions. Issue rechecks current source/policy/override authority under the existing database-owned commercial publication fence. Owner status alone grants no bypass. |
| Revision | Full replacement of a draft requires expected revision. Issuance freezes the complete offer, actor/time, pricing and attribution facts. A later draft does not affect the current issued offer; issuing its replacement supersedes the earlier unaccepted offer atomically. An accepted version cannot be superseded or rewritten; changed terms need a new quotation family. |
| Retry and artifacts | Caller-scoped idempotency, current authorization on replay, and atomic issue/number/revision/receipt persistence. Issued facts succeed independently of PDF rendering, object storage or notification delivery. |

Draft detail and bounded issued-history reads preserve prior facts after price,
Catalog label, customer or permission changes. Current view authority still
applies. Empty/foreign identities, unsupported units, invalid decimals, invalid
expiry and unresolved price selections fail before an effect. Issue/revise races
have one revision-checked winner; no winning command overwrites issued history.

## Proposed response and conversion contract (COM-010)

| Decision | Recommended bounded contract |
|---|---|
| Acceptance evidence | A separately authorized tenant operator records the exact issued revision, response time, bounded reason/evidence note and responding customer's claimed identity. Retain the recording operator distinctly. This is recorded external evidence, not authenticated customer-portal acceptance or a verified signature. |
| Expiry and supersession | Acceptance succeeds only for the latest unsuperseded offer while `issuedAt <= serverNow < validUntil`. Acceptance, rejection and expiry are separate revision-checked commands with retained actor/time/evidence. A read may report elapsed validity but never writes an expiry event. No scheduler is required. |
| Price revalidation | Issue is the price/policy revalidation point. Acceptance and conversion honor the exact frozen offer amount and source, without substituting later price-book values. Current tenant/account/action authority still applies; no historical pricing permission authorizes a new override. |
| Conversion | Convert only an accepted immutable revision, with independent quotation-convert plus Orders-create authority. Create one linked priced Order draft; commitment remains a separate command. Accepted evidence remains valid for conversion after the acceptance window closes. |
| Atomic boundary | The owned conversion/link/order-create/receipt operation commits in one PostgreSQL transaction through a reviewed Orders application/store surface, with a unique quotation-revision-to-order link. Never use two independent commits or private cross-module table access from CoreApi. |
| Concurrency and recovery | A second authorized conversion, even with another key/caller, returns the already linked Order. Response loss is resolved through retained receipts/link identity. Cancellation/crash before commit leaves neither Order nor conversion link; replay after commit preserves both. |

Accepted quotation origin is a distinct authoritative pricing fact, not a
price-book tier. COM-010 must deliberately extend Orders' current-source
revalidation branch so conversion/commitment preserves the accepted offer. The
trusted owning capability supplies this fact through a reviewed in-process
contract; arbitrary client quotation IDs never grant a price or authority.

## Review and implementation boundary

Acceptance of these choices authorizes COM-009 first, then COM-010 only after its
dependency is implemented and verified. Write the accepted choices into the
focused quotation owner and current/open decision records before affected
persistence or wire semantics. Any requested alternative remains open until
settled; none of this proposal is an accepted decision merely because it is in Git.

Permanent checks must cover exact expiry boundaries, supersession/acceptance
races, immutable historical reads, current permissions on replay, pricing changes,
real PostgreSQL numbering/rollback/receipt atomicity, cross-tenant denial and
duplicate conversion with crash/response-loss recovery. Run the exact normal
`./eng/verify.sh` after each integrated scope; requalify on authority, pricing,
time, numbering, schema, serialization or cross-capability transaction changes.

Adaptive workflows still need tenant-profile prerequisites and one selected
variation before COM-011–013. Fulfillment needs its admission/approval decisions
before COM-014–015. COM-016–018 require explicit case selection. OPS-013 remains
the distinct general durable-business-audit slice. Live object storage remains
blocked; this proposal earns no storage/delivery or whole-product qualification.
