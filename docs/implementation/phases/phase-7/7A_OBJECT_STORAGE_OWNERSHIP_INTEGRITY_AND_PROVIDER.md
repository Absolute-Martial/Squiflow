# Phase 7A — Object Storage Ownership, Integrity, and Provider Adapter

## Required boundary

Complete the current `IObjectStore` provider path without leaking Hugging Face types into business/application contracts.

Authoritative DB metadata should carry, as applicable:

```text
SquiFlow object identity/key
tenant ownership
business relationship
size/hash/content metadata
lifecycle/state
version/immutability evidence
provider locator/reference
```

The provider stores bytes; SquiFlow owns business identity/lifecycle.

Provider contracts or limited file concepts may exist before Phase 7. If a real earlier capability needs durable retained objects sooner, pull this ownership/integrity foundation forward rather than introducing an ad-hoc storage shortcut.

## Integrity/lifecycle

Issued/versioned bytes must not be silently overwritten. Missing-object/metadata asymmetry requires reconciliation.

## Security

Validate ownership, size/type/structure as required; do not trust user file names as storage paths; prevent cross-tenant key reuse and executable/path traversal issues.

## Provider behavior

Map timeouts/not-found/capacity/permission/provider failures into stable SquiFlow results without erasing useful diagnostics.

## Exit gate

A second adapter could implement the same SquiFlow contract without rewriting capability code, and provider-specific failure does not corrupt authoritative metadata/business state.