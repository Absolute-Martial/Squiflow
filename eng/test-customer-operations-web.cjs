// Local HTTPS protocol fixture. This proves browser/framework behavior, not live ZITADEL qualification.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const https = require('node:https');
const http = require('node:http');
const os = require('node:os');
const path = require('node:path');
const { spawn, execFileSync } = require('node:child_process');
const { chromium } = require(process.env.APPLICATION_ENTRY_PLAYWRIGHT_PATH || 'playwright');
const audit = require('./test-entry-ui.cjs');
const root = path.resolve(__dirname, '..');
const output = process.env.APPLICATION_UI_ARTIFACTS || path.join(root, 'artifacts/customer-operations-web-browser');
fs.mkdirSync(output, { recursive: true });
const temporary = fs.mkdtempSync(path.join(os.tmpdir(), 'application-login-browser-'));
const cert = path.join(temporary, 'certificate.pem');
const key = path.join(temporary, 'private-key.pem');
const pfx = path.join(temporary, 'certificate.pfx');
execFileSync('openssl', ['req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-keyout', key, '-out', cert, '-days', '1', '-subj', '/CN=Application local test', '-addext', 'subjectAltName=IP:127.0.0.1'], { stdio: 'ignore' });
execFileSync('openssl', ['pkcs12', '-export', '-out', pfx, '-inkey', key, '-in', cert, '-passout', 'pass:local-test-only'], { stdio: 'ignore' });
const trust = path.join(temporary, 'trusted-certificates.pem');
fs.writeFileSync(trust, Buffer.concat([fs.readFileSync('/etc/ssl/certs/ca-certificates.crt'), fs.readFileSync(cert)]));
const signing = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 });
const publicJwk = { ...signing.publicKey.export({ format: 'jwk' }), kid: 'local-signing', use: 'sig', alg: 'RS256' };
const accessToken = 'synthetic-browser-actor-token-never-render';
const clientSecret = 'synthetic-browser-client-secret-never-render';
const clientId = 'browser-entry-client';
const apiScope = 'urn:zitadel:iam:org:project:id:browser-fixture:aud';
const accountId = crypto.randomUUID();
const first = { tenantId: crypto.randomUUID(), displayName: 'North workspace' };
const second = { tenantId: crypto.randomUUID(), displayName: '<script>South workspace</script>' };
let memberships = [first, second];
const requests = [], receipts = new Map(), resolutions = [];
const sourceId = crypto.randomUUID(), otherId = crypto.randomUUID(), importId = crypto.randomUUID();
const now = () => new Date().toISOString();
let sourceRevision = 3, redirected = false, plan, work, completed = false;
let csvBytes, failure, lostSave = false, malformed = false, releaseBusiness, holdBusiness = false;
const rowHash = number => crypto.createHash('sha256').update('synthetic-row-' + number).digest('hex');
const csv = 'name,email\r\n<script>Import customer</script>,first@example.test\r\nSecond customer,second@example.test\r\n,invalid\r\n';
const template = 'name,external_id,customer_type,email,phone,address_line1,address_line2,city,notes\r\n';
function individual(id) { return { individualId: id, tenantId: first.tenantId, displayName: '<script>Duplicate customer</script>', email: 'person@example.test', phone: null, availability: 1, revision: sourceRevision, createdByAccountId: accountId, createdAt: now(), customerType: 'individual', externalRegistrationId: null, addressLine1: null, addressLine2: null, city: null, notes: null, redirectTargetIndividualId: null }; }
function intent(name, email) { return { displayName: name, email, phone: null, customerType: 'individual', externalRegistrationId: null, addressLine1: null, addressLine2: null, city: null, notes: '<script>plain notes</script>', fingerprint: rowHash(99) }; }
function importRows() { return [
    { rowNumber: 2, sourceRowHash: rowHash(2), status: completed ? 2 : 1, intent: intent('<script>Import customer</script>', 'first@example.test'), errorCode: null, errorMessage: null, rowId: crypto.randomUUID(), customerId: completed ? sourceId : null, requiresDecision: true, duplicateEvidence: 'normalized_email', decision: work ? 1 : null, mappingCustomerId: null, attempts: completed ? 1 : 0 },
    { rowNumber: 3, sourceRowHash: rowHash(3), status: completed ? 3 : 1, intent: intent('Second customer', 'second@example.test'), errorCode: null, errorMessage: null, rowId: crypto.randomUUID(), customerId: completed ? otherId : null, requiresDecision: false, duplicateEvidence: null, decision: work ? 2 : null, mappingCustomerId: work ? otherId : null, attempts: completed ? 1 : 0 },
    { rowNumber: 4, sourceRowHash: rowHash(4), status: 4, intent: null, errorCode: 'name_invalid', errorMessage: 'Name is required.', rowId: crypto.randomUUID(), customerId: null, requiresDecision: false, duplicateEvidence: null, decision: null, mappingCustomerId: null, attempts: 0 }
]; }
async function business(request, response, uri, tenantId, resource) {
    assert.equal(request.headers.authorization, 'Bearer ' + accessToken); requests.push({ tenantId, resource, method: request.method });
    const finish = (value, status = 200) => json(response, value, status);
    if (holdBusiness) await new Promise(resolve => { releaseBusiness = resolve; });
    if (denyWorkspace || tenantId !== first.tenantId) return finish({ diagnostic: 'private customer diagnostic' }, 403);
    if (failure) return finish({ diagnostic: 'private customer diagnostic' }, failure);
    if (malformed) return finish({ unsupported: true });
    if (request.method === 'GET') {
        assert.equal(request.headers['idempotency-key'], undefined);
        if (resource.endsWith('import-template') || resource.endsWith('/source')) {
            const value = resource.endsWith('import-template') ? Buffer.from(template) : csvBytes;
            response.writeHead(200, { 'Content-Type': 'text/csv; charset=utf-8', 'Cache-Control': 'no-store' }); response.end(value); return;
        }
        if (resource.endsWith('/duplicate-resolutions')) return finish(resolutions);
        if (resource.startsWith('customers/individuals/')) return finish({ ...individual(sourceId), availability: 'active', redirectTargetIndividualId: redirected ? otherId : null });
        if (!plan) return finish({}, 404);
        if (resource.endsWith('/rows')) {
            assert.equal(Number(uri.searchParams.get('limit')), 2);
            const after = Number(uri.searchParams.get('afterRowNumber')), all = importRows().filter(x => x.rowNumber > after), items = all.slice(0, 2);
            return finish({ items, nextRowNumber: all.length > 2 ? items.at(-1).rowNumber : null });
        }
        if (completed && work) work = { ...work, status: 3, processedRows: 2, remainingRows: 0, completedAt: now() };
        return finish({ importId, contractVersion: 'customer-import/v1', manifestHash: plan.manifestHash, rowCount: 3, pending: completed ? 0 : 2, imported: completed ? 1 : 0, mappedToExisting: completed ? 1 : 0, rejected: 1, failed: 0, work: work || null,
            source: { byteLength: csvBytes.length, sha256: plan.manifestHash, contentType: 'text/csv', state: 3, retention: 2, createdAt: now(), expiresAt: null, failureCode: null } });
    }
    let bytes = []; for await (const chunk of request) { bytes.push(chunk); assert.ok(bytes.reduce((n,x) => n+x.length, 0) <= 10*1024*1024); } bytes = Buffer.concat(bytes);
    const key = request.headers['idempotency-key'];
    if (resource === 'customers/duplicates/search') {
        assert.equal(key, undefined); const body = JSON.parse(bytes); assert.equal(body.limit, 2);
        return finish({ candidates: [{ caseId: crypto.randomUUID(), customer: individual(otherId), evidence: [{ kind: 1 }, { kind: 4 }], requiresManualReview: true, resolution: resolutions.at(-1) || null }], hasMore: false });
    }
    assert.match(key, /^[a-f0-9]{32}$/);
    const saved = receipts.get(key), retained = { resource, bytes: bytes.toString('base64'), retention: request.headers['x-tenant-import-retention'] || null };
    if (saved) { assert.deepEqual(retained, saved.intent); response.setHeader('Idempotency-Replayed', 'true'); return finish(saved.value, saved.status); }
    function save(value, status = 200) { receipts.set(key, { intent: retained, value: structuredClone(value), status }); if (lostSave) { lostSave = false; response.destroy(); return; } return finish(value, status); }
    if (resource === 'customers/imports') {
        assert.equal(request.headers['content-type'], 'text/csv; charset=utf-8'); assert.equal(retained.retention, 'archive'); assert.equal(bytes.toString(), csv); csvBytes = bytes;
        plan = { importId, contractVersion: 'customer-import/v1', manifestHash: crypto.createHash('sha256').update(bytes).digest('hex'), rowCount: 3, pendingRows: 2, rejectedRows: 1, rowsRequiringDecision: 1 };
        return save(plan, 201);
    }
    const body = JSON.parse(bytes);
    if (resource.endsWith('/accept')) {
        assert.equal(work, undefined); assert.deepEqual(body.decisions, [{ rowNumber: 2, sourceRowHash: rowHash(2), kind: 'CreateNew', customerId: null }, { rowNumber: 3, sourceRowHash: rowHash(3), kind: 'MapToExisting', customerId: otherId }]);
        work = { workId: crypto.randomUUID(), importId, status: 1, processedRows: 0, remainingRows: 2, createdAt: now(), completedAt: null, lastError: null }; return save(work, 202);
    }
    const consolidate = resource.endsWith('/consolidate');
    assert.equal(body[consolidate ? 'sourceCustomerId' : 'customerId'], sourceId); assert.equal(body[consolidate ? 'canonicalCustomerId' : 'otherCustomerId'], otherId);
    assert.equal(body[consolidate ? 'expectedSourceRevision' : 'expectedCustomerRevision'], sourceRevision); assert.equal(body[consolidate ? 'expectedCanonicalRevision' : 'expectedOtherRevision'], 3);
    const outcome = consolidate ? 3 : { PotentialDuplicate: 1, KeepSeparate: 2, Dismiss: 4 }[body.outcome]; assert.ok(outcome);
    const resolution = { resolutionId: crypto.randomUUID(), customerId: sourceId, otherCustomerId: otherId, outcome, actorAccountId: accountId, resolvedAt: now(), reason: body.reason, evidence: 'NormalizedEmail,ExactName' };
    resolutions.push(resolution); if (consolidate) { sourceRevision++; redirected = true; }
    return save({ status: 1, resolution, canonicalCustomerId: consolidate ? otherId : null });
}

let denyWorkspace = false;
let accountEnabled = true;
let readiness = 200;
let longNames = false;
let releaseReadiness;
let holdReadiness = false;
let fixtureOrigin;
let webOrigin;
let web;
let browser;
let activePage;
let pkceChecks = 0;
let verificationChecks = 0;
const codes = new Map();
const events = [];
const fixturePaths = [];
const browserResponses = [];
const log = fs.openSync(path.join(output, 'customer-operations-web-host.log'), 'w');
function json(response, value, status = 200) {
    response.writeHead(status, { 'Content-Type': status === 403 ? 'application/problem+json' : 'application/json', 'Cache-Control': 'no-store' });
    response.end(JSON.stringify(value));
}
function brand() {
    const value = { displayName: longNames ? 'Fieldwork ' + 'North '.repeat(18).trim() : 'Fieldwork', shortName: longNames ? 'Fieldwork ' + 'North '.repeat(18).trim() : 'Fieldwork', legalName: 'Fieldwork Services', themeKey: 'default', logoUrl: '/logo.svg', faviconUrl: '/favicon.svg', supportUrl: '/support', privacyUrl: '/privacy', termsUrl: '/terms' };
    value.revision = crypto.createHash('sha256').update(Object.values(value).join('\n')).digest('hex').slice(0, 24);
    return value;
}
const fixture = https.createServer({ key: fs.readFileSync(key), cert: fs.readFileSync(cert) }, async (request, response) => {
    const uri = new URL(request.url, fixtureOrigin);
    fixturePaths.push(uri.pathname);
    if (uri.pathname === '/.well-known/openid-configuration') return json(response, {
        issuer: fixtureOrigin, authorization_endpoint: fixtureOrigin + '/authorize', token_endpoint: fixtureOrigin + '/token', jwks_uri: fixtureOrigin + '/keys',
        response_types_supported: ['code'], response_modes_supported: ['query'], grant_types_supported: ['authorization_code'],
        token_endpoint_auth_methods_supported: ['client_secret_post'], id_token_signing_alg_values_supported: ['RS256'], code_challenge_methods_supported: ['S256']
    });
    if (uri.pathname === '/keys') return json(response, { keys: [publicJwk] });
    if (uri.pathname === '/authorize') {
        if (uri.searchParams.get('prompt') === 'login') { assert.equal(uri.searchParams.get('max_age'), '0'); assert.equal(uri.searchParams.get('acr_values'), 'urn:application:assurance:browser-test'); verificationChecks++; }
        assert.equal(uri.searchParams.get('client_id'), clientId);
        assert.equal(uri.searchParams.get('code_challenge_method'), 'S256');
        assert.equal(uri.searchParams.get('redirect_uri'), webOrigin + '/signin-oidc');
        assert.equal(uri.searchParams.get('scope'), 'openid ' + apiScope);
        assert.ok(uri.searchParams.get('nonce'));
        const code = crypto.randomUUID();
        codes.set(code, { nonce: uri.searchParams.get('nonce'), challenge: uri.searchParams.get('code_challenge') });
        const callback = new URL(webOrigin + '/signin-oidc');
        callback.searchParams.set('code', code); callback.searchParams.set('state', uri.searchParams.get('state'));
        response.writeHead(302, { Location: callback.href, 'Cache-Control': 'no-store' }); return response.end();
    }
    if (uri.pathname === '/token') {
        let body = '';
        for await (const chunk of request) { body += chunk; assert.ok(body.length <= 16384); }
        const form = new URLSearchParams(body); const transaction = codes.get(form.get('code')); codes.delete(form.get('code'));
        assert.ok(transaction); assert.equal(form.get('client_secret'), clientSecret);
        assert.equal(crypto.createHash('sha256').update(form.get('code_verifier')).digest('base64url'), transaction.challenge); pkceChecks++;
        const now = Math.floor(Date.now() / 1000);
        const unsigned = Buffer.from(JSON.stringify({ alg: 'RS256', kid: publicJwk.kid, typ: 'JWT' })).toString('base64url') + '.' +
            Buffer.from(JSON.stringify({ iss: fixtureOrigin, aud: clientId, sub: 'synthetic-browser-human', nonce: transaction.nonce, iat: now, exp: now + 600 })).toString('base64url');
        const token = unsigned + '.' + crypto.sign('RSA-SHA256', Buffer.from(unsigned), signing.privateKey).toString('base64url');
        return json(response, { access_token: accessToken, token_type: 'Bearer', expires_in: 600, id_token: token });
    }
    if (uri.pathname === '/api/v1/application/bootstrap') return json(response, brand());
    if (uri.pathname === '/health/ready') {
        if (holdReadiness) await new Promise(resolve => { releaseReadiness = resolve; });
        response.writeHead(readiness); return response.end();
    }
    if (uri.pathname === '/favicon.svg' || uri.pathname === '/logo.svg') { response.writeHead(200, { 'Content-Type': 'image/svg+xml' }); return response.end('<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32"><rect width="32" height="32" rx="8" fill="#20352e"/></svg>'); }
    if (uri.pathname.startsWith('/api/')) {
        if (request.headers.authorization !== 'Bearer ' + accessToken) { response.writeHead(401); return response.end(); }
        if (!accountEnabled) return json(response, { code: 'account_disabled' }, 403);
        if (uri.pathname === '/api/v1/account') return json(response, { accountId });
        if (uri.pathname === '/api/v1/account/tenants/page') return json(response, { items: memberships, nextTenantId: null });
        const businessMatch = uri.pathname.match(/^\/api\/v1\/tenants\/([^/]+)\/(customers\/.*)$/);
        if (businessMatch) return business(request, response, uri, businessMatch[1], businessMatch[2]);
        const match = uri.pathname.match(/^\/api\/v1\/tenants\/([^/]+)\/workspace$/);
        if (match) {
            const tenant = memberships.find(value => value.tenantId === match[1]);
            if (!tenant || denyWorkspace) return json(response, { code: 'tenant_access_denied' }, 403);
            return json(response, { tenantId: tenant.tenantId, displayName: tenant === first ? 'North workspace' : 'South workspace' });
        }
    }
    response.writeHead(404); response.end();
});
async function until(check, timeout = 20000) {
    const end = Date.now() + timeout;
    while (Date.now() < end) { if (await check()) return; await new Promise(resolve => setTimeout(resolve, 50)); }
    throw new Error('Expected runtime signal did not arrive.');
}
async function stopWeb() {
    if (!web || web.exitCode !== null) return;
    const exited = new Promise(resolve => web.once('exit', resolve)); web.kill('SIGTERM');
    await Promise.race([exited, new Promise(resolve => setTimeout(resolve, 5000))]);
    if (web.exitCode === null) { web.kill('SIGKILL'); await exited; }
}
async function startWeb() {
    web = spawn('dotnet', [path.join(root, 'apps/web/Application.Web/bin/Release/net10.0/Application.Web.dll'), '--urls', webOrigin], {
        cwd: path.join(root, 'apps/web/Application.Web'), stdio: ['ignore', log, log],
        env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', SSL_CERT_FILE: trust,
            Kestrel__Certificates__Default__Path: pfx, Kestrel__Certificates__Default__Password: 'local-test-only',
            ApplicationEntry__CoreApiBaseUrl: fixtureOrigin, Authentication__Enabled: 'true', Authentication__Authority: fixtureOrigin,
            Authentication__StrongAuthenticationAcr: 'urn:application:assurance:browser-test', Authentication__PublicOrigin: webOrigin, Authentication__ClientId: clientId, Authentication__ClientSecret: clientSecret, Authentication__ApiScope: apiScope }
    });
    await until(() => new Promise(resolve => {
        const request = https.get(webOrigin + '/health/live', { ca: fs.readFileSync(cert) }, response => { response.resume(); resolve(response.statusCode === 200); });
        request.on('error', () => resolve(false));
    }));
}
async function run() {
    await new Promise(resolve => fixture.listen(0, '127.0.0.1', resolve)); fixtureOrigin = `https://127.0.0.1:${fixture.address().port}`;
    const probe = http.createServer(); await new Promise(resolve => probe.listen(0, '127.0.0.1', resolve));
    webOrigin = `https://127.0.0.1:${probe.address().port}`; await new Promise(resolve => probe.close(resolve));
    await startWeb(); browser = await chromium.launch({ headless: true, executablePath: process.env.APPLICATION_ENTRY_BROWSER_EXECUTABLE || undefined });
    const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 1000 } });
    const page = await context.newPage(); activePage = page; page.setDefaultTimeout(30000);
    page.on('response', response => browserResponses.push({ path: new URL(response.url()).pathname, status: response.status() }));
    page.on('dialog', dialog => dialog.dismiss());
    const pageErrors = []; page.on('pageerror', error => pageErrors.push(error.message));
    async function click(name) {
        console.log('Control:', name);
        const businessPage = name !== 'Sign in' && await page.locator('.business-status').count();
        if (!businessPage) { await page.getByRole('button', { name, exact: true }).click(); return; }
        await page.locator('.business-status[aria-busy=false]').waitFor();
        releaseBusiness = undefined; holdBusiness = true;
        try {
            await page.getByRole('button', { name, exact: true }).click();
            await until(() => Boolean(releaseBusiness));
            await page.locator('.business-status[aria-busy=true]').waitFor();
        } finally { holdBusiness = false; releaseBusiness?.(); releaseBusiness = undefined; }
        await page.locator('.business-status[aria-busy=false]').waitFor();
    }
    async function fill(label, value) { await page.getByLabel(label, { exact: true }).fill(String(value)); }
    async function message(text) { await page.locator('.business-status').getByText(text, { exact: false }).waitFor(); }
    async function go(resource) { await page.goto(`${webOrigin}/workspace/${first.tenantId}/${resource}`); await page.locator('.business-status[aria-busy=false]').waitFor(); }
    async function saved() { await message('Request completed.'); }
    async function capture(name) {
        for (let attempt = 0; attempt < 2; attempt++) {
            await page.locator('#components-reconnect-modal').waitFor({ state: 'hidden' });
            await page.evaluate(() => {
                const modal = document.querySelector('#components-reconnect-modal');
                window.operationsReconnectObserved = false;
                window.operationsReconnectObserver = new MutationObserver(() => { if (getComputedStyle(modal).display !== 'none') window.operationsReconnectObserved = true; });
                window.operationsReconnectObserver.observe(modal, { attributes: true });
            });
            let interrupted;
            try { await audit(page, name, output); }
            finally { interrupted = await page.evaluate(() => { window.operationsReconnectObserver.disconnect(); return window.operationsReconnectObserved; }); }
            if (!interrupted) return;
            console.log('Capture interrupted by reconnect; waiting for a connected frame:', name);
        }
        throw new Error('Capture repeatedly interrupted by reconnect: ' + name);
    }
    try { await page.goto(webOrigin); }
    catch (error) {
        if (!String(error).includes('ERR_NETWORK_CHANGED') || requests.length !== 0) throw error;
        console.log('Initial browser network-change interruption; retrying initial navigation once before sign-in or business requests.');
        await page.goto(webOrigin);
    }
    await click('Sign in'); await page.waitForURL(webOrigin + '/workspace');
    await go('customers/duplicates'); await capture('customer-duplicates-empty-desktop');
    await fill('Name to compare', 'Duplicate customer'); await fill('Matching email (optional)', 'person@example.test');
    await click('Find possible duplicates'); await saved(); await capture('customer-duplicate-matches');
    await page.getByRole('button', { name: 'Use <script>Duplicate customer</script> as other customer', exact: true }).click();
    await fill('Source customer reference', sourceId); await click('Load source customer'); await saved();
    await fill('Review reason (optional)', '<script>Reviewed exact pair</script>');
    await page.getByLabel('I reviewed both physical customer identities, current revisions and this decision.', { exact: true }).check();
    lostSave = true; await click('Save duplicate review'); await message('save outcome is unknown'); assert.equal(resolutions.length, 1);
    await capture('customer-duplicate-unknown'); await click('Retry saved request'); await message('earlier request was already saved'); assert.equal(resolutions.length, 1);
    await page.getByText('Consolidate source into survivor', { exact: true }).click();
    assert.ok(await page.getByRole('button', { name: 'Consolidate exact customer pair', exact: true }).isDisabled());
    await fill('Type “Consolidate” to confirm redirect', 'Consolidate'); await click('Consolidate exact customer pair'); await saved(); assert.equal(redirected, true);
    await click('Load source customer'); await saved(); await page.getByText('This physical source has been consolidated.', { exact: false }).waitFor();
    await click('Load duplicate resolution history'); await saved(); assert.equal(resolutions.length, 2); await capture('customer-consolidated-history');
    failure = 409; await page.getByLabel('I reviewed both physical customer identities, current revisions and this decision.', { exact: true }).check();
    await click('Save duplicate review'); await message('conflicts with current records'); failure = undefined;
    malformed = true; await click('Find possible duplicates'); await message('unsupported response'); malformed = false;
    failure = 403; await click('Find possible duplicates'); await message('Each operation needs its own permission'); failure = undefined;
    await go('customers/imports'); await capture('customer-import-empty-desktop');
    async function download(name, expected) {
        const pending = page.waitForEvent('download'); await click(name); const value = await pending;
        assert.equal(fs.readFileSync(await value.path(), 'utf8'), expected); await message('CSV download offered');
    }
    await download('Download CSV template', template);
    await page.getByLabel('Customer CSV file', { exact: true }).setInputFiles({ name: 'synthetic-customers.csv', mimeType: 'text/csv', buffer: Buffer.from(csv) });
    await message('CSV selected.'); await page.getByLabel('Source retention', { exact: true }).selectOption('archive');
    await page.getByLabel('I reviewed this CSV and its source-retention choice.', { exact: true }).check();
    lostSave = true; await click('Prepare retained import plan'); await message('save outcome is unknown'); assert.equal(plan.importId, importId);
    await capture('customer-import-plan-unknown'); await click('Retry saved request'); await message('Plan retained; no customers were created');
    await click('Load saved import'); await saved(); await download('Download retained CSV source', csv);
    await click('Review first row page'); await saved();
    assert.ok(await page.getByRole('button', { name: 'Accept reviewed import', exact: true }).isDisabled());
    await page.getByLabel('Decision for row 2', { exact: true }).selectOption('CreateNew');
    await page.getByLabel('Decision for row 3', { exact: true }).selectOption('MapToExisting'); await fill('Mapping customer reference for row 3', otherId);
    await capture('customer-import-decisions'); await click('Review next row page'); await saved();
    await page.getByRole('heading', { name: 'Row 4 · Rejected', exact: true }).waitFor();
    await page.getByLabel('I reviewed every saved row and required duplicate decision, including default creation for other valid rows.', { exact: true }).check();
    lostSave = true; await click('Accept reviewed import'); await message('save outcome is unknown'); const originalWorkId = work.workId;
    await click('Retry saved request'); await message('Import work Accepted.'); assert.equal(work.workId, originalWorkId); assert.equal(completed, false);
    await capture('customer-import-work-accepted');
    completed = true; await click('Load saved import'); await saved(); await page.getByRole('heading', { name: 'Work Completed', exact: true }).waitFor();
    await click('Review first row page'); await saved(); await page.getByRole('heading', { name: 'Row 2 · Imported', exact: true }).waitFor();
    await page.getByRole('heading', { name: 'Row 3 · Mapped to existing', exact: true }).waitFor(); await capture('customer-import-results-desktop');
    await page.setViewportSize({ width: 320, height: 900 }); await capture('customer-import-results-mobile-320');
    await page.emulateMedia({ forcedColors: 'active' }); await capture('customer-import-results-forced-colors'); await page.emulateMedia({ forcedColors: 'none' });
    await go('customers/duplicates'); await capture('customer-duplicates-mobile-320'); await page.setViewportSize({ width: 1440, height: 1000 });
    failure = 503; await go('customers/imports/' + importId); await message('service is unavailable'); failure = undefined;
    denyWorkspace = true; await go('customers/imports'); await page.getByRole('heading', { name: 'Workspace unavailable', exact: true }).waitFor();
    assert.equal(await page.getByRole('button', { name: 'Prepare retained import plan', exact: true }).count(), 0); denyWorkspace = false;
    await go('customers/imports'); assert.ok(!(await page.content()).includes(accessToken)); assert.ok(!(await page.content()).includes(clientSecret)); assert.ok(!(await page.content()).includes('private customer diagnostic'));
    assert.deepEqual(await page.evaluate(() => ({ local: Object.keys(localStorage), session: Object.keys(sessionStorage) })), { local: [], session: [] });
    assert.equal((await context.request.post(page.url(), { data: csv })).ok(), false);
    assert.deepEqual(pageErrors, []);
    events.push('Rendered duplicate search, explicit review replay, typed exact-pair consolidation, current redirect/history, malformed/conflict and denial states passed.',
        'Bounded CSV upload with frozen archive retention, unknown plan and acceptance replay, exact row decisions across pages, durable work progress/results and authorized template/source downloads passed.',
        'Actual desktop, 320px, forced-colors, CSRF, session/tenant admission, encoded untrusted text and automated accessibility checks passed.');
    fs.writeFileSync(path.join(output, 'customer-operations-browser-results.json'), JSON.stringify({ browser: await browser.version(), events, requestCount: requests.length, backendProvider: 'NOT_RUN_CONTROLLED_HTTP_CONTRACT_FIXTURE', liveIdentityProvider: 'NOT_RUN_CONTROLLED_LOCAL_ISSUER' }, null, 2));
    console.log(events.join('\n'));

}
run().catch(async error => { console.error(error); console.error(JSON.stringify({ url: activePage?.url(), requests: requests.slice(-12), status: activePage ? await activePage.locator('.business-status').textContent({ timeout: 1000 }).catch(() => null) : null })); if (activePage) await activePage.screenshot({ path: path.join(output, 'failure.png'), fullPage: true }).catch(() => {}); process.exitCode = 1; }).finally(async () => {
    holdBusiness = false; releaseBusiness?.(); if (browser) await browser.close(); await stopWeb(); fixture.closeAllConnections(); await new Promise(resolve => fixture.close(resolve));
    fs.closeSync(log); fs.rmSync(temporary, { recursive: true, force: true });
});
