# Customer contacts and representative relationships

Product version: v0.0.1. Focused owner: Customers. Implemented for COM-002.

This slice extends the existing Customers capability. It does not restore a generic
Parties model and it does not make an application login account, customer individual,
representative, approver or debtor interchangeable.

## Owned behavior

- POST /api/v1/tenants/{tenantId}/customers/individuals/{individualId}/contact
  replaces the bounded current display name, email and phone fields under
  expectedRevision plus Idempotency-Key. A successful change advances the
  individual revision and records the changing account and time.
- Equal names, emails and phone values remain legal across different individual
  records. They are not uniqueness constraints or automatic duplicate evidence.
- An active customer individual can be linked explicitly as a representative of one
  tenant-owned organization or one valid child program of that organization. A link
  has its own stable identity, active/inactive availability and positive revision.
- POST .../customers/organizations/{organizationId}/representatives creates the
  active relationship. GET .../representatives/{representativeId} reads only the
  relationship metadata. POST .../representatives/{representativeId}/unlink
  deactivates it under expectedRevision; it does not delete retained history.
- An inactive individual cannot be linked as an active representative. An organization
  and program mismatch is rejected. Cross-tenant identities are not resolved through
  the relationship.
- At most one active relationship for the same individual and exact organization or
  organization/program target is allowed. Concurrent link attempts converge on that
  one active relationship; concurrent edit/unlink attempts use revision checks so one
  transition wins.

Representative metadata grants no login, tenant membership, operator action, approval,
billing or debtor authority. Those meanings require their own capability and permission.

## Authorization and disclosure

The current account and tenant membership are established before the pinned OpenFGA
operation check on every request and retry. The following permissions are independent:

- customers.individuals.contact.edit / individual_contact_editor;
- customers.representatives.view / representative_viewer;
- customers.representatives.manage / representative_manager.

Each effective relation is intersected with current tenant membership. Revoking a
permission blocks a later retry even when a durable command receipt exists. Provider
failure fails closed through the existing CoreApi authorization path. Representative
responses expose relationship identity, target identity, availability/revision and
transition time only; they do not disclose the linked individual's name, email or phone.
Contact-edit responses are available only through the independent contact-edit action.

## Durability and database boundary

Individual contact edits reuse the existing tenant-owned individual row and add
contact-change actor/time facts. Caller/operation-scoped receipts retain the successful
contact result so an exact retry is stable even after later changes.

Representative relationships and their command receipts live in the Customers schema.
PostgreSQL foreign keys require the tenant-owned organization and individual, and an
additional parent constraint requires a supplied program to belong to that organization.
Partial unique indexes protect the one-active-link invariant. Both representative tables
use enabled and forced tenant RLS. Runtime grants are restricted to the contact columns
needed on customers.individuals and availability/revision/change facts on
customers.representatives; runtime deletion is not granted. All SQL uses parameters.

The COM-002 migration is additive. Its down path refuses to discard retained contact
change or representative history. Existing organization/program, individual create/read/
availability and order attribution identities remain unchanged.

## Permanent regression evidence

Recurring guards include:

- CustomerIndividualTests and CustomerRepresentativeTests;
- real CustomerContactsRepresentativesPostgresTests plus the existing Customers
  PostgreSQL isolation/privilege/migration tests;
- CustomerIndividualEndpointTests and CustomerRepresentativeEndpointTests;
- OpenFgaTenantAuthorizationTests against the real pinned-model server path;
- the repository architecture tests and normal eng/verify.sh gate.

The PostgreSQL tests exercise concurrent contact edits, concurrent representative links,
concurrent unlinks, deterministic link-versus-deactivation serialization at the active-
individual row lock, stale revisions, same-key replay, inactive individuals, invalid program
parentage, cross-tenant reads, and actual PostgreSQL outage failure for contact edit, link
and unlink. The host tests exercise independent permissions, permission revocation before
replay and personal-data non-disclosure.

The exact normal repository gate on 2026-10-06 passed locked restore, formatting,
the Release build with zero warnings/errors, and all 789 tests across the solution
with zero failures or skips. This requalifies the declared COM-002 source scope in
the current receiving tree; it does not qualify duplicate resolution, onboarding
import, catalog identity, adaptive pricing, deployment or remote CI.

## COM-002 non-claims and subsequent owners

COM-002 does not introduce duplicate scoring, automatic merge, survivor selection,
keep-separate resolution, onboarding import, addresses, portal login, invitations,
representative role/approval semantics, debtor assignment, credit or settlement effects.

Those non-claims describe COM-002 itself, not absence of later runtime. The owner
closed COM-003/004 on 2026-10-06; the current duplicate/forward-canonicalization and
bounded non-retaining import runtime is owned by
`CUSTOMER_DUPLICATES_AND_IMPORTS_SLICE.md`. Full COM-004 still lacks its accepted
raw-source retention/OPS-008 path, not format/sample/product decisions.

Catalog/unit decisions are separately owned by
`CATALOG_AND_UNIT_BOUNDARY.md`; calculation/manual-entry compatibility stays in
`PRICING_COMPONENT_BOUNDARY.md`, and accepted adaptive policy/publication is owned by
`PRICING_POLICY_AND_PUBLICATION.md`. These later scopes are implemented and locally
qualified as documented by their focused owners; COM-002 does not redefine them.

## Requalification triggers

Requalify when individual contact fields or normalization change; representative target or
availability semantics change; a representative gains business authority; authorization
relations/model/membership sequencing change; command receipt shape changes; PostgreSQL
keys, RLS, SQL or runtime grants change; or a later duplicate/import flow rewrites these
identities.
