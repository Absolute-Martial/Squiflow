#!/usr/bin/env python3
"""Read-only ADM-004 ZITADEL Cloud topology qualification.

Secrets are accepted only through environment variables and are never printed.
This tool intentionally performs no provider mutation.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from pathlib import Path

MAX_RESPONSE_BYTES = 64 * 1024
TIMEOUT_SECONDS = 10


class QualificationError(RuntimeError):
    pass


@dataclass(frozen=True)
class Inventory:
    issuer: str
    tenant_project_id: str
    tenant_audience: str
    admin_project_id: str
    admin_audience: str
    tenant_redirect_uris: tuple[str, ...]
    admin_redirect_uris: tuple[str, ...]
    verifier_role: str
    verifier_role_evidence_sha256: str
    recovery_inventory_sha256: str
    recovery_binding_sha256: str


def fail(message: str) -> None:
    raise QualificationError(message)


def required_text(value: object, path: str) -> str:
    if not isinstance(value, str) or not value.strip():
        fail(f"{path} must be a non-empty string")
    return value.strip()


def required_sha256(value: object, path: str, *, allow_placeholder: bool) -> str:
    text = required_text(value, path)
    if allow_placeholder and text.startswith("REPLACE_WITH_"):
        return text
    if len(text) != 64 or any(ch not in "0123456789abcdefABCDEF" for ch in text):
        fail(f"{path} must be a 64-character SHA-256 value")
    return text.lower()


def load_inventory(path: Path, *, allow_placeholders: bool) -> Inventory:
    try:
        document = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"inventory cannot be read: {exc}")
    if document.get("schemaVersion") != 1:
        fail("schemaVersion must equal 1")
    issuer = required_text(document.get("issuer"), "issuer").rstrip("/")
    parsed = urllib.parse.urlsplit(issuer)
    if parsed.scheme != "https" or not parsed.hostname or parsed.username or parsed.password or parsed.query or parsed.fragment:
        fail("issuer must be an absolute HTTPS origin without credentials, query or fragment")

    tenant = document.get("tenantAccess")
    admin = document.get("platformAdmin")
    verifier = document.get("identityVerifier")
    recovery = document.get("recoveryEvidence")
    if not isinstance(tenant, dict) or not isinstance(admin, dict) or not isinstance(verifier, dict) or not isinstance(recovery, dict):
        fail("tenantAccess, platformAdmin, identityVerifier and recoveryEvidence objects are required")

    inventory = Inventory(
        issuer=issuer,
        tenant_project_id=required_text(tenant.get("projectId"), "tenantAccess.projectId"),
        tenant_audience=required_text(tenant.get("apiAudience"), "tenantAccess.apiAudience"),
        admin_project_id=required_text(admin.get("projectId"), "platformAdmin.projectId"),
        admin_audience=required_text(admin.get("apiAudience"), "platformAdmin.apiAudience"),
        tenant_redirect_uris=tuple(required_text(v, "tenantAccess.redirectUris[]") for v in tenant.get("redirectUris", [])),
        admin_redirect_uris=tuple(required_text(v, "platformAdmin.redirectUris[]") for v in admin.get("redirectUris", [])),
        verifier_role=required_text(verifier.get("declaredProviderRole"), "identityVerifier.declaredProviderRole"),
        verifier_role_evidence_sha256=required_sha256(
            verifier.get("roleAssignmentEvidenceSha256"),
            "identityVerifier.roleAssignmentEvidenceSha256",
            allow_placeholder=allow_placeholders),
        recovery_inventory_sha256=required_sha256(
            recovery.get("inventorySha256"),
            "recoveryEvidence.inventorySha256",
            allow_placeholder=allow_placeholders),
        recovery_binding_sha256=required_sha256(
            recovery.get("knownSubjectBindingEvidenceSha256"),
            "recoveryEvidence.knownSubjectBindingEvidenceSha256",
            allow_placeholder=allow_placeholders),
    )
    if inventory.tenant_project_id == inventory.admin_project_id:
        fail("Tenant Access and Platform Admin must use distinct ZITADEL project IDs")
    if inventory.tenant_audience == inventory.admin_audience:
        fail("Tenant and Platform Admin API audiences must differ")
    if "OWNER" in inventory.verifier_role.upper() or "WRITE" in inventory.verifier_role.upper():
        fail("identity verifier declared role must be narrow/read-only, not an owner/write role")
    if not allow_placeholders:
        for label, value in {
            "issuer": inventory.issuer,
            "tenant project": inventory.tenant_project_id,
            "tenant audience": inventory.tenant_audience,
            "admin project": inventory.admin_project_id,
            "admin audience": inventory.admin_audience,
            "verifier role": inventory.verifier_role,
        }.items():
            if "REPLACE_WITH_" in value or "example." in value:
                fail(f"live qualification cannot use placeholder {label}")
    validate_redirects(inventory)
    return inventory


def validate_redirects(inventory: Inventory) -> None:
    if not inventory.tenant_redirect_uris or not inventory.admin_redirect_uris:
        fail("both tenant and admin redirect inventories must be non-empty")
    for uri in inventory.admin_redirect_uris:
        parsed = urllib.parse.urlsplit(uri)
        if parsed.scheme != "https" or not parsed.hostname:
            fail(f"Admin redirect must use HTTPS: {uri}")
    for uri in inventory.tenant_redirect_uris:
        parsed = urllib.parse.urlsplit(uri)
        loopback = parsed.hostname in {"127.0.0.1", "::1"}
        if parsed.scheme != "https" and not (parsed.scheme == "http" and loopback):
            fail(f"Tenant redirect must use HTTPS or native loopback HTTP: {uri}")


class SameOriginRedirectHandler(urllib.request.HTTPRedirectHandler):
    """Refuses to follow a redirect that leaves the origin of the original request.

    urllib's default opener replays the request headers, including the live verifier
    bearer credential, against whatever host a redirect names. Qualification must never
    disclose that credential to another origin, so a cross-origin redirect is a hard
    failure rather than a followed hop.
    """

    def redirect_request(self, req, fp, code, msg, headers, newurl):
        original = urllib.parse.urlsplit(req.full_url)
        target = urllib.parse.urlsplit(newurl)
        if (original.scheme, original.hostname, original.port) != (
                target.scheme, target.hostname, target.port):
            raise urllib.error.HTTPError(
                newurl,
                code,
                "refused cross-origin redirect during provider qualification",
                headers,
                fp,
            )
        return super().redirect_request(req, fp, code, msg, headers, newurl)


QUALIFICATION_OPENER = urllib.request.build_opener(SameOriginRedirectHandler())


def get_json(url: str, bearer: str | None = None) -> tuple[int, dict[str, object]]:
    headers = {"Accept": "application/json"}
    if bearer:
        headers["Authorization"] = f"Bearer {bearer}"
    request = urllib.request.Request(url, headers=headers, method="GET")
    try:
        with QUALIFICATION_OPENER.open(request, timeout=TIMEOUT_SECONDS) as response:
            body = response.read(MAX_RESPONSE_BYTES + 1)
            status = response.status
    except urllib.error.HTTPError as exc:
        body = exc.read(MAX_RESPONSE_BYTES + 1)
        status = exc.code
    except (urllib.error.URLError, TimeoutError) as exc:
        fail(f"provider request failed safely: {type(exc).__name__}")
    if len(body) > MAX_RESPONSE_BYTES:
        fail("provider response exceeded the 64 KiB qualification bound")
    try:
        value = json.loads(body) if body else {}
    except json.JSONDecodeError:
        fail(f"provider returned non-JSON response with status {status}")
    if not isinstance(value, dict):
        fail(f"provider returned non-object JSON with status {status}")
    return status, value


def same_origin(left: str, right: str) -> bool:
    lhs = urllib.parse.urlsplit(left)
    rhs = urllib.parse.urlsplit(right)
    return (lhs.scheme.lower(), lhs.hostname, lhs.port or 443) == (rhs.scheme.lower(), rhs.hostname, rhs.port or 443)


def subject_fingerprint(subject: str) -> str:
    return hashlib.sha256(subject.encode("utf-8")).hexdigest()


def qualify_discovery(inventory: Inventory) -> dict[str, object]:
    status, document = get_json(f"{inventory.issuer}/.well-known/openid-configuration")
    if status != 200:
        fail(f"OIDC discovery returned status {status}")
    if document.get("issuer") != inventory.issuer:
        fail("OIDC discovery issuer does not exactly match inventory issuer")
    endpoint_names = ("authorization_endpoint", "token_endpoint", "jwks_uri")
    endpoints: dict[str, str] = {}
    for name in endpoint_names:
        endpoint = required_text(document.get(name), f"discovery.{name}")
        parsed = urllib.parse.urlsplit(endpoint)
        if parsed.scheme != "https" or not parsed.hostname:
            fail(f"discovery.{name} must use HTTPS")
        if not same_origin(inventory.issuer, endpoint):
            fail(f"discovery.{name} must use the configured issuer origin")
        endpoints[name] = endpoint
    return {"status": status, "issuer": inventory.issuer, "httpsEndpoints": sorted(endpoints)}


def qualify_user(inventory: Inventory, token: str, subject: str, expected_human: bool) -> dict[str, object]:
    encoded = urllib.parse.quote(subject, safe="")
    status, document = get_json(f"{inventory.issuer}/v2/users/{encoded}", token)
    if status != 200:
        fail(f"known {'human' if expected_human else 'machine'} subject lookup returned status {status}")
    user = document.get("user")
    if not isinstance(user, dict):
        fail("provider user response is missing user object")
    if user.get("userId") != subject:
        fail("provider userId does not exactly equal requested subject")
    is_human = isinstance(user.get("human"), dict)
    if is_human != expected_human:
        fail(f"subject {subject!r} human classification did not match expectation")
    return {
        "status": status,
        "subjectSha256": subject_fingerprint(subject),
        "kind": "human" if is_human else "non-human",
    }


def qualify_unknown(inventory: Inventory, token: str, subject: str) -> dict[str, object]:
    encoded = urllib.parse.quote(subject, safe="")
    status, _ = get_json(f"{inventory.issuer}/v2/users/{encoded}", token)
    if status == 200:
        fail("unknown-subject probe unexpectedly resolved a provider user")
    # Only a genuine not-found is evidence that the subject is unknown. A rejected
    # verifier credential, a throttled request or a provider outage is not, and treating
    # those as a pass would let live qualification succeed without ever confirming the
    # subject is unresolved.
    if status != 404:
        fail(
            "unknown-subject probe could not confirm the subject is unknown: "
            f"expected 404 from the provider but observed {status}"
        )
    return {
        "status": status,
        "subjectSha256": subject_fingerprint(subject),
        "result": "not-found-as-required",
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--static-only", action="store_true")
    args = parser.parse_args()

    try:
        inventory = load_inventory(args.inventory, allow_placeholders=args.static_only)
        evidence: dict[str, object] = {
            "schemaVersion": 1,
            "static": {
                "issuer": inventory.issuer,
                "tenantProjectDistinctFromAdmin": True,
                "tenantAudienceDistinctFromAdmin": True,
                "redirectInventoryValidated": True,
                "declaredVerifierRole": inventory.verifier_role,
                "verifierRoleEvidenceSha256": inventory.verifier_role_evidence_sha256,
                "recoveryInventorySha256": inventory.recovery_inventory_sha256,
                "recoveryBindingSha256": inventory.recovery_binding_sha256,
            },
        }
        if args.static_only:
            evidence["live"] = "NOT_RUN_STATIC_ONLY"
        else:
            token = os.environ.get("ZITADEL_API_TOKEN", "").strip()
            human = os.environ.get("ZITADEL_HUMAN_SUBJECT", "").strip()
            machine = os.environ.get("ZITADEL_MACHINE_SUBJECT", "").strip()
            unknown = os.environ.get("ZITADEL_UNKNOWN_SUBJECT", "adm004-does-not-exist").strip()
            if not token or not human or not machine or not unknown:
                fail("live qualification requires ZITADEL_API_TOKEN, ZITADEL_HUMAN_SUBJECT and ZITADEL_MACHINE_SUBJECT")
            evidence["live"] = {
                "discovery": qualify_discovery(inventory),
                "human": qualify_user(inventory, token, human, expected_human=True),
                "machine": qualify_user(inventory, token, machine, expected_human=False),
                "unknown": qualify_unknown(inventory, token, unknown),
            }
        print(json.dumps(evidence, indent=2, sort_keys=True))
        return 0
    except QualificationError as exc:
        print(json.dumps({"qualification": "FAILED", "reason": str(exc)}, sort_keys=True), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
