# COM-011 remaining contract proposal

The owner selected and clarified the first case on 2026-10-08: customer-supplied
reference text on an Order already attributed to a program must be present before
Order commitment. An existing internal `ProgramId` does not replace this text.
The [workflow owner](../workflow/WORKFLOW_DESIGN.md) records that accepted meaning.

Everything below is proposed for owner acceptance. No COM-011 runtime, durable
profile authority or production qualification is claimed.

## Proposed bounded contract

| Concern | Proposed behavior |
|---|---|
| Field | One Order-owned `ExternalProgramReference` text value; display label “Program reference”. The operator records customer-supplied text. It is evidence of supplied information, not proof of customer approval, payment or an externally verified purchase order. |
| Validation | Trim outer whitespace; blank becomes absent; retain case and internal whitespace; reject Unicode control characters and malformed Unicode; maximum 128 Unicode scalar values. No uniqueness, external lookup, regex rules, automatic default or free-form field schema. |
| Applicability | One typed profile setting `RequireReferenceForProgramOrders`: the default definition is `false`; the selected required variant is `true`. In the required variant, every Order with a validated tenant-owned `ProgramId` needs reference text. Non-program Orders remain optional. No per-program exemption or organization hierarchy is proposed; making program attribution itself mandatory would be a separate decision. |
| Attribution changes | A direct draft whose Program changes evaluates the new attribution against the policy in the profile already pinned to that draft, in the same revision-checked replacement. A quoted draft retains its accepted attribution. Changing attribution is not a way to modify a quotation-origin Order. |
| Draft entry | Reference may be absent during draft creation, quotation issuance/acceptance and conversion. Draft guidance explains the missing reference. There is no extra canonical lifecycle state or approval stage. |
| Reference edit | A narrow draft-only reference edit uses current `order_editor`, expected Order revision and semantic idempotency, with no manual-price permission. It updates only this metadata, revision and retained history. The same operation works for direct and quoted Orders; committed/abandoned Orders cannot be edited. |
| Commitment | Current `order_committer` authorization precedes protected reads and replay. Missing required reference denies commitment with `409 order_program_reference_required`, without successful transition/receipt effects. Guidance exposes the corresponding reason under existing Order access rules. Missing, incompatible or unavailable profile authority fails closed with a safe compatibility/unavailability outcome. |
| Quotation protection | Reference editing never changes price, lines, customer attribution, origin or the original immutable conversion snapshot. Commitment still proves the complete accepted quotation facts. Conversion retries return the original linked Order as COM-010 already requires. |
| Policy versions | A new direct draft pins the active compatible profile at creation; a converted Order pins it at conversion. Later publication applies to new work. Existing drafts retain their pin, with no automatic migration. Grandfather pre-existing drafts only if the owner accepts that choice. ADM-021 must durably assign them a real retained baseline revision through an explicit revision-checked transition; that baseline does not exist today. Until that transition is qualified, absent profile authority fails closed. Requiring the new policy for old drafts instead needs an explicit migration/revalidation contract. Successful commitment freezes reference and policy evidence. |
| Read/history compatibility | Protected Order detail/history retain the supplied text, stable field meaning and exact policy/profile revision. New optional fields are absent from historical payloads that never recorded them; supported old receipts remain readable. Reference text is excluded from ordinary diagnostic logs and rendered as text at the output boundary. |

This is one typed required-field policy with constant bounded evaluation. It does
not require arbitrary expressions, tenant scripts, custom forms, workflow
engines, timers, notifications or approval orchestration. Unknown definition
versions and duplicate setting definitions are rejected before
publication. Retiring a program or definition preserves retained evidence.

## Publication authority choice

Propose tenant Web administration → CoreApi for this tenant-owned policy's
edit/validate/publication operations, consistent with the accepted Web-only
workflow authoring boundary. Use independent, explicitly granted policy-editor
and policy-publisher authority; neither tenant Owner status nor an Order business
permission grants these operations automatically. Exact permission identifiers
and delegation ceilings need acceptance. No tenant Web UI is claimed here.

Platform Admin owns admitted catalog/ceiling and profile-control operations in
ADM-018–021, separately from tenant business-policy authoring. If private AdminApi
publishes or activates a platform-owned profile containing that tenant policy,
it must check its exact operation authority and reference accepted immutable
policy evidence. Platform entry/device verification alone does not authorize
all operations. The [Admin surface owner](../admin/ADMIN_SURFACES.md) preserves
these distinct authorities. Profile publication/activation actors and how this
policy's published evidence enters their snapshot remain prerequisite decisions.

## Durable prerequisites and receiving evidence

COM-011 cannot consume the current in-memory feature compiler as durable profile
authority. The prerequisite sequence is ADM-018 → ADM-019 → ADM-020 → ADM-021:
admit the smallest shipped feature/permission vocabulary, persist this one typed
setting, publish an immutable compatible profile, then atomically activate and
resolve it for this consumer. ADM-020/021 also require receiving evidence for
ADM-009/011. Their feature identifiers, platform ceiling, dormant-grant behavior,
profile schema, publication and activation contracts remain decisions in their
own task cards; acceptance of this COM-011 proposal does not silently close them.

Use the existing data-only variation path; do not introduce ADM-022 runtime
composition. Disablement must not bypass a requirement retained by existing work
or delete its data. A compatible rollback changes routing for new work while
preserving retained policy and committed facts.

## Required proof before qualification

Prove default and program-required behavior through real CoreApi/PostgreSQL
paths; missing/blank/invalid/oversize values; cross-tenant Program references;
current authorization on edits, commitment and retries; metadata idempotency and
revision races; activation versus create/convert/commit races; policy retention
after retirement/restart; and safe authority failures. For quoted Orders, prove
reference edit followed by commitment preserves accepted origin, summary,
currency, total, attribution and every line/source fact, and leaves the immutable
original conversion snapshot plus conversion retry/history evidence intact. Run the exact normal
repository gate and applicable host publishes after implementation. A fake policy
seam alone does not qualify durable profile authority or atomic enforcement.

## Decisions requested

Accept or revise the bounded contract, including independent tenant policy
publication authority and the proposed grandfathering of pre-existing drafts. The accepted reference meaning is already closed. ADM-018–021 decisions
and receiving qualification remain a separate prerequisite review.

Reviewed with GPT-6 Luna at high effort. The proposal was corrected to preserve
tenant Web authoring, cover every program Order in the required variant, make
legacy baseline assignment explicitly unimplemented, and protect all accepted
quotation facts. This is a design review, not runtime qualification.
