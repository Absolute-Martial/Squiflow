#!/usr/bin/env python3
"""Static regression checks for ADM-004 qualification tooling."""
from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TOOL = ROOT / "eng" / "qualify-zitadel-topology.py"
EXAMPLE = ROOT / "deploy" / "zitadel" / "adm-004-topology.example.json"

spec = importlib.util.spec_from_file_location("qualify_zitadel_topology", TOOL)
assert spec and spec.loader
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)


def expect_failure(document: dict, contains: str) -> None:
    with tempfile.TemporaryDirectory() as directory:
        path = Path(directory) / "inventory.json"
        path.write_text(json.dumps(document), encoding="utf-8")
        try:
            module.load_inventory(path, allow_placeholders=True)
        except module.QualificationError as exc:
            if contains not in str(exc):
                raise AssertionError(f"expected {contains!r}, got {exc!r}") from exc
        else:
            raise AssertionError(f"expected qualification failure containing {contains!r}")


def main() -> int:
    base = json.loads(EXAMPLE.read_text(encoding="utf-8"))
    inventory = module.load_inventory(EXAMPLE, allow_placeholders=True)
    assert inventory.tenant_project_id != inventory.admin_project_id
    assert inventory.tenant_audience != inventory.admin_audience

    same_project = json.loads(json.dumps(base))
    same_project["platformAdmin"]["projectId"] = same_project["tenantAccess"]["projectId"]
    expect_failure(same_project, "distinct ZITADEL project IDs")

    same_audience = json.loads(json.dumps(base))
    same_audience["platformAdmin"]["apiAudience"] = same_audience["tenantAccess"]["apiAudience"]
    expect_failure(same_audience, "API audiences must differ")

    unsafe_admin_redirect = json.loads(json.dumps(base))
    unsafe_admin_redirect["platformAdmin"]["redirectUris"] = ["http://admin.example.invalid/callback"]
    expect_failure(unsafe_admin_redirect, "Admin redirect must use HTTPS")

    unsafe_tenant_redirect = json.loads(json.dumps(base))
    unsafe_tenant_redirect["tenantAccess"]["redirectUris"] = ["http://tenant.example.invalid/callback"]
    expect_failure(unsafe_tenant_redirect, "Tenant redirect must use HTTPS or native loopback HTTP")

    owner_role = json.loads(json.dumps(base))
    owner_role["identityVerifier"]["declaredProviderRole"] = "IAM_OWNER"
    expect_failure(owner_role, "narrow/read-only")

    write_role = json.loads(json.dumps(base))
    write_role["identityVerifier"]["declaredProviderRole"] = "USER_WRITE"
    expect_failure(write_role, "narrow/read-only")

    print("ADM-004 topology static regressions: PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
