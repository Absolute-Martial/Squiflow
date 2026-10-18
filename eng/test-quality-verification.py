#!/usr/bin/env python3
"""Check orchestration/failure handling with fixtures; never run product builds or browsers."""
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
HOSTS = {
    row[0]: (row[1], row[2].split(";"))
    for line in (ROOT / "eng/hosts.tsv").read_text().splitlines()
    if line and not line.startswith("#")
    for row in [line.split("\t")]
}

FAKE_DOTNET = r'''#!/usr/bin/env python3
import json, os, sys
from pathlib import Path
args = sys.argv[1:]
with open(os.environ["FAKE_DOTNET_LOG"], "a") as log:
    log.write(json.dumps(args) + "\n")
if args[0] == os.environ.get("FAKE_DOTNET_FAILURE"):
    sys.exit(17)
if args[0] == "publish":
    output = Path(args[args.index("--output") + 1])
    output.mkdir(parents=True, exist_ok=True)
    for suffix in ("dll", "deps.json", "runtimeconfig.json"):
        if suffix != os.environ.get("FAKE_MISSING_OUTPUT"):
            content = "" if suffix == os.environ.get("FAKE_EMPTY_OUTPUT") else "fixture"
            (output / (Path(args[1]).stem + "." + suffix)).write_text(content)
'''

FAKE_BROWSER_GUARD = r'''
const fs = require('node:fs');
const path = require('node:path');
const output = process.env.APPLICATION_UI_ARTIFACTS;
fs.mkdirSync(output, { recursive: true });
const journeys = { 'test-business-web.cjs': ['business', 'business-browser-results.json'], 'test-quotation-web.cjs': ['quotations', 'quotation-browser-results.json'], 'test-operations-web.cjs': ['operations', 'operations-browser-results.json'], 'test-tenant-administration-web.cjs': ['administration', 'tenant-administration-browser-results.json'], 'test-customer-operations-web.cjs': ['customers', 'customer-operations-browser-results.json'], 'test-signed-in-entry-browser.cjs': ['entry', 'signed-in-browser-results.json'] };
const [journey, filename] = journeys[path.basename(process.argv[1])];
const mode = !process.env.FAKE_BROWSER_TARGET || process.env.FAKE_BROWSER_TARGET === journey ? process.env.FAKE_BROWSER_MODE : undefined;
if (mode === 'failed') process.exit(19);
if (mode === 'no-result') process.exit(0);
fs.writeFileSync(path.join(output, filename), mode === 'malformed' ? '{' : JSON.stringify({
    browser: mode === 'no-version' ? '' : 'fixture-browser', events: ['fixture'],
    liveZitadel: 'NOT_RUN_CONTROLLED_LOCAL_ISSUER', providerPersistence: 'NOT_RUN_CONTROLLED_CONTRACT_FIXTURE',
    liveProviders: 'NOT_RUN_CONTROLLED_HTTPS_CONTRACT_FIXTURE', liveIdentityProvider: 'NOT_RUN_CONTROLLED_LOCAL_ISSUER', backendProvider: 'NOT_RUN_CONTROLLED_HTTP_CONTRACT_FIXTURE'
}));
if (mode !== 'no-screenshot') fs.writeFileSync(path.join(output, 'fixture.png'), 'fixture');
fs.writeFileSync(path.join(output, 'fixture-accessibility.json'), JSON.stringify({
    violations: mode === 'violations' ? ['fixture-violation'] : [], passedRules: 1
}));
'''


class VerificationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="application-quality-selfcheck-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        (self.root / "eng").mkdir()
        (self.root / "tools").mkdir()
        for name in ("verify-host-ci.sh", "verify-host.sh", "build-host.sh", "hosts.tsv", "verify-web-browser.sh"):
            shutil.copy2(ROOT / "eng" / name, self.root / "eng" / name)
        # The token generator is outside this orchestration test's claim.
        (self.root / "eng/build-ui-tokens.py").write_text("import sys; assert sys.argv[1:] == ['--check']\n")
        for project, tests in HOSTS.values():
            for relative in [project, *tests]:
                target = self.root / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text("<Project />")
        dotnet = self.root / "tools/dotnet"
        dotnet.write_text(FAKE_DOTNET)
        dotnet.chmod(0o755)
        self.log = self.root / "dotnet.jsonl"
        self.env = {**os.environ, "PATH": str(dotnet.parent) + os.pathsep + os.environ["PATH"],
                    "FAKE_DOTNET_LOG": str(self.log)}

    def run_script(self, name, *args):
        return subprocess.run(["bash", str(self.root / "eng" / name), *args], cwd="/tmp",
                              env=self.env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=30)

    def calls(self):
        return [json.loads(line) for line in self.log.read_text().splitlines()] if self.log.exists() else []

    def assert_checksums(self, output):
        lines = (output / "SHA256SUMS").read_text().splitlines()
        self.assertGreater(len(lines), 0)
        for line in lines:
            digest, relative = line.split("  ", 1)
            self.assertEqual(digest, hashlib.sha256((output / relative).read_bytes()).hexdigest())

    def test_all_five_host_graphs_restore_test_and_publish_independently(self):
        for host, (project, tests) in HOSTS.items():
            if host == "workstation":
                continue
            with self.subTest(host=host):
                self.log.unlink(missing_ok=True)
                result = self.run_script("verify-host-ci.sh", host)
                self.assertEqual(result.returncode, 0, result.stdout)
                calls = self.calls()
                restores = [call for call in calls if call[0] == "restore"]
                self.assertTrue(all("--locked-mode" in call for call in restores))
                self.assertEqual({call[1] for call in restores}, {project, *tests})
                self.assertEqual([call[1] for call in calls if call[0] == "test"], tests)
                self.assertEqual([call[1] for call in calls if call[0] == "publish"], [project])
                self.assertTrue(all("Application.slnx" not in call for call in calls))
                self.assertTrue(all("workstation" not in " ".join(call) for call in calls))
                self.assert_checksums(self.root / "artifacts/publish" / host)

    def test_invalid_host_arguments_never_invoke_dotnet(self):
        for args in ((), ("workstation",), ("../web",), ("unknown",), ("web", "--publish")):
            with self.subTest(args=args):
                self.assertEqual(self.run_script("verify-host-ci.sh", *args).returncode, 2)
                self.assertEqual(self.calls(), [])

    def test_failures_propagate_and_old_publication_is_removed(self):
        for phase in ("--info", "restore", "format", "build", "test", "publish"):
            with self.subTest(phase=phase):
                output = self.root / "artifacts/publish/core-api"
                output.mkdir(parents=True, exist_ok=True)
                (output / "old.dll").write_text("stale")
                self.env["FAKE_DOTNET_FAILURE"] = phase
                result = self.run_script("verify-host-ci.sh", "core-api")
                self.assertEqual(result.returncode, 17, result.stdout)
                self.assertFalse(output.exists())

    def test_each_missing_runtime_file_rejects_publication(self):
        for failure in ("FAKE_MISSING_OUTPUT", "FAKE_EMPTY_OUTPUT"):
            for suffix in ("dll", "deps.json", "runtimeconfig.json"):
                with self.subTest(failure=failure, suffix=suffix):
                    self.env[failure] = suffix
                    result = self.run_script("verify-host-ci.sh", "web")
                    self.assertNotEqual(result.returncode, 0, result.stdout)
                    self.assertFalse((self.root / "artifacts/publish/web/SHA256SUMS").exists())
            self.env.pop(failure)

    def prepare_browser(self):
        web = self.root / "apps/web/Application.Web/bin/Release/net10.0"
        web.mkdir(parents=True)
        for suffix in ("dll", "deps.json", "runtimeconfig.json"):
            (web / ("Application.Web." + suffix)).write_text("fixture")
        playwright = self.root / "tools/playwright"
        playwright.mkdir()
        (playwright / "package.json").write_text(json.dumps({"version": "1.64.0"}))
        executable = playwright / "fixture-chromium"
        executable.write_text("fixture")
        (playwright / "index.js").write_text("exports.chromium = {executablePath: () => " + json.dumps(str(executable)) + "};")
        axe = self.root / "tools/axe-core"
        axe.mkdir()
        (axe / "package.json").write_text(json.dumps({"version": "4.11.0"}))
        (axe / "axe.min.js").write_text("fixture")
        self.env.update(APPLICATION_ENTRY_PLAYWRIGHT_PATH=str(playwright), APPLICATION_ENTRY_AXE_PATH=str(axe / "axe.min.js"))
        for name in ("test-signed-in-entry-browser.cjs", "test-business-web.cjs", "test-quotation-web.cjs", "test-operations-web.cjs", "test-tenant-administration-web.cjs", "test-customer-operations-web.cjs"):
            (self.root / "eng" / name).write_text(FAKE_BROWSER_GUARD)

    def test_browser_wrapper_validates_fresh_evidence_and_checksums(self):
        self.prepare_browser()
        result = self.run_script("verify-web-browser.sh")
        self.assertEqual(result.returncode, 0, result.stdout)
        self.assert_checksums(self.root / "artifacts/web-browser")
        self.assertEqual(self.calls(), [])

    def test_browser_failure_is_not_masked_by_logging(self):
        self.prepare_browser()
        self.env["FAKE_BROWSER_MODE"] = "failed"
        result = self.run_script("verify-web-browser.sh")
        self.assertEqual(result.returncode, 19, result.stdout)

    def test_business_failure_is_not_masked_by_successful_entry(self):
        self.prepare_browser()
        self.env.update(FAKE_BROWSER_MODE="failed", FAKE_BROWSER_TARGET="business")
        result = self.run_script("verify-web-browser.sh")
        self.assertEqual(result.returncode, 19, result.stdout)
        self.assertTrue((self.root / "artifacts/web-browser/entry/signed-in-browser-results.json").exists())
        self.assertFalse((self.root / "artifacts/web-browser/SHA256SUMS").exists())

    def test_each_additional_journey_failure_rejects_the_combined_receipt(self):
        self.prepare_browser()
        for journey in ("quotations", "operations", "administration", "customers"):
            with self.subTest(journey=journey):
                self.env.update(FAKE_BROWSER_MODE="failed", FAKE_BROWSER_TARGET=journey)
                result = self.run_script("verify-web-browser.sh")
                self.assertEqual(result.returncode, 19, result.stdout)
                self.assertTrue((self.root / "artifacts/web-browser/business/business-browser-results.json").exists())
                self.assertFalse((self.root / "artifacts/web-browser/SHA256SUMS").exists())

    def test_missing_stale_malformed_or_failed_browser_evidence_is_rejected(self):
        self.prepare_browser()
        for mode in ("no-result", "malformed", "no-version", "no-screenshot", "violations"):
            with self.subTest(mode=mode):
                self.env.pop("FAKE_BROWSER_MODE", None)
                self.assertEqual(self.run_script("verify-web-browser.sh").returncode, 0)
                self.env["FAKE_BROWSER_MODE"] = mode
                result = self.run_script("verify-web-browser.sh")
                self.assertNotEqual(result.returncode, 0, result.stdout)
                self.assertFalse((self.root / "artifacts/web-browser/SHA256SUMS").exists())

    def test_browser_requires_built_web_and_qualified_tool_version(self):
        self.prepare_browser()
        (self.root / "tools/playwright/package.json").write_text('{"version":"0.0.0"}')
        self.assertNotEqual(self.run_script("verify-web-browser.sh").returncode, 0)
        (self.root / "apps/web/Application.Web/bin/Release/net10.0/Application.Web.dll").unlink()
        self.assertNotEqual(self.run_script("verify-web-browser.sh").returncode, 0)
        self.assertEqual(self.calls(), [])

    def test_ci_keeps_pins_permissions_existing_gates_and_host_inventory(self):
        for path in (ROOT / ".github/workflows").glob("*.yml"):
            text = path.read_text()
            self.assertIn("permissions:\n  contents: read", text, str(path))
            self.assertNotIn("pull_request_target:", text, str(path))
            actions = re.findall(r"uses:\s*([^\s#]+)", text)
            self.assertTrue(actions, str(path))
            self.assertTrue(all(re.fullmatch(r"[^@]+@[0-9a-f]{40}", action) for action in actions), str(path))
        verify = (ROOT / ".github/workflows/verify.yml").read_text()
        matrix = re.search(r"host:\s*\[([^]]+)\]", verify)
        self.assertEqual({host.strip() for host in matrix[1].split(",")}, set(HOSTS))
        for guard in ('COLLECT_COVERAGE: "1"', "./eng/verify.sh", "./eng/mutate-orders.sh", "fail-fast: false",
                      'if: matrix.host != \'workstation\'\n        run: ./eng/verify-host-ci.sh "${{ matrix.host }}"',
                      'if: matrix.host == \'workstation\'\n        run: ./eng/verify-host.sh "${{ matrix.host }}"'):
            self.assertIn(guard, verify)
        security = (ROOT / ".github/workflows/security.yml").read_text()
        self.assertIn("./eng/audit-dependencies.sh", security)
        self.assertIn("gitleaks/gitleaks-action@", security)
        browser = (ROOT / ".github/workflows/web-browser.yml").read_text()
        for guard in ("playwright@1.64.0", "axe-core@4.11.0", "--ignore-scripts", "persist-credentials: false",
                      "./eng/build-host.sh web", "./eng/verify-web-browser.sh", "if-no-files-found: error"):
            self.assertIn(guard, browser)


if __name__ == "__main__":
    unittest.main(verbosity=2)
