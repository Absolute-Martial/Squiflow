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
const output = process.env.APPLICATION_UI_ARTIFACTS || path.join(root, 'artifacts/quotation-web-browser');
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
let expiryClockOffset = 0;
const now = () => new Date(Date.now() + expiryClockOffset).toISOString();
const actor = () => crypto.randomUUID();
const quotes = [], approvals = [], requests = [], receipts = new Map(), approvalEvents = new Map(), issuedHistory = new Map();
let failure, readDenied = false, malformed = false, lostSave = false, holdBusiness = false, releaseBusiness;
const itemId = crypto.randomUUID(), unitId = crypto.randomUUID();
const policy = { profileId: actor(), policyRevisionId: actor(), requireReferenceForProgramOrders: true, requireApprovalForProgramOrders: true };
const order = { orderId: actor(), summary: '<script>Program Order</script>', currencyCode: 'NPR', total: 25, revision: 1, createdAt: now(),
    lines: [{ position: 1, description: 'Printing', quantity: 2, unitCode: 'EA', unitPrice: 12.5, lineTotal: 25 }], state: 'draft', abandonedAt: null, committedAt: null,
    customerContext: { organizationId: actor(), programId: actor() }, quotationOrigin: null, programPolicy: policy, externalProgramReference: null };
function approvalSnapshot(requestedBy = actor(), assigned = accountId) {
    return { requestId: actor(), orderId: order.orderId, requestedByAccountId: requestedBy, assignedAccountId: assigned, revision: 1, state: 1, createdAt: now(), updatedAt: now(),
        subject: { schemaVersion: 1, order: structuredClone(order), revisionAuthorAccountId: actor(), fingerprint: 'a'.repeat(64) }, lastActorAccountId: requestedBy, authorizationRevision: 1, change: 1, reason: null };
}
async function business(request, response, uri, tenantId, resource) {
    assert.equal(request.headers.authorization, 'Bearer ' + accessToken);
    requests.push({ resource, tenantId, method: request.method });
    const reply = (value, status = 200) => json(response, value, status);
    if (tenantId !== first.tenantId || denyWorkspace) return reply({ diagnostic: 'private tenant diagnostic' }, 403);
    if (failure) return reply({ diagnostic: 'private SQL bearer diagnostic' }, failure);
    if (request.method === 'GET' && readDenied) return reply({ diagnostic: 'private view diagnostic' }, 403);
    if (request.method === 'GET' && malformed) return reply({ items: [null], nextCursor: 'https://other.example.test' });
    if (holdBusiness) await new Promise(resolve => { releaseBusiness = resolve; });
    let body;
    if (request.method !== 'GET') {
        let text = ''; for await (const chunk of request) { text += chunk; assert.ok(text.length <= (resource.startsWith('quotations') ? 65536 : 8192)); }
        body = JSON.parse(text);
    }
    if (request.method === 'GET' && resource === 'customers/organizations') return reply({ items: [{ organizationId: order.customerContext.organizationId, tenantId, displayName: '<script>Saved organization</script>', createdAt: now() }], nextCursor: null });
    if (request.method === 'GET' && resource === `customers/organizations/${order.customerContext.organizationId}/programs`) return reply({ items: [{ programId: order.customerContext.programId, organizationId: order.customerContext.organizationId, tenantId, displayName: 'Saved program', createdAt: now() }], nextCursor: null });
    if (request.method === 'GET' && resource === 'catalog/items') return reply({ items: [{ itemId, baseUnitId: unitId, code: 'PRINT', name: 'Saved printing service', description: null, kind: 'service', stockMode: 'nonStock', availability: null, status: 'active', revision: 1, createdAt: now(), retiredAt: null, availabilityChangedAt: null }], nextCursor: null });
    const key = request.headers['idempotency-key'];
    if (request.method !== 'GET') {
        assert.match(key, /^[a-f0-9]{32}$/);
        const previous = receipts.get(key);
        if (previous) {
            assert.deepEqual(previous.intent, { resource, method: request.method, body });
            response.setHeader('Idempotency-Replayed', 'true'); return reply(previous.value);
        }
    }
    const save = (value, created = false) => {
        receipts.set(key, { intent: { resource, method: request.method, body }, value: structuredClone(value) });
        if (lostSave) { lostSave = false; response.destroy(); return; }
        return reply(value, created ? 201 : 200);
    };
    const quotePath = resource.match(/^quotations(?:\/([^/]+)(?:\/(draft|issue|accept|reject|expire|convert|issued)(?:\/([^/]+)\/response)?)?)?$/);
    if (quotePath) {
        let quote = quotes.find(x => x.quotationId === quotePath[1]); const action = quotePath[2];
        if (request.method === 'GET') {
            if (!quote) return reply({}, 404);
            if (action === 'issued' && quotePath[3]) return quote.currentResponse?.issuedRevisionId === quotePath[3] ? reply(quote.currentResponse) : reply({}, 404);
            if (action === 'issued') {
                const rows = (issuedHistory.get(quote.quotationId) ?? []).filter(x => x.revisionNumber > Number(uri.searchParams.get('afterRevision') || 0));
                const items = rows.slice(0, Number(uri.searchParams.get('limit') || 20));
                return reply({ items, nextAfterRevision: rows.length > items.length ? items.at(-1).revisionNumber : null });
            }
            return reply(quote);
        }
        if (!action || action === 'draft') {
            assert.equal(request.method, action ? 'PUT' : 'POST');
            if (quote && (body.expectedVersion !== quote.version || quote.currentResponse?.kind === 'accepted')) return reply({}, 409);
            if (!quote) { quote = { quotationId: actor(), tenantId, version: 0, number: null, createdByAccountId: accountId, createdAt: now(), draft: null, currentIssued: null, currentResponse: null, conversion: null }; quotes.push(quote); }
            const lines = body.lines.map((line, index) => {
                if (body.mode === 'manual') { assert.equal(line.itemId, null); assert.equal(line.overridePrice, null); }
                else { assert.equal(line.description, null); assert.equal(line.unitPrice, null); assert.equal(line.itemId, itemId); assert.equal(line.unitId, unitId); }
                const price = body.mode === 'manual' ? line.unitPrice : line.overridePrice ?? 12.5;
                return { position: index + 1, description: body.mode === 'manual' ? line.description : 'Catalog printing', quantity: line.quantity, unitCode: body.mode === 'manual' ? line.unitCode : 'EA', unitPrice: price,
                    lineTotal: line.quantity * price, itemId: body.mode === 'catalog' ? itemId : null, unitId: body.mode === 'catalog' ? unitId : null, priceRevisionId: body.mode === 'catalog' ? actor() : null,
                    policyRevision: body.mode === 'catalog' ? 1 : null, override: line.overridePrice === null ? null : { unitPrice: line.overridePrice, reason: line.overrideReason, beyondPolicy: false } };
            });
            quote.version++; quote.draft = { ...body, total: lines.reduce((sum, line) => sum + line.lineTotal, 0), lines, validUntil: new Date(body.validUntil).toISOString() };
            return save(quote, !action);
        }
        if (!quote) return reply({}, 404);
        if (action === 'convert' && quote.conversion) return save({ quotationId: quote.quotationId, tenantId, version: quote.version, response: quote.currentResponse, conversion: quote.conversion });
        if (body.expectedVersion !== quote.version) return reply({}, 409);
        if (action === 'issue') {
            if (!quote.draft || quote.currentResponse?.kind === 'accepted' || Date.parse(quote.draft.validUntil) <= Date.now()) return reply({}, 409);
            const rows = issuedHistory.get(quote.quotationId) ?? [];
            quote.number ??= quotes.indexOf(quote) + 1;
            quote.currentIssued = { revisionId: actor(), revisionNumber: rows.length + 1, issuedByAccountId: accountId, issuedAt: now(), offer: structuredClone(quote.draft) };
            quote.draft = null; quote.currentResponse = null; quote.version++; rows.push(quote.currentIssued); issuedHistory.set(quote.quotationId, rows); return save(quote);
        }
        if (body.issuedRevisionId !== quote.currentIssued?.revisionId) return reply({}, 409);
        if (action === 'convert') {
            if (quote.currentResponse?.kind !== 'accepted') return reply({}, 409);
            quote.conversion = { quotationId: quote.quotationId, issuedRevisionId: body.issuedRevisionId, convertedByAccountId: accountId, convertedAt: now(), originalOrder: { orderId: order.orderId, revision: 1 } };
        } else {
            if (quote.currentResponse) return reply({}, 409);
            if (action === 'accept' && Date.parse(quote.currentIssued.offer.validUntil) <= Date.now() + expiryClockOffset || action === 'expire' && Date.parse(quote.currentIssued.offer.validUntil) > Date.now() + expiryClockOffset) return reply({}, 409);
            assert.ok(body.evidence); if (action === 'expire') assert.equal(body.customerClaim, null); else assert.ok(body.customerClaim);
            quote.currentResponse = { quotationId: quote.quotationId, issuedRevisionId: body.issuedRevisionId, kind: { accept: 'accepted', reject: 'rejected', expire: 'expired' }[action], recordedByAccountId: accountId,
                recordedAt: now(), evidence: body.evidence, customerClaim: body.customerClaim };
        }
        quote.version++; return save({ quotationId: quote.quotationId, tenantId, version: quote.version, response: quote.currentResponse, conversion: quote.conversion });
    }
    if (resource === `orders/${order.orderId}`) return reply(order);
    if (resource === `orders/${order.orderId}/program-reference`) {
        assert.equal(request.method, 'PUT'); if (body.expectedRevision !== order.revision) return reply({}, 409);
        order.externalProgramReference = body.externalProgramReference?.trim() || null; order.revision++;
        return save({ orderId: order.orderId, revision: order.revision, externalProgramReference: order.externalProgramReference, programPolicy: policy });
    }
    if (resource === `orders/${order.orderId}/approvals`) {
        if (body.expectedOrderRevision !== order.revision) return reply({}, 409);
        const pending = approvals.find(x => x.state === 1 && x.orderId === order.orderId);
        if (pending && (body.expectedPendingRequestRevision !== pending.revision || pending.subject.order.revision === order.revision)) return reply({}, 409);
        if (pending) { pending.state = 5; pending.change = 6; pending.revision++; }
        const value = approvalSnapshot(accountId, body.assignedAccountId); approvals.push(value); approvalEvents.set(value.requestId, [structuredClone(value)]);
        return save({ requestId: value.requestId, orderId: value.orderId, orderRevision: value.subject.order.revision, requestRevision: value.revision, state: value.state });
    }
    if (resource === 'order-approvals') {
        const rows = uri.searchParams.has('after') ? [] : approvals.map(x => ({ requestId: x.requestId, orderId: x.orderId, orderRevision: x.subject.order.revision, requestRevision: x.revision, state: x.state,
            requestedByAccountId: x.requestedByAccountId, assignedAccountId: x.assignedAccountId, createdAt: x.createdAt, subjectStale: x.subject.order.revision !== order.revision, assigneeUnavailable: false }));
        return reply({ items: rows.slice(0, 25), nextCursor: rows.length >= 25 ? 'bmV4dA' : null });
    }
    const approvalPath = resource.match(/^order-approvals\/([^/]+)(?:\/(history|approve|reject|cancel|reassign|recover-cancel))?$/);
    if (approvalPath) {
        const value = approvals.find(x => x.requestId === approvalPath[1]); const action = approvalPath[2]; if (!value) return reply({}, 404);
        if (request.method === 'GET') {
            if (action === 'history') return reply({ currentRevision: value.revision, items: (approvalEvents.get(value.requestId) ?? [value]).filter(x => !uri.searchParams.has('beforeRevision') || x.revision < Number(uri.searchParams.get('beforeRevision'))), nextRevision: null });
            return reply(value);
        }
        if (body.expectedRequestRevision !== value.revision || value.state !== 1) return reply({}, 409);
        if (['approve', 'reject'].includes(action) && (value.requestedByAccountId === accountId || value.subject.revisionAuthorAccountId === accountId || value.assignedAccountId !== accountId)) return reply({}, 403);
        if (['approve', 'reject'].includes(action) && value.subject.order.revision !== order.revision) return reply({}, 409);
        if (action === 'cancel' && value.requestedByAccountId !== accountId) return reply({}, 403);
        if (['reassign', 'recover-cancel'].includes(action) && verificationChecks === 0) return reply({}, 403);
        if (action !== 'approve') assert.ok(body.reason);
        if (action === 'reassign') value.assignedAccountId = body.assignedAccountId;
        value.state = { approve: 2, reject: 3, cancel: 4, reassign: 1, 'recover-cancel': 4 }[action];
        value.change = { approve: 2, reject: 3, cancel: 5, reassign: 4, 'recover-cancel': 5 }[action];
        value.revision++; value.updatedAt = now(); value.lastActorAccountId = accountId; value.reason = body.reason;
        const rows = approvalEvents.get(value.requestId) ?? []; rows.unshift(structuredClone(value)); approvalEvents.set(value.requestId, rows);
        return save({ requestId: value.requestId, orderId: value.orderId, orderRevision: value.subject.order.revision, requestRevision: value.revision, state: value.state });
    }
    return reply({}, 404);
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
let networkNavigationRecoveries = 0;
let pkceChecks = 0;
let verificationChecks = 0;
const codes = new Map();
const events = [];
const fixturePaths = [];
const browserResponses = [];
const log = fs.openSync(path.join(output, 'quotation-web-host.log'), 'w');
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
        const businessMatch = uri.pathname.match(/^\/api\/v1\/tenants\/([^/]+)\/(quotations(?:\/.*)?|order-approvals(?:\/.*)?|orders(?:\/.*)?|customers\/organizations(?:\/.*)?|catalog\/items)$/);
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
    const pageErrors = []; page.on('pageerror', error => pageErrors.push(error.message));
    page.on('response', response => browserResponses.push({ path: new URL(response.url()).pathname, status: response.status() }));
    page.on('dialog', dialog => dialog.dismiss());
    async function ready() { await page.locator('.business-status[aria-busy=false]').waitFor(); }
    async function navigate(url) {
        for (let attempt = 0; ; attempt++) {
            try { await page.goto(url); return; }
            catch (error) { if (attempt >= 2 || !error.message.includes('ERR_NETWORK_CHANGED')) throw error; networkNavigationRecoveries++; }
        }
    }
    async function go(resource) { await navigate(`${webOrigin}/workspace/${first.tenantId}/${resource}`); await ready(); }
    async function fill(label, value) { const input = page.getByLabel(label, { exact: true }); await input.fill(String(value)); await input.press('Tab'); }
    async function check(label) { await page.getByLabel(label, { exact: true }).check(); }
    async function select(label, value) { await page.getByLabel(label, { exact: true }).selectOption(value); }
    async function message(text) { await page.locator('.business-status').getByText(text, { exact: false }).waitFor(); }
    async function click(name) {
        console.log('Control:', name);
        const monitor = !['Add offer line', 'Sign out'].includes(name) && await page.locator('.business-status').count();
        if (monitor) await page.evaluate(() => {
            window.businessOperation = new Promise((resolve, reject) => {
                const deadline = setTimeout(() => { observer.disconnect(); reject(new Error('Business control did not finish rendering.')); }, 15000);
                const observer = new MutationObserver(mutations => {
                    const status = document.querySelector('.business-status');
                    if (status?.getAttribute('aria-busy') === 'false' && mutations.some(mutation => mutation.target === status && mutation.attributeName === 'aria-busy' && mutation.oldValue === 'true')) {
                        observer.disconnect(); clearTimeout(deadline); resolve();
                    }
                });
                observer.observe(document.body, { subtree: true, attributes: true, attributeOldValue: true, childList: true });
            });
        });
        await page.getByRole('button', { name, exact: true }).click();
        if (monitor) await page.evaluate(() => window.businessOperation);
    }
    const actionConfirmation = 'Record this exact action against the entered version and revision.';
    const approvalConfirmation = 'Record this exact action against the entered request revision.';
    async function quoteAction(action, button) { await select('Quotation action', action); await check(actionConfirmation); await click(button); }
    async function approvalAction(action, button, reason = 'Reviewed evidence') { await select('Approval action', action); await fill('Decision or recovery reason (required except approval)', reason); await check(approvalConfirmation); await click(button); }
    async function manualQuote(name) {
        await go('quotations'); await fill('Offer summary', name); await fill('Offer currency', 'NPR'); await fill('Valid until (ISO timestamp with Z or explicit offset)', new Date(Date.now() + 86400000).toISOString());
        await fill('Offer description 1', 'Printing'); await fill('Offer unit code 1', 'EA'); await fill('Offer unit price 1', 12.5);
        await click('Save quotation draft'); await message('Request completed.');
    }
    function seedApproval(requester = actor(), assigned = accountId) {
        const value = approvalSnapshot(requester, assigned); approvals.push(value); approvalEvents.set(value.requestId, [structuredClone(value)]); return value;
    }
    await navigate(webOrigin); await page.getByRole('button', { name: 'Sign in', exact: true }).waitFor();
    assert.equal((await context.request.post(webOrigin + '/auth/login', { form: {} })).status(), 400);
    await click('Sign in');
    await until(() => page.url() === webOrigin + '/workspace' || page.url().startsWith('chrome-error:'));
    if (page.url().startsWith('chrome-error:')) { networkNavigationRecoveries++; await navigate(webOrigin + '/workspace'); }
    await page.waitForURL(webOrigin + '/workspace');
    await page.getByLabel('North workspace', { exact: true }).check(); await click('Enter workspace');
    await page.getByRole('heading', { name: 'North workspace', exact: true }).waitFor();
    await go('quotations'); await audit(page, 'quotations-empty-desktop', output);
    const sharedSkipLink = await page.locator('.skip-link').evaluate(element => element.href);
    events.push('Shared skip-link observed: ' + sharedSkipLink + '; root shell fix excluded from this patch.');
    await page.getByLabel('Offer summary', { exact: true }).focus(); await page.keyboard.press('Tab');
    assert.equal(await page.getByLabel('Offer currency', { exact: true }).evaluate(element => element === document.activeElement), true);
    await fill('Offer summary', '<script>Quotation customer</script>'); await fill('Offer currency', 'NPR');
    await fill('Valid until (ISO timestamp with Z or explicit offset)', new Date(Date.now() + 86400000).toISOString());
    await fill('Offer terms (optional)', 'Terms <script>must remain text</script>'); await fill('Offer quantity 1', 2); await fill('Offer description 1', 'Print'); await fill('Offer unit code 1', 'EA'); await fill('Offer unit price 1', 12.5);
    await click('Add offer line'); await fill('Offer description 2', 'Setup'); await fill('Offer unit code 2', 'EA'); await fill('Offer unit price 2', 5);
    lostSave = true; await click('Save quotation draft'); await message('save outcome is unknown'); assert.equal(quotes.length, 1); assert.equal(quotes[0].draft.total, 30);
    assert.ok(await page.getByLabel('Offer summary', { exact: true }).isDisabled()); await audit(page, 'quotation-unknown-save', output);
    failure = 403; await click('Retry saved request'); await message('earlier save outcome is still unknown'); assert.ok(await page.getByLabel('Offer summary', { exact: true }).isDisabled());
    failure = undefined; await click('Retry saved request'); await message('earlier request was already saved'); assert.equal(quotes.length, 1);
    assert.equal(await page.locator('script').filter({ hasText: 'must remain text' }).count(), 0);
    await fill('Offer summary', 'Revised offer'); await check('Replace the complete saved draft with these values.'); await click('Replace quotation draft'); assert.equal(quotes[0].version, 2);
    await quoteAction('issue', 'Issue quotation'); assert.equal(quotes[0].number, 1); assert.equal(quotes[0].currentIssued.offer.total, 30);
    await fill('Offer summary', 'Replacement issued offer'); await check('Replace the complete saved draft with these values.'); await click('Replace quotation draft');
    await quoteAction('issue', 'Issue quotation'); assert.equal(quotes[0].currentIssued.revisionNumber, 2);
    await click('Load issued history'); assert.equal(issuedHistory.get(quotes[0].quotationId).length, 2); await click('Next issued revisions');
    await fill('Response evidence or expiry reason', 'Customer acceptance email <script>encoded</script>'); await fill('Responding customer claim (accept/reject)', 'Customer stated identity');
    await quoteAction('accept', 'Record acceptance'); assert.equal(quotes[0].currentResponse.kind, 'accepted');
    await click('Read exact issued response');
    await quoteAction('convert', 'Convert accepted quotation'); assert.ok(quotes[0].conversion);
    const linked = quotes[0].conversion.originalOrder.orderId;
    assert.ok((await page.getByRole('link', { name: 'Open linked order', exact: true }).getAttribute('href')).endsWith('/orders/' + linked));
    await quoteAction('convert', 'Convert accepted quotation'); assert.equal(quotes.length, 1);
    await audit(page, 'quotation-accepted-converted-desktop', output);
    await page.setViewportSize({ width: 320, height: 844 }); await audit(page, 'quotation-mobile-320', output); await page.setViewportSize({ width: 1440, height: 1000 });
    await page.getByRole('link', { name: 'Reload saved quotation', exact: true }).click(); await ready(); assert.equal(await page.getByRole('button', { name: 'Replace quotation draft', exact: true }).count(), 0);
    await manualQuote('Rejected offer'); await quoteAction('issue', 'Issue quotation'); await fill('Response evidence or expiry reason', 'Declined by customer'); await fill('Responding customer claim (accept/reject)', 'Customer representative'); await quoteAction('reject', 'Record rejection'); assert.equal(quotes[1].currentResponse.kind, 'rejected');
    await manualQuote('Expiring offer'); await quoteAction('issue', 'Issue quotation');
    await fill('Response evidence or expiry reason', 'Validity elapsed');
    await quoteAction('expire', 'Record expiry'); await message('conflicts with current records');
    // The controlled clock advances; the retained issued offer is unchanged.
    expiryClockOffset = 2 * 86400000;
    await check(actionConfirmation); await click('Record expiry'); assert.equal(quotes[2].currentResponse.kind, 'expired'); expiryClockOffset = 0;
    await go('quotations'); await fill('Offer summary', 'Catalog offer'); await fill('Offer currency', 'NPR'); await fill('Valid until (ISO timestamp with Z or explicit offset)', new Date(Date.now() + 86400000).toISOString());
    await page.getByText('Choose a saved organization and program', { exact: true }).click();
    const customerChoice = page.locator('details').filter({ has: page.getByText('Choose a saved organization and program', { exact: true }) });
    readDenied = true; await page.getByRole('button', { name: 'Browse saved organizations', exact: true }).click();
    await customerChoice.getByRole('status').getByText('You do not have access', { exact: false }).waitFor();
    assert.equal(await page.getByLabel('Organization reference (optional)', { exact: true }).isEnabled(), true); readDenied = false;
    malformed = true; await page.getByRole('button', { name: 'Browse saved organizations', exact: true }).click();
    await customerChoice.getByRole('status').getByText('unsupported response', { exact: false }).waitFor(); malformed = false;
    await page.getByRole('button', { name: 'Browse saved organizations', exact: true }).click();
    await page.getByLabel('Saved organization', { exact: true }).selectOption(order.customerContext.organizationId);
    await page.getByRole('button', { name: 'Browse programs for selected organization', exact: true }).click();
    await page.getByLabel('Saved program (optional)', { exact: true }).selectOption(order.customerContext.programId);
    await page.getByRole('button', { name: 'Use selected customer context', exact: true }).click();
    await page.getByText('Selected customer references copied into this draft. Review them before saving.', { exact: true }).waitFor();
    assert.equal(await page.getByLabel('Organization reference (optional)', { exact: true }).inputValue(), order.customerContext.organizationId);
    assert.equal(await page.getByLabel('Program reference (optional)', { exact: true }).inputValue(), order.customerContext.programId);
    await select('Offer pricing mode', 'catalog');
    await page.getByText('Choose a saved catalog item for offer line 1', { exact: true }).click();
    await page.getByRole('button', { name: 'Browse saved items for offer line 1', exact: true }).click();
    await page.getByLabel('Saved item for offer line 1', { exact: true }).selectOption(itemId);
    await page.getByRole('button', { name: 'Use saved item for offer line 1', exact: true }).click();
    await page.getByText('Item and base-unit references copied into this line. Review before saving.', { exact: true }).waitFor();
    assert.equal(await page.getByLabel('Offer item reference 1', { exact: true }).inputValue(), itemId);
    assert.equal(await page.getByLabel('Offer unit reference 1', { exact: true }).inputValue(), unitId);
    await audit(page, 'quotation-saved-selectors', output);
    await fill('Override price 1 (optional)', 20); await fill('Override reason 1 (required with override)', 'Authorized adjustment');
    await click('Save quotation draft'); assert.equal(quotes[3].draft.total, 20); await quoteAction('issue', 'Issue quotation'); await audit(page, 'quotation-catalog-override', output);
    await go(`orders/${order.orderId}/program-reference`); await audit(page, 'program-reference-empty', output);
    await fill('Program reference (empty to clear)', 'PO/2026/42'); await check('Replace or clear the supplied reference on this exact Order revision.');
    lostSave = true; await click('Save program reference'); await message('save outcome is unknown'); await click('Retry saved request'); await message('earlier request was already saved'); assert.equal(order.revision, 2);
    await page.reload(); await ready(); assert.equal(await page.getByLabel('Program reference (empty to clear)', { exact: true }).inputValue(), 'PO/2026/42');
    await fill('Program reference (empty to clear)', ''); await check('Replace or clear the supplied reference on this exact Order revision.'); await click('Save program reference'); assert.equal(order.externalProgramReference, null);
    await fill('Program reference (empty to clear)', 'PO/2026/43'); await check('Replace or clear the supplied reference on this exact Order revision.'); await click('Save program reference');
    await page.getByRole('link', { name: 'Request independent approval', exact: true }).click(); await ready();
    await fill('Assigned account reference', actor()); await check('Submit this exact Order revision; replace a stale pending request only at the explicitly entered request revision.');
    await click('Submit approval request'); const submitted = approvals[0];
    await page.getByRole('link', { name: 'Open saved approval request', exact: true }).click(); await ready();
    await approvalAction('approve', 'Approve submitted subject'); await message('Each operation needs its own permission'); assert.equal(submitted.state, 1);
    await audit(page, 'approval-self-decision-denied', output);
    await approvalAction('cancel', 'Cancel my approval request', 'Requester canceled'); assert.equal(submitted.state, 4);
    const cancelWithoutView = seedApproval(accountId, actor()); readDenied = true; await go('order-approvals'); await message('Each operation needs its own permission');
    await fill('Action request reference', cancelWithoutView.requestId); await fill('Expected request revision', 1); await approvalAction('cancel', 'Cancel my approval request', 'Cancel known request without view'); assert.equal(cancelWithoutView.state, 4); readDenied = false;
    const independent = seedApproval(); await go(`order-approvals/${independent.requestId}`);
    await audit(page, 'approval-subject-desktop', output); await approvalAction('approve', 'Approve submitted subject', 'Reviewed exact subject'); assert.equal(independent.state, 2);
    await click('Load approval history'); await page.getByRole('heading', { name: 'Request revision 2 · Approved · Approved', exact: true }).waitFor();
    const rejected = seedApproval(); await go(`order-approvals/${rejected.requestId}`); await approvalAction('reject', 'Reject approval request', 'Incorrect job details'); assert.equal(rejected.state, 3);
    const stale = seedApproval(); order.revision++; await go(`order-approvals/${stale.requestId}`); await approvalAction('approve', 'Approve submitted subject'); await message('conflicts with current records'); assert.equal(stale.state, 1);
    await go(`orders/${order.orderId}/approval`); await fill('Assigned account reference', actor()); await fill('Existing stale pending request revision (optional)', stale.revision); await check('Submit this exact Order revision; replace a stale pending request only at the explicitly entered request revision.'); await click('Submit approval request'); assert.equal(stale.state, 5);
    const recovering = approvals.at(-1); await go(`order-approvals/${recovering.requestId}`); await select('Approval action', 'reassign'); await fill('New assigned account reference', actor()); await fill('Decision or recovery reason (required except approval)', 'Assignee unavailable'); await check(approvalConfirmation); await click('Reassign approval request'); await message('Each operation needs its own permission');
    assert.equal((await context.request.post(webOrigin + '/auth/verify-identity', { form: { tenantId: first.tenantId } })).status(), 400);
    await page.getByRole('button', { name: 'Verify identity for approval recovery', exact: true }).click(); await page.waitForURL(`${webOrigin}/workspace/${first.tenantId}/billing`); assert.equal(verificationChecks, 1);
    await go(`order-approvals/${recovering.requestId}`); await select('Approval action', 'reassign'); const newAccount = actor(); await fill('New assigned account reference', newAccount); await fill('Decision or recovery reason (required except approval)', 'New eligible assignee'); await check(approvalConfirmation); await click('Reassign approval request'); assert.equal(recovering.assignedAccountId, newAccount);
    await approvalAction('recover-cancel', 'Owner cancel approval request', 'Owner resolved unavailable assignee'); assert.equal(recovering.state, 4);
    await go('order-approvals'); await page.setViewportSize({ width: 320, height: 844 }); await audit(page, 'approval-mobile-320', output);
    await page.emulateMedia({ forcedColors: 'active', reducedMotion: 'reduce' }); await audit(page, 'approval-forced-colors', output); await page.emulateMedia({ forcedColors: 'none' });
    malformed = true; await click('Refresh approvals'); await message('unsupported response'); assert.equal(await page.locator('#approval-inbox + ul li').count(), 0); malformed = false;
    failure = 503; await click('Refresh approvals'); await message('service is unavailable'); failure = undefined;
    const before = requests.length; denyWorkspace = true; await go('quotations'); await page.getByRole('heading', { name: 'Workspace unavailable', exact: true }).waitFor(); assert.equal(requests.length, before); denyWorkspace = false;
    await page.goto(`${webOrigin}/workspace/${second.tenantId}/order-approvals`); await message('Each operation needs its own permission'); assert.equal(await page.locator('.record-list li').count(), 0);
    await go('order-approvals'); const html = await page.content(); assert.ok(!html.includes(accessToken)); assert.ok(!html.includes(clientSecret)); assert.ok(!html.includes('private SQL'));
    assert.equal((await context.request.post(page.url(), { data: { assignedAccountId: actor() } })).ok(), false);
    assert.deepEqual(await page.evaluate(() => ({ local: Object.keys(localStorage), session: Object.keys(sessionStorage) })), { local: [], session: [] });
    await click('Sign out'); await page.getByRole('heading', { name: 'You are signed out.', exact: true }).waitFor();
    await page.goto(`${webOrigin}/workspace/${first.tenantId}/quotations`); assert.equal(new URL(page.url()).pathname, '/');
    assert.deepEqual(pageErrors, []);
    events.push('Quotation manual/catalog/override create, PUT replacement, immutable issue/history, acceptance/rejection/expiry/conversion and replay passed.',
        'Program-reference read/set/clear, pinned policy display, lost-response PUT replay and reload passed.',
        'Approval submit/detail/inbox/history, independent approve/reject, self-decision denial, stale supersession, requester cancellation without view, reassign/recovery cancellation and identity verification passed.',
        'Scoped rendered CSRF/session/tenant/error/encoding/token-custody/form-keyboard/reflow/axe checks passed; shared skip-link and audit incompletes require integrator review.');
    fs.writeFileSync(path.join(output, 'quotation-browser-results.json'), JSON.stringify({ browser: await browser.version(), events, requestCount: requests.length, networkNavigationRecoveries, liveProviders: 'NOT_RUN_CONTROLLED_HTTPS_CONTRACT_FIXTURE', excludedHelper: 'Local BusinessApiClient route allowance and quotation-only 64 KiB request cap' }, null, 2));
    console.log(events.join('\n'));
}
run().catch(async error => { console.error(error); console.error(JSON.stringify({ url: activePage?.url(), browserResponses: browserResponses.slice(-15), requests: requests.slice(-12), status: activePage ? await activePage.locator('.business-status').textContent().catch(() => '') : '' })); if (activePage) await activePage.screenshot({ path: path.join(output, 'failure.png'), fullPage: true }).catch(() => {}); process.exitCode = 1; }).finally(async () => {
    holdBusiness = false; releaseBusiness?.();
    if (browser) await browser.close(); await stopWeb(); fixture.closeAllConnections(); await new Promise(resolve => fixture.close(resolve));
    fs.closeSync(log); fs.rmSync(temporary, { recursive: true, force: true });
});
