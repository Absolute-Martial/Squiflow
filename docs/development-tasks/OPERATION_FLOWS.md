# End-to-end operating flows

This guide connects the assignments to real operation. It is a proposed complete backend + two-Web delivery scope; it does not assert that missing operations or screens exist. [Current truth](../../README.IMPLEMENTATION.md) and focused owners govern actual behavior. Task IDs identify dependencies and evidence, not invented future HTTP routes.

## 1. Separate identities and authority

The tenant is the business operating the product. Its customer may be an organization, child program or independent individual billing record. A signed-in operator is neither automatically the customer nor the debtor. A Platform Admin controls the platform through AdminApi; a Tenant Owner operates within tenant authority through CoreApi. Neither membership nor a brand/profile grants permission by itself.

Each protected command establishes current account and tenant/platform authority, checks the specific permission, validates its bounded input and expected revision, and invokes the owning capability. Retryable mutations commit effects and receipts together. Guidance and frontend visibility explain observed choices; they never substitute for current command admission.

## 2. Platform onboarding and access

| Step / actor | Operation and authoritative result | Assignments / current distinction |
|---|---|---|
| Infrastructure operator | One-shot bootstrap binds an exact external admin identity and registered device, reconciles the pinned platform relation and retains audit | Existing AdminBootstrap; BAS-001 and ADM-001 preserve it; no first-login/public bootstrap |
| Platform administrator | Sign in through selected ZITADEL topology and prove the active registered Admin device; AdminApi rechecks platform authority | Existing API boundary; live topology ADM-004; later UIA-001–003 |
| Platform administrator | Create tenant, import an already existing human identity, link identities, establish membership and protected initial Owner | Existing narrow commands qualified by ADM-001; supported reads ADM-006; UIA-004–005 consume them |
| Authorized administrator | Publish and reconcile allowed grants/revocations; report pending/uncertain states until OpenFGA effects are known | ADM-008–013 and ADM-032; membership and permissions remain separate |
| Authorized tenant operator | Enter only a current active membership, then view or execute specifically granted capabilities | CoreApi existing account/workspace boundary; WEB-001–002 after GATE-002 |
| Authorized operator | Publish settings/profile, activate a compatible revision and explain resulting features | ADM-018–024; WEB-012 and UIA-007 consume their distinct authority |
| Authorized platform operator | Add/revoke operators or devices, rotate certificate and use the approved recovery ceremony | ADM-014–017; UIA-006/008; never recover by rerunning bootstrap |

If a tenant/account/device is suspended or authority is revoked, new protected operations fail under current checks. A denial on replay does not erase an already committed effect. Restore current authority, then retry the same semantic key to discover its receipt; never manufacture a new key to evade uncertainty.

## 3. Organization/program commercial journey

| Step / daily actor | Business action | Facts/effects that must be retained | Assignment chain / frontend |
|---|---|---|---|
| Customer administrator | Establish organization, its programs and optional separate individual billing records; add supported contacts | Tenant-owned identities and explicitly versioned relationships; no implicit login binding or debtor assignment | Existing Customers + COM-001–004; WEB-003 |
| Catalog/pricing operator | Define the supported product/service and unit, publish applicable price selection and permitted manual overrides | Chosen source/revision, explanation, authorized override reason and required approval under accepted policy | COM-005–008; WEB-015; manual pricing permission applies to initial and later price entry |
| Order operator | Preview and create priced draft; revise or abandon under current authority and revision | Draft intent, priced snapshots, attribution, actor and caller-scoped receipt | Existing drafts preserved; COM-007/008; WEB-004 |
| Sales operator/customer representative | Optionally issue an offer, record response to the exact issued revision and convert accepted offer without duplicate orders | Immutable quotation facts, validity/response/conversion identity | COM-009–010; WEB-006; direct orders remain available |
| Authorized requester/approver | Resolve applicable typed customer/program guidance and any required approval; discover who acts next | Pinned compatible rule/form/workflow policy and accepted human decision; unavailable-approver recovery | COM-011–012; WEB-016 and WEB-006; no uploaded arbitrary server code |
| Order committer | Admit a current direct or quoted order after required policy checks | Immutable committed priced content and attribution, exact revision and receipt; no implied invoice or fulfillment | Existing commitment + COM-013; WEB-005 |
| Production/fulfillment operator | Record actual work, artwork approval, partial quantities, discrepancies, readiness and pickup/delivery | Actual performed quantities and stages, responsible actor and linked evidence | COM-014–015; WEB-007; stage names/rules require the selected case, not a fixed universal pipeline |
| Billing operator | Choose organization default, exact attributed independent program or separate individual debtor; issue from eligible committed NPR facts | Frozen debtor and prices, tenant-wide invoice identity, organization-scoped reference, accepted business date, issue timestamp and receipt | COM-019–022; WEB-010; four contract decisions precede durable issuance |
| Receivables/payment operator | Record recognized payment outcome, allocate partial amounts to eligible receivables, retain unapplied amount and reconcile | Invoice-backed receivable, payment/allocation/reversal facts and derived outstanding balance | COM-023–025; WEB-010; recording payment is not automatically full settlement |
| Authorized correction operator | Cancel before an effect or apply the appropriate later credit, return or refund | Linked corrective facts, remaining quantities/balances and reconciled external result | COM-028–030; WEB-011; no deletion/rewrite of issued history |
| Viewer/accountant | Explain current balance, prior prices, debtor, documents and history | Controlled projections of authoritative retained facts | COM-031; OPS-009/013; WEB-011 |

These are dependency relationships, not a mandatory business-time sequence. Supported deposits/payment-before-fulfillment, partial handoffs or multiple invoices require their accepted contracts. The catalog does not invent their allocation or accounting policy. Quotation capability and actual fulfillment belong to full requested completion; quotation use is optional on each order.

Initial invoice currency is NPR, displayed as रु. Prices use decimal(19,4), round each line to four decimals using ToEven, then sum. Issued debtor/prices are frozen. Tax is outside the initial invoice scope; this is not a tax exemption or fiscal/legal compliance claim.

## 4. Conditional commercial branches

| Supported branch | Required extra behavior | Tasks and disposition |
|---|---|---|
| Supplier-performed work | Record actual supplied input, supplier work/cost and payable linked to customer work | COM-016–017; WEB-008 and relevant WEB-007 portion |
| Tracked items | Distinguish precise stock, availability-only and non-stock/service; record concurrent movements and damage | COM-018; WEB-009; do not decrement fictitious stock for services |
| External payment provider | Separate intent, confirmed/failed/unknown outcome and safe reconciliation before allocation | COM-026 + selected OPS durable execution; no automatic second charge |
| Credit sale | Enforce accepted terms and current shared exposure with race-safe admission | COM-027; cached balance cannot grant credit |
| Managed artwork/import bytes | Authorize upload, bound bytes, verify hash, quarantine/scan and controlled retention | OPS-007/008 and COM-004/015 |
| External customer portal | Establish separate customer identity, exposed actions/documents and account authority | WEB-013 decision; tenant operator credentials/permissions are not reused |

The owner must select each branch or record why it is absent and its activation trigger. A conditional label is not permission to declare a requested branch complete without implementing it.

## 5. Supporting consequences and operation

A business transaction commits first. An independent delivery/rendering consequence owns durable intent/outbox, then Worker claims it with leases/fencing and bounded fair execution (OPS-001–004). Channels or actor mailboxes are transient wake/dispatch only. Selected Quartz scheduling materializes a unique durable occurrence/job (OPS-005), rather than becoming a second owner of business completion.

Document rendering consumes frozen issued facts and retains version/hash; print or notification failure does not undo an invoice (OPS-009/010). Notification failure leaves discoverable in-product continuation. Metering uses durable/reconcilable usage and strict reservation only where the accepted resource needs it (OPS-011/012); telemetry is not usage or financial authority.

AdminApi controls actual jobs through explicit pause/drain/retry/quarantine capabilities (OPS-006), never unrestricted database or framework dashboards. Logs/traces are safe operational evidence (OPS-014); authoritative audit remains capability-owned (OPS-013, ADM-023). Separate provisioner, migrator, API and Worker identities remain least privileged (OPS-016).

Independent builds/CI and stable consumer contracts are OPS-017/021. Deployment, keys/secrets, trusted edge, restore, load and failed-release recovery are OPS-015/018–020. Their target evidence is required before the corresponding production claim; local unit tests are insufficient.

## 6. Connected completion checks

1. GATE-001 verifies the actual incoming baseline and honest unresolved boundaries.
2. COM-032 proves the selected commercial cases through owned APIs, including negative and uncertain outcomes.
3. GATE-002 accepts qualified backend contracts and operation before frontend runtime implementation.
4. WEB-014/GATE-003 prove the tenant operator journey in an actual browser.
5. UIA-009/GATE-004 independently prove the private Admin journey and denial boundaries.
6. GATE-005 combines the declared backend + Web scope, target operations and explicit branch dispositions.

Use [functionality acceptance](FUNCTIONALITY_ACCEPTANCE.md) for exact scenario obligations. Workstation/Sync/Guard remain visible future product scope in the coverage matrix. Finishing these Web/backend tasks alone does not unlock the product-version gate.
