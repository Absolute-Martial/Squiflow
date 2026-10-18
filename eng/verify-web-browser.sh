#!/usr/bin/env bash
set -euo pipefail

if (( $# != 0 )); then
    echo "No arguments are supported; build the Release Web graph first." >&2
    exit 2
fi
repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"
export APPLICATION_UI_ARTIFACTS="$repository_root/artifacts/web-browser"
rm -rf -- "$APPLICATION_UI_ARTIFACTS"
mkdir -p -- "$APPLICATION_UI_ARTIFACTS"
browser_output="$APPLICATION_UI_ARTIFACTS"

{
    for command in node dotnet openssl timeout; do
        command -v "$command" >/dev/null || { echo "Missing browser prerequisite: $command" >&2; exit 1; }
    done
    web="apps/web/Application.Web/bin/Release/net10.0/Application.Web"
    for suffix in dll deps.json runtimeconfig.json; do
        if [[ ! -s "$web.$suffix" ]]; then
            echo "Missing Release Web output: $web.$suffix. Run ./eng/build-host.sh web first." >&2
            exit 1
        fi
    done
    node <<'JS'
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
assert.ok(Number(process.versions.node.split('.')[0]) >= 20, 'Playwright requires Node.js 20 or later.');
const playwrightPath = process.env.APPLICATION_ENTRY_PLAYWRIGHT_PATH || 'playwright';
const playwright = require(playwrightPath);
const playwrightVersion = require(playwrightPath + '/package.json').version;
const axePath = require.resolve(process.env.APPLICATION_ENTRY_AXE_PATH || 'axe-core/axe.min.js');
const axeVersion = require(path.join(path.dirname(axePath), 'package.json')).version;
assert.equal(playwrightVersion, '1.64.0', 'Requalify the browser guard before changing Playwright.');
assert.equal(axeVersion, '4.11.0', 'Requalify the accessibility guard before changing axe-core.');
const browserExecutable = process.env.APPLICATION_ENTRY_BROWSER_EXECUTABLE || playwright.chromium.executablePath();
assert.ok(path.isAbsolute(browserExecutable) && fs.existsSync(browserExecutable), 'Install the browser or provide an existing absolute browser executable path.');
console.log(`Node ${process.versions.node}; Playwright ${playwrightVersion}; axe-core ${axeVersion}`);
JS
    APPLICATION_UI_ARTIFACTS="$browser_output/entry" timeout --signal=TERM --kill-after=10s 10m node eng/test-signed-in-entry-browser.cjs
    APPLICATION_UI_ARTIFACTS="$browser_output/business" timeout --signal=TERM --kill-after=10s 10m node eng/test-business-web.cjs
    APPLICATION_UI_ARTIFACTS="$browser_output/quotations" timeout --signal=TERM --kill-after=10s 10m node eng/test-quotation-web.cjs
    APPLICATION_UI_ARTIFACTS="$browser_output/operations" timeout --signal=TERM --kill-after=10s 10m node eng/test-operations-web.cjs
    APPLICATION_UI_ARTIFACTS="$browser_output/administration" timeout --signal=TERM --kill-after=10s 10m node eng/test-tenant-administration-web.cjs
    APPLICATION_UI_ARTIFACTS="$browser_output/customers" timeout --signal=TERM --kill-after=10s 10m node eng/test-customer-operations-web.cjs
    node <<'JS'
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = process.env.APPLICATION_UI_ARTIFACTS;
for (const [journey, filename] of [['entry', 'signed-in-browser-results.json'], ['business', 'business-browser-results.json'], ['quotations', 'quotation-browser-results.json'], ['operations', 'operations-browser-results.json'], ['administration', 'tenant-administration-browser-results.json'], ['customers', 'customer-operations-browser-results.json']]) {
const output = path.join(root, journey);
const result = JSON.parse(fs.readFileSync(path.join(output, filename), 'utf8'));
assert.ok(typeof result.browser === 'string' && result.browser.length > 0, 'Missing actual browser version.');
assert.ok(Array.isArray(result.events) && result.events.length > 0, 'Missing browser journey results.');
if (journey === 'entry' || journey === 'business') assert.equal(result.liveZitadel, 'NOT_RUN_CONTROLLED_LOCAL_ISSUER');
if (journey === 'quotations') assert.equal(result.liveProviders, 'NOT_RUN_CONTROLLED_HTTPS_CONTRACT_FIXTURE');
if (journey === 'operations' || journey === 'administration' || journey === 'customers') {
    assert.equal(result.liveIdentityProvider, 'NOT_RUN_CONTROLLED_LOCAL_ISSUER');
    assert.equal(result.backendProvider, 'NOT_RUN_CONTROLLED_HTTP_CONTRACT_FIXTURE');
}
const files = fs.readdirSync(output);
const screenshots = files.filter(file => file.endsWith('.png'));
const audits = files.filter(file => file.endsWith('-accessibility.json'));
assert.ok(screenshots.length > 0 && audits.length > 0, 'Missing screenshots or accessibility evidence.');
for (const file of [...screenshots, ...audits]) assert.ok(fs.statSync(path.join(output, file)).size > 0, `Empty evidence: ${file}`);
for (const file of audits) {
    const audit = JSON.parse(fs.readFileSync(path.join(output, file), 'utf8'));
    assert.deepEqual(audit.violations, [], `${file}: accessibility violations`);
    assert.ok(audit.passedRules > 0, `${file}: no rules executed`);
}
if (journey === 'business') assert.equal(result.providerPersistence, 'NOT_RUN_CONTROLLED_CONTRACT_FIXTURE');
console.log(`${journey} browser journeys completed; ${screenshots.length} screenshots and ${audits.length} accessibility reports.`);
}
JS
} 2>&1 | tee "$APPLICATION_UI_ARTIFACTS/browser-run.log"
(
    cd "$APPLICATION_UI_ARTIFACTS"
    find . -type f ! -path ./SHA256SUMS -print0 | sort -z | xargs -0 -r sha256sum > SHA256SUMS
)
