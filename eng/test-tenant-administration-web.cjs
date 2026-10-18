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
const output = process.env.APPLICATION_UI_ARTIFACTS || path.join(root, 'artifacts/tenant-administration-web-browser');
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
const requests = [], receipts = new Map(), proposals = new Map(), roles = [];
const memberId = crypto.randomUUID(), successorId = crypto.randomUUID();
let authorizationRevision = 7, tenantRevision = 3, currentOwner = true, reconcileMode = 'applied';
let policy = { tenantId: first.tenantId, revision: 0, requireReferenceForProgramOrders: false, publishedPolicyRevisionId: null, requireApprovalForProgramOrders: false };
let failure, lostSave = false, malformed = false, releaseBusiness, holdBusiness = false;
const permissionCatalog = [{ permissionId: 'orders.view', relation: 'order_viewer', displayName: 'View Orders', delegableByInitialOwner: true }, { permissionId: 'orders.create', relation: 'order_creator', displayName: 'Create Orders', delegableByInitialOwner: true }];
const now = () => new Date().toISOString();
async function business(request, response, uri, tenantId, resource) {
    assert.equal(request.headers.authorization, 'Bearer ' + accessToken);
    requests.push({ tenantId, resource, method: request.method });
    const finish = (value, status = 200) => json(response, value, status);
    if (holdBusiness) await new Promise(resolve => { releaseBusiness = resolve; });
    if (denyWorkspace || tenantId !== first.tenantId || resource.startsWith('authorization') && !currentOwner) return finish({ diagnostic: 'private authority diagnostic' }, 403);
    if (failure) return finish({ diagnostic: 'private authority diagnostic' }, failure);
    if (malformed) return finish({ unsupported: true });
    if (request.method === 'GET') {
        assert.equal(request.headers['idempotency-key'], undefined);
        if (resource === 'authorization') return finish({ tenantId, authorizationRevision, permissionCatalog, roles });
        if (resource === 'profile-policy') return finish(policy);
        const proposal = proposals.get(resource.split('/')[2]);
        return proposal ? finish(proposal) : finish({}, 404);
    }
    let text = ''; for await (const chunk of request) { text += chunk; assert.ok(Buffer.byteLength(text) <= 8192); }
    const body = JSON.parse(text), key = request.headers['idempotency-key']; assert.match(key, /^[a-f0-9]{32}$/);
    const saved = receipts.get(key);
    if (saved) { assert.deepEqual({ resource, body }, saved.intent); response.setHeader('Idempotency-Replayed', 'true'); return finish(saved.value, saved.status); }
    function save(value, status = 200) {
        receipts.set(key, { intent: { resource, body }, value: structuredClone(value), status });
        if (lostSave) { lostSave = false; response.destroy(); return; } return finish(value, status);
    }
    if (resource.startsWith('profile-policy')) {
        assert.equal(body.expectedRevision, policy.revision);
        if (resource === 'profile-policy') {
            assert.equal(request.method, 'PUT'); assert.equal(typeof body.requireReferenceForProgramOrders, 'boolean'); assert.equal(typeof body.requireApprovalForProgramOrders, 'boolean');
            policy = { ...policy, revision: policy.revision + 1, requireReferenceForProgramOrders: body.requireReferenceForProgramOrders, requireApprovalForProgramOrders: body.requireApprovalForProgramOrders };
            return save({ status: 'Edited', policy, publishedPolicy: null });
        }
        assert.deepEqual(Object.keys(body), ['expectedRevision']);
        const publishedPolicy = { policyRevisionId: crypto.randomUUID(), tenantId, revision: policy.revision + 1, requireReferenceForProgramOrders: policy.requireReferenceForProgramOrders, requireApprovalForProgramOrders: policy.requireApprovalForProgramOrders, publishedByAccountId: accountId, publishedAt: now(), observedAuthorizationRevision: authorizationRevision, version: 'tenant-policy/v2' };
        policy = { ...policy, revision: publishedPolicy.revision, publishedPolicyRevisionId: publishedPolicy.policyRevisionId };
        return save({ status: 'PolicyPublished', policy, publishedPolicy });
    }
    if (resource === 'authorization/owner-transfer') {
        assert.deepEqual(body, { targetAccountId: successorId, expectedTenantRevision: tenantRevision });
        assert.ok([...proposals.values()].every(x => x.status === 2));
        currentOwner = false; tenantRevision++; authorizationRevision++;
        return save({ status: 1, previousOwnerAccountId: accountId, currentOwnerAccountId: successorId, tenantRevision, authorizationRevision });
    }
    if (resource.endsWith('/reconcile')) {
        const proposal = proposals.get(resource.split('/')[2]); assert.ok(proposal); assert.equal(proposal.status === 1 || proposal.status === 4, true);
        proposal.attemptCount++; proposal.updatedAt = now();
        if (reconcileMode !== 'applied') { proposal.status = 4; proposal.failureCode = reconcileMode; return save(proposal, 202); }
        proposal.status = 2; proposal.appliedAuthorizationRevision = ++authorizationRevision; proposal.failureCode = null;
        let role = roles.find(x => x.roleId === proposal.roleId);
        if (proposal.kind === 3) { role = { tenantId, roleId: proposal.roleId, revision: 1, name: proposal.roleName, permissionIds: proposal.requestedPermissions, availability: 2, createdAt: now(), updatedAt: now(), retiredAt: null }; roles.push(role); }
        if (proposal.kind === 4) { role.revision++; role.name = proposal.roleName; role.permissionIds = proposal.requestedPermissions; role.updatedAt = now(); }
        if (proposal.kind === 5) { role.revision++; role.availability = 5; role.retiredAt = role.updatedAt = now(); }
        return save(proposal);
    }
    assert.equal(body.expectedAuthorizationRevision, authorizationRevision);
    assert.ok([...proposals.values()].every(x => x.status === 2));
    const segments = resource.split('/');
    const kind = segments[1] === 'permissions' ? segments[2] === 'grant' ? 1 : 2 :
        segments[3] === 'assignments' ? segments[5] === 'assign' ? 6 : 7 :
        segments.length === 2 ? 3 : segments[3] === 'retire' ? 5 : 4;
    const roleId = kind <= 2 ? null : body.roleId || segments[2];
    const value = { proposalId: crypto.randomUUID(), tenantId, requestedByAccountId: accountId, idempotencyKey: key, requestFingerprint: crypto.createHash('sha256').update(text).digest('hex'), kind, status: 1, expectedAuthorizationRevision: authorizationRevision, appliedAuthorizationRevision: null,
        targetAccountId: kind <= 2 ? body.targetAccountId : kind >= 6 ? segments[4] : null,
        permissionId: body.permissionId || null, roleId, expectedRoleRevision: body.expectedRoleRevision || null, roleName: body.name || null, requestedPermissions: body.permissionIds || [], appliedPermissions: [], attemptCount: 0, failureCode: null, requestedAt: now(), updatedAt: now() };
    if (kind === 1 || kind === 2) assert.equal(value.targetAccountId, memberId);
    if (kind === 6 || kind === 7) { value.targetAccountId = segments[4]; assert.equal(value.targetAccountId, memberId); }
    proposals.set(value.proposalId, value); return save(value, 202);
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
const log = fs.openSync(path.join(output, 'tenant-administration-web-host.log'), 'w');
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
        const businessMatch = uri.pathname.match(/^\/api\/v1\/tenants\/([^/]+)\/(authorization(?:\/.*)?|profile-policy(?:\/.*)?)$/);
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
    await go('administration'); await capture('tenant-administration-empty-desktop');
    await page.getByLabel('Member account reference', { exact: true }).focus(); await page.keyboard.press('Tab');
    assert.equal(await page.evaluate(() => document.activeElement.id), 'permission-operation');
    await fill('Member account reference', memberId); await page.getByLabel('Compiled permission', { exact: true }).selectOption('orders.view');
    await page.getByLabel('I reviewed this active member, direct permission action and authorization revision.', { exact: true }).check();
    lostSave = true; await click('Save direct permission proposal'); await message('save outcome is unknown');
    assert.equal(proposals.size, 1); assert.ok(await page.getByLabel('Member account reference', { exact: true }).isDisabled());
    await capture('tenant-proposal-unknown'); await click('Retry saved request'); await message('Proposal saved as Pending');
    assert.equal(requests.filter(x => x.resource.endsWith('/reconcile')).length, 0);
    async function reconcile(expected = 'Proposal Applied') {
        await page.getByLabel('I reviewed the saved proposal and request one bounded reconciliation attempt.', { exact: true }).check();
        await click('Reconcile saved proposal'); await message(expected);
    }
    reconcileMode = 'execution_finished:authorization_model_rejected'; await reconcile('Proposal Uncertain');
    assert.ok(await page.getByRole('button', { name: 'Save direct permission proposal', exact: true }).isDisabled());
    await page.getByText('execution_finished:authorization_model_rejected', { exact: true }).waitFor(); await capture('tenant-proposal-finished-uncertain');
    const afterAttempt = requests.length; await page.waitForTimeout(250); assert.equal(requests.length, afterAttempt, 'No automatic reconciliation or polling');
    reconcileMode = 'applied'; await reconcile(); assert.equal(authorizationRevision, 8);
    const firstProposal = [...proposals.values()][0];
    await fill('Authorization proposal reference', firstProposal.proposalId); await click('Load saved proposal'); await message('Proposal Applied');
    await click('Reload administration'); await page.getByLabel('Compiled permission', { exact: true }).selectOption('orders.view'); await page.getByLabel('Direct permission action', { exact: true }).selectOption('revoke');
    await page.getByLabel('I reviewed this active member, direct permission action and authorization revision.', { exact: true }).check();
    await click('Save direct permission proposal'); await message('Proposal saved as Pending'); await reconcile();
    await click('Reload administration');
    const roleId = await page.getByLabel('Custom role reference', { exact: true }).inputValue();
    await fill('Custom role name', '<script>Role</script>'); await page.getByLabel('View Orders (orders.view)', { exact: true }).check();
    await page.getByLabel('I reviewed this role action, full permission replacement and exact revisions.', { exact: true }).check();
    await click('Save custom role proposal'); await message('Proposal saved as Pending'); await reconcile(); await click('Reload administration');
    assert.equal(roles.length, 1); assert.equal(roles[0].roleId, roleId);
    await page.getByRole('button', { name: 'Review <script>Role</script>', exact: true }).click();
    await fill('Custom role name', 'Dispatch role'); await page.getByLabel('Create Orders (orders.create)', { exact: true }).check();
    await page.getByLabel('I reviewed this role action, full permission replacement and exact revisions.', { exact: true }).check();
    await click('Save custom role proposal'); await reconcile(); await click('Reload administration'); assert.equal(roles[0].revision, 2);
    for (const action of ['assign', 'remove']) {
        await fill('Assignment role reference', roleId); await fill('Assignment member account reference', memberId);
        await page.getByLabel('Role assignment action', { exact: true }).selectOption(action);
        await page.getByLabel('I reviewed this role, active member and assignment action.', { exact: true }).check();
        await click('Save role assignment proposal'); await reconcile(); await click('Reload administration');
    }
    await page.getByRole('button', { name: 'Review Dispatch role', exact: true }).click(); await page.getByLabel('Role action', { exact: true }).selectOption('retire');
    await page.getByLabel('I reviewed this role action, full permission replacement and exact revisions.', { exact: true }).check();
    await click('Save custom role proposal'); await reconcile(); await click('Reload administration'); assert.equal(roles[0].availability, 5);
    await capture('tenant-custom-role-retired');
    failure = 409; await page.getByLabel('Compiled permission', { exact: true }).selectOption('orders.view'); await page.getByLabel('Direct permission action', { exact: true }).selectOption('grant');
    await page.getByLabel('I reviewed this active member, direct permission action and authorization revision.', { exact: true }).check();
    await click('Save direct permission proposal'); await message('conflicts with current records'); failure = undefined;
    malformed = true; await click('Reload administration'); await message('unsupported response'); malformed = false;
    failure = 503; await click('Reload administration'); await message('service is unavailable'); failure = undefined;
    await click('Reload administration');
    await page.getByRole('button', { name: 'Verify identity for Owner handoff', exact: true }).click();
    await page.waitForURL(`${webOrigin}/workspace/${first.tenantId}/billing`); assert.equal(verificationChecks, 1);
    await go('profile-policy'); await capture('policy-empty-desktop');
    await page.getByLabel('Require a supplied reference for program Orders', { exact: true }).check();
    await page.getByLabel('Require independent exact-revision approval for program Orders', { exact: true }).check();
    await page.getByLabel('I reviewed both draft requirements and the exact policy revision.', { exact: true }).check();
    lostSave = true; await click('Save policy draft'); await message('save outcome is unknown'); assert.equal(policy.revision, 1);
    await click('Retry saved request'); await message('Draft saved.'); assert.equal(policy.revision, 1);
    await page.getByLabel('I reviewed the saved draft at this exact revision and want an immutable publication.', { exact: true }).check();
    await click('Publish saved policy revision'); await message('Policy publication saved.'); assert.equal(policy.revision, 2);
    await page.getByRole('heading', { name: 'Saved policy publication receipt', exact: true }).waitFor(); await capture('policy-published-desktop');
    await page.setViewportSize({ width: 320, height: 900 }); await capture('policy-published-mobile-320');
    await page.emulateMedia({ forcedColors: 'active' }); await capture('policy-published-forced-colors'); await page.emulateMedia({ forcedColors: 'none' });
    await go('administration'); await capture('tenant-administration-mobile-320');
    await page.setViewportSize({ width: 1440, height: 1000 }); await page.emulateMedia({ forcedColors: 'active' }); await capture('tenant-administration-forced-colors'); await page.emulateMedia({ forcedColors: 'none' });
    await fill('Successor account reference', successorId); await fill('Expected tenant revision', tenantRevision);
    assert.ok(await page.getByRole('button', { name: 'Transfer initial Owner', exact: true }).isDisabled());
    await fill('Repeat successor account reference to confirm handoff', successorId); await click('Transfer initial Owner'); await message('Owner handoff saved.');
    assert.equal(currentOwner, false); assert.ok(await page.getByRole('button', { name: 'Reload administration', exact: true }).isDisabled());
    await go('administration'); await message('Each operation needs its own permission'); assert.ok(await page.getByRole('button', { name: 'Save direct permission proposal', exact: true }).isDisabled());
    denyWorkspace = true; await go('profile-policy'); await page.getByRole('heading', { name: 'Workspace unavailable', exact: true }).waitFor();
    assert.equal(await page.getByRole('button', { name: 'Save policy draft', exact: true }).count(), 0); denyWorkspace = false;
    await go('profile-policy'); assert.ok(!(await page.content()).includes(accessToken)); assert.ok(!(await page.content()).includes(clientSecret)); assert.ok(!(await page.content()).includes('private authority diagnostic'));
    assert.deepEqual(await page.evaluate(() => ({ local: Object.keys(localStorage), session: Object.keys(sessionStorage) })), { local: [], session: [] });
    assert.equal((await context.request.post(webOrigin + '/auth/verify-identity', { form: { tenantId: first.tenantId } })).status(), 400);
    assert.equal((await context.request.post(page.url(), { data: { expectedRevision: 1 } })).ok(), false);
    assert.deepEqual(pageErrors, []);
    events.push('Rendered grant/revoke, custom-role create/revise/retire, assignment/removal, explicit Pending/Uncertain reconciliation and typed Owner handoff passed.',
        'Unknown-save envelope replay, current authority denial, malformed/failed reads, conflicts, CSRF and token custody passed.',
        'Policy draft edit and immutable publication with revision advancement passed; desktop, 320px, forced-colors, keyboard and automated accessibility checks passed.');
    fs.writeFileSync(path.join(output, 'tenant-administration-browser-results.json'), JSON.stringify({ browser: await browser.version(), events, requestCount: requests.length, verificationChecks, backendProvider: 'NOT_RUN_CONTROLLED_HTTP_CONTRACT_FIXTURE', liveIdentityProvider: 'NOT_RUN_CONTROLLED_LOCAL_ISSUER' }, null, 2));
    console.log(events.join('\n'));

}
run().catch(async error => { console.error(error); console.error(JSON.stringify({ url: activePage?.url(), requests: requests.slice(-12), status: activePage ? await activePage.locator('.business-status').textContent({ timeout: 1000 }).catch(() => null) : null })); if (activePage) await activePage.screenshot({ path: path.join(output, 'failure.png'), fullPage: true }).catch(() => {}); process.exitCode = 1; }).finally(async () => {
    holdBusiness = false; releaseBusiness?.(); if (browser) await browser.close(); await stopWeb(); fixture.closeAllConnections(); await new Promise(resolve => fixture.close(resolve));
    fs.closeSync(log); fs.rmSync(temporary, { recursive: true, force: true });
});
