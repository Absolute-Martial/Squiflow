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
const output = process.env.APPLICATION_UI_ARTIFACTS || path.join(root, 'artifacts/operations-web-browser');
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
const requests = [], receipts = new Map(), suppliers = [], purchases = [], supplierEvents = new Map(), purchaseEvents = [];
const orderId = crypto.randomUUID(), unitId = crypto.randomUUID();
let work, workEvents = [], failure, lostSave = false, malformed = false, releaseBusiness, holdBusiness = false;
const now = () => new Date().toISOString();
async function business(request, response, uri, tenantId, resource) {
    assert.equal(request.headers.authorization, 'Bearer ' + accessToken);
    requests.push({ tenantId, resource, method: request.method });
    const finish = (value, status = 200) => json(response, value, status);
    if (holdBusiness) await new Promise(resolve => { releaseBusiness = resolve; });
    if (denyWorkspace || tenantId !== first.tenantId) return finish({ diagnostic: 'private tenant diagnostic' }, 403);
    if (failure) return finish({ diagnostic: 'private supplier SQL diagnostic' }, failure);
    if (malformed) return finish({ unsupported: true });
    let body;
    if (request.method !== 'GET') { let text = ''; for await (const chunk of request) { text += chunk; assert.ok(Buffer.byteLength(text) <= 8192); } body = JSON.parse(text); }
    const key = request.headers['idempotency-key'];
    if (body) {
        assert.match(key, /^[a-f0-9]{32}$/);
        const saved = receipts.get(key);
        if (saved) { assert.deepEqual({ resource, body }, saved.intent); response.setHeader('Idempotency-Replayed', 'true'); return finish(saved.value); }
    } else assert.equal(key, undefined);
    function save(value) { receipts.set(key, { intent: { resource, body }, value: structuredClone(value) }); if (lostSave) { lostSave = false; response.destroy(); return; } return finish(value); }
    const page = rows => ({ items: uri.searchParams.has('after') ? [] : rows.slice(0, 25), nextCursor: rows.length >= 25 && !uri.searchParams.has('after') ? 'bmV4dA' : null });
    const history = (rows, revision) => { const before = Number(uri.searchParams.get('beforeRevision') || Number.MAX_SAFE_INTEGER), items = rows.filter(x => x.revision < before).slice().reverse().slice(0, 10); return { observedRevision: revision, items, nextRevision: rows.filter(x => x.revision < before).length > 10 ? items.at(-1).revision : null }; };
    if (resource === 'fulfillment') return finish(page(work ? [work] : []));
    const fulfillment = resource.match(/^orders\/([^/]+)\/fulfillment(?:\/(.+))?$/);
    if (fulfillment) {
        assert.equal(fulfillment[1], orderId);
        const action = fulfillment[2];
        if (!body) return !work ? finish({}, 404) : finish(action === 'history' ? history(workEvents, work.revision) : work);
        const revision = work?.revision ?? 0;
        assert.equal(body.expectedRevision, revision);
        if (action === 'start') { assert.deepEqual(body, { expectedRevision: 0, expectedOrderRevision: 7 }); work = { workId: crypto.randomUUID(), orderId, revision: 0, stage: 'InProgress', started: true, orderedQuantity: 5, completedQuantity: 0, handedOffQuantity: 0, unitCode: 'EA', unitPrecision: 0, activeDispositionId: null, activeDispositionKind: null, createdAt: now(), updatedAt: now() }; }
        const event = { eventId: crypto.randomUUID(), revision: revision + 1, actorAccountId: accountId, recordedAt: now(), authorizationRevision: 7, kind: { start: 'Started', complete: 'Completion', handoffs: 'Handoff', dispositions: 'Disposition', resolve: 'Resolution', corrections: 'Correction' }[action], quantity: body.quantity ?? null, recipient: body.recipient ?? null, reason: body.reason ?? null, disposition: body.disposition ?? null, targetEventId: body.targetEventId ?? null, replacement: body.replacement ?? null, isActive: true };
        assert.ok(event.kind);
        if (action === 'complete' || action === 'handoffs') { assert.equal(work.activeDispositionId, null); assert.ok(body.quantity > 0 && Number.isInteger(body.quantity)); }
        if (action === 'complete') work.completedQuantity += body.quantity;
        if (action === 'handoffs') { assert.equal(typeof body.recipient, 'string'); work.handedOffQuantity += body.quantity; }
        if (action === 'dispositions') { work.activeDispositionId = event.eventId; work.activeDispositionKind = body.disposition; }
        if (action === 'resolve') { work.activeDispositionId = null; work.activeDispositionKind = null; }
        if (action === 'corrections') { const target = workEvents.find(x => x.eventId === body.targetEventId); assert.ok(target); target.isActive = false; assert.equal(target.kind, 'Handoff'); assert.equal(body.replacement.kind, 'Handoff'); work.handedOffQuantity += body.replacement.quantity - target.quantity; }
        work.revision++; work.updatedAt = now(); work.stage = work.activeDispositionId ? 'Paused' : work.completedQuantity < 5 ? 'InProgress' : work.handedOffQuantity < 5 ? 'ReadyForPickup' : 'HandedOff'; workEvents.push(event);
        return save({ workId: work.workId, orderId, revision: work.revision, stage: work.stage });
    }
    if (resource === 'purchasing/suppliers') return finish(page(suppliers));
    if (resource === 'purchasing/requests') return finish(page(purchases));
    const supplier = resource.match(/^purchasing\/suppliers\/([^/]+)(?:\/(.+))?$/);
    if (supplier) {
        const id = supplier[1], action = supplier[2], current = suppliers.find(x => x.supplierId === id);
        if (!body) {
            if (!current) return finish({}, 404);
            if (action === 'history') return finish(history(supplierEvents.get(id), current.revision));
            if (action === 'balance') { const rows = purchases.filter(x => x.supplier.supplierId === id); return finish({ supplierId: id, currency: 'NPR', totalCost: rows.reduce((s,x) => s+x.totalCost,0), paidAmount: rows.reduce((s,x) => s+x.paidAmount,0), refundedAmount: rows.reduce((s,x) => s+x.refundedAmount,0), outstandingPayable: rows.reduce((s,x) => s+x.outstandingPayable,0) }); }
            return finish(current);
        }
        assert.equal(body.expectedRevision, current?.revision ?? 0);
        const value = current ?? { supplierId: id, revision: 0, status: 'Active', createdAt: now() };
        if (!current) { suppliers.push(value); supplierEvents.set(id, []); }
        if (action === 'retire') value.status = 'Retired'; else { value.name = body.name; value.contact = body.contact; }
        value.revision++; value.updatedAt = now();
        supplierEvents.get(id).push({ eventId: crypto.randomUUID(), revision: value.revision, actorAccountId: accountId, recordedAt: now(), authorizationRevision: 7, kind: action === 'retire' ? 'Retired' : action === 'revise' ? 'Revised' : 'Created', supplier: structuredClone(value) });
        return save({ id, revision: value.revision });
    }
    const purchase = resource.match(/^purchasing\/requests\/([^/]+)(?:\/(.+))?$/);
    if (purchase) {
        const id = purchase[1], action = purchase[2]; let value = purchases.find(x => x.purchaseId === id);
        if (!body) return !value ? finish({},404) : finish(action === 'history' ? history(purchaseEvents.filter(x => x.purchaseId === id), value.revision) : value);
        if (body.expectedRevision !== (value?.revision ?? 0)) return finish({},409);
        const eventId = crypto.randomUUID();
        if (!action) { const source = suppliers.find(x => x.supplierId === body.supplierId); assert.equal(source.status, 'Active'); assert.equal(body.unitId, unitId); value = { purchaseId: id, revision: 0, supplier: structuredClone(source), unitId, unitCode: 'EA', unitPrecision: 0, unitRevision: 1, order: body.order, description: body.description, requestedQuantity: body.quantity, receivedQuantity: 0, remainingQuantity: body.quantity, cancelledQuantity: null, unfulfilledQuantity: body.quantity, currency: 'NPR', totalCost: 0, paidAmount: 0, refundedAmount: 0, outstandingPayable: 0, receipts: [], payments: [], refunds: [], createdAt: now(), updatedAt: now() }; purchases.push(value); }
        if (action === 'receipts') value.receipts.push({ entryId: eventId, effectiveEventId: eventId, quantity: body.quantity, cost: body.cost, isVoided: false });
        if (action === 'payments') { assert.ok(value.receipts.some(x => x.entryId === body.receiptId)); value.payments.push({ entryId: eventId, effectiveEventId: eventId, receiptId: body.receiptId, amount: body.amount, method: body.method, reference: body.reference, isVoided: false }); }
        if (action === 'refunds') { assert.ok(value.payments.some(x => x.entryId === body.paymentId)); value.refunds.push({ entryId: eventId, effectiveEventId: eventId, paymentId: body.paymentId, amount: body.amount, method: body.method, reference: body.reference, isVoided: false }); }
        if (action === 'corrections') { const target = value.receipts.find(x => x.effectiveEventId === body.targetEventId); assert.ok(target); assert.equal(body.replacement.kind, 'Receipt'); Object.assign(target, { effectiveEventId: eventId, quantity: body.replacement.quantity, cost: body.replacement.amount }); }
        value.receivedQuantity = value.receipts.filter(x => !x.isVoided).reduce((s,x) => s+x.quantity,0);
        if (action === 'cancel') value.cancelledQuantity = value.requestedQuantity - value.receivedQuantity;
        value.remainingQuantity = value.cancelledQuantity === null ? value.requestedQuantity - value.receivedQuantity : 0;
        value.unfulfilledQuantity = value.requestedQuantity - value.receivedQuantity - (value.cancelledQuantity ?? 0);
        value.totalCost = value.receipts.reduce((s,x) => s+(x.isVoided ? 0 : x.cost),0); value.paidAmount = value.payments.reduce((s,x) => s+(x.isVoided ? 0 : x.amount),0); value.refundedAmount = value.refunds.reduce((s,x) => s+(x.isVoided ? 0 : x.amount),0); value.outstandingPayable = Number((value.totalCost - value.paidAmount + value.refundedAmount).toFixed(2));
        value.revision++; value.updatedAt = now();
        const kind = { receipts: 'Receipt', payments: 'Payment', refunds: 'Refund', corrections: 'Correction', cancel: 'Cancellation' }[action] ?? 'Requested';
        const facts = kind === 'Correction' ? null : { kind, quantity: body.quantity ?? (kind === 'Cancellation' ? value.cancelledQuantity : null), amount: body.cost ?? body.amount ?? null, receiptId: body.receiptId ?? null, paymentId: body.paymentId ?? null, method: body.method ?? null, reference: body.reference ?? null };
        purchaseEvents.push({ purchaseId: id, eventId, revision: value.revision, actorAccountId: accountId, recordedAt: now(), authorizationRevision: 7, kind, facts, reason: body.reason ?? null, targetEventId: body.targetEventId ?? null, replacement: body.replacement ?? null });
        return save({ id, revision: value.revision });
    }
    return finish({}, 404);
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
const log = fs.openSync(path.join(output, 'operations-web-host.log'), 'w');
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
        const businessMatch = uri.pathname.match(/^\/api\/v1\/tenants\/([^/]+)\/(orders\/.*|fulfillment|purchasing\/.*)$/);
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
            if (name === 'Start service work') { await page.getByRole('button', { name, exact: true }).focus(); await page.keyboard.press('Enter'); }
            else await page.getByRole('button', { name, exact: true }).click();
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
    await go('fulfillment'); await capture('fulfillment-empty-desktop');
    await page.getByLabel('Committed Order reference', { exact: true }).focus(); await page.keyboard.press('Tab'); assert.equal(await page.evaluate(() => document.activeElement.id), 'work-revision');
    await fill('Committed Order reference', orderId); await page.getByText('Start eligible work', { exact: true }).click();
    await fill('Expected committed Order revision', 7);
    await page.getByLabel('I reviewed this exact committed Order and fulfillment revision.', { exact: true }).check();
    lostSave = true; await click('Start service work'); await message('save outcome is unknown');
    assert.equal(work.revision, 1); assert.ok(await page.getByLabel('Committed Order reference', { exact: true }).isDisabled());
    await capture('fulfillment-unknown'); await click('Retry saved request'); await message('earlier request was already saved'); assert.equal(work.revision, 1);
    await click('Load service work'); await saved(); await page.getByRole('heading', { name: 'Current service work', exact: true }).waitFor();
    await page.getByText('Record good completion', { exact: true }).click(); await fill('Good-completion quantity', 5);
    await page.getByLabel('I reviewed the completed quantity and current revision.', { exact: true }).check(); await click('Record completion'); await saved();
    await page.getByText('Record customer pickup', { exact: true }).click(); await fill('Pickup quantity', 2); await fill('Pickup recipient label', '<script>Recipient</script>');
    await page.getByLabel('I reviewed the actual pickup quantity and recipient.', { exact: true }).check(); await click('Record pickup'); await saved();
    const pickupEvent = workEvents.find(x => x.kind === 'Handoff');
    await page.getByText('Pause or resolve work', { exact: true }).click(); await page.getByLabel('Disposition', { exact: true }).selectOption('Defective');
    await fill('Affected quantity (optional)', 1); await fill('Disposition reason', '<script>Defective edge</script>');
    await page.getByLabel('I reviewed this blocker and its reason.', { exact: true }).check(); await click('Record disposition'); await saved();
    await click('Load service work'); await saved(); await page.getByText('Paused', { exact: true }).first().waitFor(); assert.equal(work.completedQuantity, 5); assert.equal(work.handedOffQuantity, 2);
    await fill('Resolution reason', 'Work checked and released'); await page.getByLabel('I reviewed the active disposition and this resolution.', { exact: true }).check();
    await click('Resolve disposition'); await saved();
    await page.getByText('Correct a fulfillment entry', { exact: true }).click(); await fill('Fulfillment event reference to correct', pickupEvent.eventId);
    await fill('Fulfillment correction reason', 'Operator counted one, not two'); await page.getByLabel('Replacement fulfillment fact', { exact: true }).selectOption('Handoff');
    await fill('Replacement quantity (optional for disposition)', 1); await fill('Replacement recipient label', 'Corrected recipient');
    assert.ok(await page.getByRole('button', { name: 'Correct fulfillment entry', exact: true }).isDisabled());
    await fill('Type “Correct fulfillment” to confirm', 'Correct fulfillment'); await click('Correct fulfillment entry'); await saved(); assert.equal(work.handedOffQuantity, 1);
    await click('Load service work'); await saved(); await click('Load fulfillment history'); await saved();
    await page.getByText('Revision 6 · Correction · Active', { exact: true }).waitFor();
    assert.equal(await page.locator('script').filter({ hasText: 'Defective edge' }).count(), 0);
    for (let i = 0; i < 3; i++) {
        await page.getByLabel('I reviewed this blocker and its reason.', { exact: true }).check(); await click('Record disposition'); await saved();
        await page.getByLabel('I reviewed the active disposition and this resolution.', { exact: true }).check(); await click('Resolve disposition'); await saved();
    }
    await click('Load fulfillment history'); await saved();
    await click('Earlier fulfillment events'); await saved(); await page.getByText('Revision 1 · Started · Active', { exact: true }).waitFor();
    await click('Refresh fulfillment inbox'); await saved(); await capture('fulfillment-desktop');
    await page.setViewportSize({ width: 320, height: 844 }); await capture('fulfillment-mobile-320');
    await page.emulateMedia({ forcedColors: 'active', reducedMotion: 'reduce' }); await capture('fulfillment-forced-colors');
    await page.emulateMedia({ forcedColors: 'none' }); await page.setViewportSize({ width: 1440, height: 1000 });
    failure = 403; await click('Load service work'); await message('Each operation needs its own permission');
    assert.equal(await page.getByRole('heading', { name: 'Record service progress', exact: true }).count(), 1); failure = undefined;
    malformed = true; await click('Load service work'); await message('unsupported response'); assert.equal(await page.getByRole('heading', { name: 'Current service work', exact: true }).count(), 0); malformed = false;
    failure = 409; await page.getByLabel('I reviewed the completed quantity and current revision.', { exact: true }).check(); await click('Record completion'); await message('conflicts with current records'); failure = undefined;
    await go('purchasing'); await capture('purchasing-empty-desktop');
    await page.getByLabel('Supplier name', { exact: true }).focus(); await page.keyboard.press('Tab'); assert.equal(await page.evaluate(() => document.activeElement.id), 'supplier-contact');
    await fill('Supplier name', '<script>Supplier</script>'); await fill('Supplier contact (optional)', 'Synthetic contact');
    await page.getByLabel('I reviewed this supplier name and contact.', { exact: true }).check(); await click('Create supplier'); await saved(); assert.equal(suppliers.length, 1);
    const supplier = suppliers[0]; await click('Load supplier'); await saved(); await fill('Supplier name', 'Revised supplier');
    await page.getByLabel('I reviewed this supplier name and contact.', { exact: true }).check(); await click('Revise supplier'); await saved();
    await fill('Request supplier reference', supplier.supplierId); await fill('Catalog unit reference', unitId); await fill('Supplier service description', '<script>Actual print service</script>'); await fill('Requested service quantity', 5);
    await page.getByLabel('I reviewed the supplier, unit, service quantity and optional Order link.', { exact: true }).check();
    lostSave = true; await click('Save supplier service request'); await message('save outcome is unknown'); assert.equal(purchases.length, 1);
    assert.ok(await page.getByLabel('Request supplier reference', { exact: true }).isDisabled());
    await capture('purchasing-unknown'); await click('Retry saved request'); await message('earlier request was already saved'); assert.equal(purchases.length, 1);
    const purchase = purchases[0]; await click('Load service request'); await saved();
    await page.getByText('Record actual service receipt', { exact: true }).click(); await fill('Actual received quantity', 2); await fill('Actual receipt total cost (NPR)', '30.01');
    await page.getByLabel('I reviewed the actual received quantity and its total cost.', { exact: true }).check(); await click('Record supplier receipt'); await saved();
    await click('Load service request'); await saved(); const payable = purchase.receipts[0];
    await page.getByText('Record payment to supplier', { exact: true }).click(); await fill('Payable receipt reference', payable.entryId); await fill('Actual supplier payment amount (NPR)', '20.00');
    await page.getByLabel('Supplier payment method', { exact: true }).selectOption('Bank'); await fill('Supplier payment reference (optional)', '<script>Payment ref</script>');
    await page.getByLabel('I reviewed this actual payment and exact receipt payable.', { exact: true }).check(); await click('Record supplier payment'); await saved();
    await click('Load service request'); await saved(); const payment = purchase.payments[0];
    await page.getByText('Record actual supplier refund', { exact: true }).click(); await fill('Original supplier payment reference', payment.entryId); await fill('Actual supplier refund amount (NPR)', '5.00');
    await page.getByLabel('I reviewed the actual refund received and original supplier payment.', { exact: true }).check(); await click('Record supplier refund'); await saved();
    await page.getByText('Correct supplier financial entry', { exact: true }).click(); await fill('Supplier service event reference to correct', payable.effectiveEventId); await fill('Supplier financial correction reason', 'Correct recorded receipt total');
    await page.getByLabel('Replacement supplier fact', { exact: true }).selectOption('Receipt'); await fill('Replacement received quantity', 2); await fill('Replacement amount or receipt cost (NPR)', '25.01');
    assert.ok(await page.getByRole('button', { name: 'Correct supplier entry', exact: true }).isDisabled());
    await fill('Type “Correct supplier entry” to confirm', 'Correct supplier entry'); await click('Correct supplier entry'); await saved(); assert.equal(purchase.totalCost, 25.01);
    await page.getByText('Cancel unreceived remainder', { exact: true }).click(); await fill('Cancellation reason', 'Remaining service no longer needed'); await fill('Type “Cancel remainder” to confirm', 'Cancel remainder');
    await click('Cancel unreceived quantity'); await saved(); assert.equal(purchase.cancelledQuantity, 3); assert.equal(purchase.receivedQuantity, 2);
    await click('Load service request'); await saved(); await click('Load supplier service history'); await saved();
    await page.getByText('Revision 6 · Cancellation', { exact: true }).waitFor();
    await fill('Saved supplier reference', supplier.supplierId); await click('Load supplier balance'); await saved(); assert.equal(purchase.outstandingPayable, 10.01);
    await click('Load supplier history'); await saved(); await click('Refresh suppliers'); await saved(); await click('Refresh service requests'); await saved();
    await capture('purchasing-desktop'); await page.setViewportSize({ width: 320, height: 844 }); await capture('purchasing-mobile-320');
    await page.emulateMedia({ forcedColors: 'active', reducedMotion: 'reduce' }); await capture('purchasing-forced-colors'); await page.emulateMedia({ forcedColors: 'none' });
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.getByText('Retire supplier', { exact: true }).click(); await fill('Type “Retire supplier” to confirm', 'Retire supplier'); await click('Retire saved supplier'); await saved(); assert.equal(supplier.status, 'Retired');
    failure = 403; await click('Refresh suppliers'); await message('Each operation needs its own permission'); assert.equal(await page.getByRole('button', { name: 'Create supplier', exact: true }).count(), 1); failure = undefined;
    malformed = true; await click('Refresh service requests'); await message('unsupported response'); malformed = false;
    failure = 503; await click('Refresh service requests'); await message('service is unavailable'); failure = undefined;
    for (let i = suppliers.length; i < 25; i++) suppliers.push({ ...supplier, supplierId: crypto.randomUUID(), name: 'Supplier ' + i });
    await click('Refresh suppliers'); await saved(); await click('Next suppliers'); await saved(); await page.getByText('No suppliers on this page.', { exact: false }).waitFor();
    await page.getByRole('button', { name: 'Verify identity for purchasing correction', exact: true }).click();
    await page.waitForURL(`${webOrigin}/workspace/${first.tenantId}/billing`); assert.equal(verificationChecks, 1);
    await go('fulfillment'); denyWorkspace = true; await go('purchasing'); await page.getByRole('heading', { name: 'Workspace unavailable', exact: true }).waitFor();
    assert.equal(await page.getByRole('button', { name: 'Create supplier', exact: true }).count(), 0); denyWorkspace = false;
    await go('purchasing'); assert.ok(!(await page.content()).includes(accessToken)); assert.ok(!(await page.content()).includes(clientSecret)); assert.ok(!(await page.content()).includes('private supplier SQL diagnostic'));
    assert.deepEqual(await page.evaluate(() => ({ local: Object.keys(localStorage), session: Object.keys(sessionStorage) })), { local: [], session: [] });
    assert.equal((await context.request.post(webOrigin + '/auth/verify-identity', { form: { tenantId: first.tenantId } })).status(), 400);
    assert.equal((await context.request.post(page.url(), { data: { expectedRevision: 1 } })).ok(), false);
    assert.deepEqual(pageErrors, []);
    events.push('Fulfillment rendered start unknown-save replay, exact completion/pickup, disposition/resolution, typed linked correction, inbox/history and denial/malformed/conflict states passed.',
        'Purchasing rendered supplier create/revise/retire, request unknown-save replay, actual receipt/payment/refund, typed receipt correction, remaining cancellation, history and supplier balance passed.',
        'Desktop, 320px reflow/axe/target checks and forced-colors snapshots passed; controlled fresh identity, CSRF, tenant denial and token custody passed.');
    fs.writeFileSync(path.join(output, 'operations-browser-results.json'), JSON.stringify({ browser: await browser.version(), events, requestCount: requests.length, verificationChecks, backendProvider: 'NOT_RUN_CONTROLLED_HTTP_CONTRACT_FIXTURE', liveIdentityProvider: 'NOT_RUN_CONTROLLED_LOCAL_ISSUER' }, null, 2));
    console.log(events.join('\n'));
}
run().catch(async error => { console.error(error); console.error(JSON.stringify({ url: activePage?.url(), requests: requests.slice(-12), status: activePage ? await activePage.locator('.business-status').textContent({ timeout: 1000 }).catch(() => null) : null })); if (activePage) await activePage.screenshot({ path: path.join(output, 'failure.png'), fullPage: true }).catch(() => {}); process.exitCode = 1; }).finally(async () => {
    holdBusiness = false; releaseBusiness?.(); if (browser) await browser.close(); await stopWeb(); fixture.closeAllConnections(); await new Promise(resolve => fixture.close(resolve));
    fs.closeSync(log); fs.rmSync(temporary, { recursive: true, force: true });
});
