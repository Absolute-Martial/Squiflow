# COM-011 accepted contract

**Status: accepted by the owner on 2026-10-08.** The customer-supplied program reference meaning and the profile/authority prerequisites are closed as decisions. The bounded implementation subsequently received local qualification under [the implementation receipt](COM_011_IMPLEMENTATION_RECEIPT.md). [Order program reference policy](../implementation/ORDER_PROGRAM_REFERENCE_POLICY.md) is the focused behavior owner; the accepted prerequisite record is [COM-011 and profile decisions](COM_011_PROFILE_PREREQUISITE_DECISION_PROPOSAL.md).

## Accepted behavior

An Order attributed to a validated tenant-owned program requires one customer-supplied reference before commitment only when the active pinned profile selects `RequireReferenceForProgramOrders = true`. The default is false. The internal `ProgramId` does not satisfy the requirement, and Orders without a program remain optional.

The Order-owned `ExternalProgramReference` is trimmed at the edges, case-preserving, limited to 128 Unicode scalar values, and rejects Unicode controls and malformed UTF-16. Blank is absent. The value is evidence of supplied text, not a verified customer instruction. The required field is allowed to be absent during draft creation and quotation conversion; a draft may be revised through the metadata-only reference command before commitment.

The reference edit uses current `orders.edit`, expected Order revision, and semantic idempotency. Commitment uses current `orders.commit` before protected reads or replay. A required-but-missing reference returns `409 order_program_reference_required` with no successful transition/receipt effect. Missing or incompatible profile authority fails closed.

For quote-origin drafts, reference-only changes retain the accepted quotation's summary, currency, total, customer attribution, priced lines/source facts, origin, and immutable conversion snapshot. Commitment still proves the accepted quotation facts; editing the reference cannot change commercial terms.

New direct drafts pin the active compatible profile at creation and quote conversions pin it at conversion. Existing pins are retained across later publication or activation. Unprofiled legacy drafts remain fail-closed until an authorized Platform Admin compare-and-set assigns the retained false baseline with an immutable actor/device receipt. That assignment changes profile authority metadata only: it does not revise the Order or rewrite earlier Order receipts.

## Authority and profile boundary

Tenant policy view/edit/publish is a Web → CoreApi tenant operation under separately delegated `profiles.policy.view`, `profiles.policy.edit`, and `profiles.policy.publish`. These rights are independent of Owner status and Order permissions. Private AdminApi separately publishes and activates profile snapshots under `can_publish_tenant_profile` and `can_activate_tenant_profile`, with current Platform Admin identity/device/entry/authorization checks.

The first profile schema is `tenant-profile/v1`, with a fixed all-always-enabled/nonselectable catalog of `customers.organizations`, `customers.programs` → organizations, `orders.drafts`, and `orders.program-attribution` → drafts + programs. The profile retains exact catalog/selection and published-policy revisions plus profile publication actor/device/time and authorization evidence. Catalog selection grants no permission and does not disable features. Policy publication creates immutable policy evidence but does not activate it; profile activation applies it to new work.

## Evidence and status

This accepted decision itself does not qualify runtime. The introduced bounded profile and Order responsibilities subsequently received local qualification under the [implementation receipt](COM_011_IMPLEMENTATION_RECEIPT.md), including the exact normal gate and real provider/host guards. Existing live object-storage/provider blockers are separate. Product version stays `v0.0.1`.
