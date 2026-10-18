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
const output = process.env.APPLICATION_UI_ARTIFACTS || path.join(root, 'artifacts/business-web-browser');
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
const organizations = [], programs = [], individuals = [], representatives = [], units = [], items = [], prices = [], conversions = [], orders = [], orderHistory = new Map();
const issuedInvoices = [], customerPayments = [], receivableRows = [], paymentEvents = [];
let pricingPolicy;
const receipts = new Map();
const requests = [];
let failure, lostSave = false, malformed = false, releaseBusiness, holdBusiness = false;
let pricingStatus = 'PriceResolved';
const now = () => new Date().toISOString();
async function business(request, response, uri, tenantId, resource) {
    assert.equal(request.headers.authorization, 'Bearer ' + accessToken);
    requests.push({ tenantId, resource, method: request.method });
    const finish = (value, status = 200) => json(response, value, status);
    if (denyWorkspace || tenantId !== first.tenantId) return finish({ diagnostic: 'private tenant diagnostic' }, 403);
    if (failure) return finish({ diagnostic: 'private SQL diagnostic and bearer must never render' }, failure);
    if (malformed) return finish({ items: [{ ...organizations[0], tenantId: second.tenantId }], nextCursor: null });
    if (holdBusiness) await new Promise(resolve => { releaseBusiness = resolve; });
    let body;
    if (request.method !== 'GET') {
        let text = ''; for await (const chunk of request) { text += chunk; assert.ok(text.length <= 8192); }
        body = JSON.parse(text);
    }
    const mutation = request.method !== 'GET' && !['pricing/resolve', 'pricing/history', 'orders/price-preview'].includes(resource);
    const key = request.headers['idempotency-key'];
    const receiptKey = tenantId + '/' + key;
    if (mutation) {
        assert.match(key, /^[a-f0-9]{32}$/);
        const saved = receipts.get(receiptKey);
        if (saved) {
            assert.deepEqual({ resource, body }, saved.intent);
            response.setHeader('Idempotency-Replayed', 'true'); return finish(saved.value);
        }
    } else assert.equal(key, undefined);
    const save = (value, created = false) => {
        receipts.set(receiptKey, { intent: { resource, body }, value: structuredClone(value) });
        if (lostSave) { lostSave = false; response.destroy(); return; }
        return finish(value, created ? 201 : 200);
    };
    const page = rows => ({ items: uri.searchParams.has('after') ? [] : rows.slice(0, 25), nextCursor: rows.length >= 25 && !uri.searchParams.has('after') ? 'bmV4dA' : null });
    if (resource === 'customers/organizations') {
        if (request.method === 'GET') return finish(page(organizations));
        assert.deepEqual(Object.keys(body), ['displayName']);
        const value = { organizationId: crypto.randomUUID(), tenantId, displayName: body.displayName, createdAt: now() }; organizations.unshift(value); return save(value, true);
    }
    const representative = resource.match(/^customers\/organizations\/([^/]+)\/representatives(?:\/([^/]+)(?:\/(unlink))?)?$/);
    if (representative) {
        if (request.method === 'GET') return finish(representatives.find(x => x.representativeId === representative[2] && x.organizationId === representative[1]) ?? {}, 200);
        if (representative[3]) {
            const value = representatives.find(x => x.representativeId === representative[2]);
            if (!value || value.revision !== body.expectedRevision) return finish({}, 409);
            value.availability = 'inactive'; value.revision++; value.changedAt = now(); return save(value);
        }
        assert.equal(individuals.find(x => x.individualId === body.individualId)?.availability, 'active');
        const value = { representativeId: crypto.randomUUID(), organizationId: representative[1], individualId: body.individualId, programId: body.programId, availability: 'active', revision: 1, createdAt: now(), changedAt: null }; representatives.push(value); return save(value, true);
    }
    const org = resource.match(/^customers\/organizations\/([^/]+)(?:\/programs(?:\/([^/]+))?)?$/);
    if (org) {
        if (!resource.includes('/programs')) return finish(organizations.find(x => x.organizationId === org[1]) ?? {}, 200);
        if (request.method === 'GET') return finish(org[2] ? programs.find(x => x.programId === org[2]) : page(programs.filter(x => x.organizationId === org[1])));
        const value = { programId: crypto.randomUUID(), tenantId, organizationId: org[1], displayName: body.displayName, createdAt: now() }; programs.unshift(value); return save(value, true);
    }
    if (resource === 'customers/individuals') {
        const value = { individualId: crypto.randomUUID(), ...body, availability: 'active', revision: 1, createdAt: now(), availabilityChangedAt: null, contactChangedAt: null, redirectTargetIndividualId: null }; individuals.push(value); return save(value, true);
    }
    const individual = resource.match(/^customers\/individuals\/([^/]+)(?:\/(contact|availability))?$/);
    if (individual) {
        const value = individuals.find(x => x.individualId === individual[1]); if (!value) return finish({}, 404);
        if (request.method === 'GET') return finish(value);
        if (body.expectedRevision !== value.revision) return finish({}, 409);
        value.revision++;
        if (individual[2] === 'contact') { Object.assign(value, { displayName: body.displayName, email: body.email, phone: body.phone, contactChangedAt: now() }); return save(value); }
        value.availability = body.availability; value.availabilityChangedAt = now();
        return save({ individualId: value.individualId, availability: value.availability, revision: value.revision, availabilityChangedAt: value.availabilityChangedAt });
    }
    if (resource === 'catalog/units' || resource === 'catalog/items') {
        const isUnit = resource.endsWith('units'), rows = isUnit ? units : items;
        if (request.method === 'GET') return finish(page(rows));
        const value = { ...(isUnit ? { unitId: crypto.randomUUID(), ...body } : { itemId: crypto.randomUUID(), ...body, availability: body.stockMode === 'availabilityOnly' ? 'unavailable' : null, availabilityChangedAt: null }), status: 'active', revision: 1, createdAt: now(), retiredAt: null }; rows.unshift(value); return save(value, true);
    }
    const conversion = resource.match(/^catalog\/units\/([^/]+)\/conversions\/([^/]+)$/);
    if (conversion) {
        if (request.method === 'GET') return finish(conversions.find(x => x.sourceUnitId === conversion[1] && x.targetUnitId === conversion[2] && x.revision === Number(uri.searchParams.get('revision'))) ?? {}, 200);
        const value = { sourceUnitId: conversion[1], targetUnitId: conversion[2], revision: body.expectedRevision + 1, numerator: body.numerator, denominator: body.denominator, publishedAt: now() }; conversions.push(value); return save(value);
    }
    const catalog = resource.match(/^catalog\/(units|items)\/([^/]+)(?:\/(display|availability|retire))?$/);
    if (catalog) {
        const rows = catalog[1] === 'units' ? units : items, field = catalog[1] === 'units' ? 'unitId' : 'itemId';
        const value = rows.find(x => x[field] === catalog[2]); if (!value) return finish({}, 404);
        if (request.method === 'GET') return finish(value);
        if (body.expectedRevision !== value.revision) return finish({}, 409);
        value.revision++;
        if (catalog[3] === 'display') { value.name = body.name; if ('description' in body) value.description = body.description; }
        if (catalog[3] === 'availability') { value.availability = body.availability; value.availabilityChangedAt = now(); }
        if (catalog[3] === 'retire') { value.status = 'retired'; value.retiredAt = now(); }
        return save({ id: value[field], revision: value.revision, status: catalog[3] === 'availability' ? value.availability : value.status, changedAt: catalog[3] === 'availability' ? value.availabilityChangedAt : value.retiredAt });
    }
    function recordOrder(value, change) {
        const history = orderHistory.get(value.orderId) ?? [];
        history.unshift({ change, changedByAccountId: accountId, recordedAt: now(), order: structuredClone(value) }); orderHistory.set(value.orderId, history);
    }
    const invoiceRoute = resource.match(/^(orders|invoices)\/([^/]+)(?:\/(invoice))?$/);
    if (invoiceRoute && (invoiceRoute[1] === 'invoices' || invoiceRoute[3])) {
        if (request.method === 'GET') return finish(issuedInvoices.find(x => x[invoiceRoute[1] === 'orders' ? 'sourceOrderId' : 'invoiceId'] === invoiceRoute[2]) ?? {}, 200);
        const order = orders.find(x => x.orderId === invoiceRoute[2]);
        assert.equal(order.state, 'committed'); assert.equal(body.expectedOrderRevision, order.revision);
        assert.equal(body.debtor.kind, 'Organization');
        const value = { invoiceId: crypto.randomUUID(), sourceOrderId: order.orderId, sourceOrderRevision: order.revision,
            organizationId: order.customerContext.organizationId, programId: null, organizationOrdinal: issuedInvoices.length + 1,
            currencyCode: 'NPR', lines: order.lines, total: order.total, debtor: { kind: 'Organization', organizationId: order.customerContext.organizationId, organizationDisplayName: organizations[0].displayName },
            issuedByAccountId: accountId, issuedAt: now(), businessDate: now().slice(0, 10) };
        issuedInvoices.push(value);
        receivableRows.push({ invoiceId: value.invoiceId, debtorKind: 'Organization', debtorId: value.organizationId, currencyCode: 'NPR', originalAmount: value.total, invoiceDate: value.businessDate, recognizedAt: now(), allocatedAmount: 0, outstandingAmount: value.total, isSettled: false });
        return save({ invoiceId: value.invoiceId }, true);
    }
    if (resource.startsWith('receivables/')) return finish(receivableRows.find(x => x.invoiceId === resource.split('/')[1]) ?? {}, 200);
    const filtered = rows => rows.filter(x => !uri.searchParams.has('debtorId') || x.debtorKind === uri.searchParams.get('debtorKind') && x.debtorId === uri.searchParams.get('debtorId'));
    if (resource === 'receivables') return finish({ items: filtered(receivableRows), nextCursor: null });
    if (resource === 'customer-payments') return finish({ items: filtered(customerPayments), nextCursor: null });
    if (resource === 'orders/price-preview') {
        assert.equal(body.currencyCode, 'NPR'); assert.equal(body.lines.length, 1);
        assert.equal(body.lines[0].quantity, 2); assert.ok([12.5, 15].includes(body.lines[0].unitPrice));
        return finish({ currencyCode: 'NPR', lines: [{ position: 1, ...body.lines[0], lineTotal: body.lines[0].unitPrice === 15 ? 30 : 25 }], total: body.lines[0].unitPrice === 15 ? 30 : 25 });
    }
    if (resource.startsWith('debtors/')) return finish({ debtorKind: 'Organization', debtorId: organizations[0].organizationId, currencyCode: 'NPR', recognizedAmount: 30, outstandingAmount: 30, confirmedUnappliedAmount: 0 });
    const paymentRoute = resource.match(/^customer-payments\/([^/]+)(?:\/(history|confirm|reject|dispute|reverse|allocations)(?:\/([^/]+)\/(release))?)?$/);
    if (paymentRoute) {
        let value = customerPayments.find(x => x.paymentId === paymentRoute[1]);
        if (request.method === 'GET') return finish(paymentRoute[2] === 'history' ? { items: paymentEvents.filter(x => x.paymentId === value.paymentId), nextCursor: null } : value);
        let operation = 'Record', amount = null, receivableId = null, allocationId = null;
        if (request.method === 'PUT') {
            assert.equal(value, undefined);
            value = { paymentId: paymentRoute[1], ...body, externalReference: body.externalReference ?? null, recordedByAccountId: accountId, recordedAt: now(), revision: 0, state: 'Pending', allocatedAmount: 0, availableAmount: 0 };
            customerPayments.push(value);
        } else {
            assert.equal(body.expectedRevision, value.revision);
            operation = paymentRoute[4] === 'release' ? 'Release' : { confirm: 'Confirm', reject: 'Reject', dispute: 'Dispute', reverse: 'Reverse', allocations: 'Allocate' }[paymentRoute[2]];
            if (operation === 'Confirm') { assert.notEqual(value.recordedByAccountId, accountId); value.state = 'Confirmed'; value.availableAmount = value.amount - value.allocatedAmount; }
            if (operation === 'Reject') value.state = 'Rejected';
            if (operation === 'Dispute') { value.state = 'Disputed'; value.availableAmount = 0; }
            if (operation === 'Reverse') { value.state = 'Reversed'; value.availableAmount = 0; }
            if (operation === 'Allocate') { amount = body.amount; receivableId = body.receivableId; value.allocatedAmount += amount; value.availableAmount -= amount; }
            if (operation === 'Release') { allocationId = paymentRoute[3]; const original = paymentEvents.find(x => x.eventId === allocationId); amount = original.amount; receivableId = original.receivableId; value.allocatedAmount -= amount; value.availableAmount = value.state === 'Confirmed' ? value.amount - value.allocatedAmount : 0; }
        }
        value.revision++;
        const event = { eventId: crypto.randomUUID(), paymentId: value.paymentId, revision: value.revision, operation, state: value.state,
            actorAccountId: accountId, recordedAt: now(), evidence: body.evidence ?? body.reason ?? null, receivableId, amount, allocationId };
        paymentEvents.push(event); return save({ paymentId: value.paymentId, eventId: event.eventId, revision: value.revision }, request.method === 'PUT');
    }
    if (resource === 'orders' && request.method === 'GET') return finish(page(orders));
    const orderMatch = resource.match(/^orders(?:\/([^/]+)(?:\/(draft|catalog-priced-draft|commit|abandon|actions|history))?)?$/);
    if (resource === 'orders' || resource === 'orders/catalog-priced' || orderMatch) {
        let value = orderMatch?.[1] && orders.find(x => x.orderId === orderMatch[1]);
        const action = orderMatch?.[2];
        if (request.method === 'GET') {
            if (!value) return finish({}, 404);
            if (action === 'actions') return finish({ orderId: value.orderId, observedRevision: value.revision, actions: ['revise', 'abandon', 'commit'].map(action => ({ action, available: value.state === 'draft', unavailabilityCode: value.state === 'draft' ? null : 'order_already_' + value.state })) });
            if (action === 'history') return finish({ currentRevision: value.revision, items: (orderHistory.get(value.orderId) ?? []).filter(x => !uri.searchParams.has('beforeRevision') || x.order.revision < Number(uri.searchParams.get('beforeRevision'))), nextBeforeRevision: null });
            return finish(value);
        }
        if (action === 'commit' || action === 'abandon') {
            if (!value || value.revision !== body.expectedRevision || value.state !== 'draft') return finish({}, 409);
            value.state = action === 'commit' ? 'committed' : 'abandoned'; value.revision++;
            value[action === 'commit' ? 'committedAt' : 'abandonedAt'] = now(); recordOrder(value, value.state);
            return save({ orderId: value.orderId, revision: value.revision, state: value.state, abandonedAt: value.abandonedAt, committedAt: value.committedAt });
        }
        if (value && (request.method !== 'PUT' || body.expectedRevision !== value.revision)) return finish({}, 409);
        if (!value) { assert.equal(request.method, 'POST'); value = { orderId: crypto.randomUUID(), revision: 0, createdAt: now(), state: 'draft', abandonedAt: null, committedAt: null, quotationOrigin: null }; orders.unshift(value); }
        const catalog = resource.endsWith('catalog-priced') || action === 'catalog-priced-draft';
        Object.assign(value, { summary: body.summary, currencyCode: body.currencyCode, customerContext: body.customerContext, revision: value.revision + 1 });
        value.lines = body.lines.map((line, index) => {
            const item = catalog && items.find(x => x.itemId === line.itemId);
            const price = catalog ? prices.find(x => x.itemId === line.itemId && x.state === 'Published') : undefined;
            const result = { position: index + 1, description: catalog ? item.name : line.description, quantity: line.quantity, unitCode: catalog ? units.find(x => x.unitId === line.unitId).code : line.unitCode, unitPrice: catalog ? price.baseUnitPrice : line.unitPrice };
            result.lineTotal = result.quantity * result.unitPrice;
            if (catalog) result.commercialFacts = { catalog: { itemId: line.itemId, unitId: line.unitId, conversion: { sourceUnitId: line.unitId, targetUnitId: line.unitId, revision: 1 } }, priceSelection: { explanation: { context: { customerId: body.customerId, wholesaleApplicable: body.wholesaleApplicable } } } };
            return result;
        });
        value.total = value.lines.reduce((sum, line) => sum + line.lineTotal, 0); recordOrder(value, value.revision === 1 ? 'created' : 'revised'); return save(value, value.revision === 1);
    }
    if (resource === 'pricing/policy') {
        if (request.method === 'GET') return finish(pricingPolicy ?? {}, pricingPolicy ? 200 : 404);
        if (body.expectedRevision !== (pricingPolicy?.policy.policyRevision ?? 0)) return finish({}, 409);
        pricingPolicy = { tenantId, policy: { policyRevision: body.expectedRevision + 1, minimumUnitPrice: body.minimumUnitPrice, maximumUnitPrice: body.maximumUnitPrice, maximumDecreasePercent: body.maximumDecreasePercent, maximumIncreasePercent: body.maximumIncreasePercent, envelopeSemanticsVersion: 2 }, createdByAccountId: accountId, createdAt: now() }; return save(pricingPolicy);
    }
    if (resource === 'pricing/drafts') {
        assert.ok(['default', 'wholesale', 'customer', 'program', 'organization'].includes(body.scope));
        const value = { ...body, priceId: body.priceId ?? crypto.randomUUID(), revisionId: crypto.randomUUID(), revision: prices.length + 1, unitCode: units.find(x => x.unitId === body.unitId)?.code ?? 'EA', scope: body.scope[0].toUpperCase() + body.scope.slice(1), state: 'Draft', createdAt: now(), publishedAt: null, retiredAt: null }; prices.push(value); return save(value, true);
    }
    const price = resource.match(/^pricing\/revisions\/([^/]+)(?:\/(publish|retire))?$/);
    if (price) {
        const value = prices.find(x => x.revisionId === price[1]); if (!value) return finish({}, 404);
        if (request.method === 'GET') return finish(value);
        value.state = price[2] === 'publish' ? 'Published' : 'Retired'; value[price[2] === 'publish' ? 'publishedAt' : 'retiredAt'] = now(); return save(value);
    }
    if (resource === 'pricing/history') return finish({ items: prices.filter(x => x.itemId === body.itemId && x.unitId === body.unitId && (!body.afterRevision || x.revision > body.afterRevision)), afterRevision: null });
    if (resource === 'pricing/resolve') {
        const value = prices.find(x => x.state === 'Published');
        return finish({ status: pricingStatus, unitPrice: pricingStatus === 'PriceResolved' ? value?.baseUnitPrice ?? 12.5 : null,
            priceId: value?.priceId ?? null, revisionId: value?.revisionId ?? null, revision: value?.revision ?? null, source: value?.scope ?? null,
            policyRevision: pricingPolicy?.policy.policyRevision ?? 1, evaluatedAt: now(), why: 'Current published default price.', candidates: [] });
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
const log = fs.openSync(path.join(output, 'business-web-host.log'), 'w');
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
        const businessMatch = uri.pathname.match(/^\/api\/v1\/tenants\/([^/]+)\/(customers\/.*|catalog\/.*|pricing\/.*|orders(?:\/.*)?|invoices\/.*|receivables(?:\/.*)?|customer-payments(?:\/.*)?|debtors\/.*)$/);
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
        const businessPage = !['Sign out', 'Add line'].includes(name) && await page.locator('.business-status').count();
        console.log('Control:', name);
        if (businessPage) await page.locator('.business-status[aria-busy=false]').waitFor();
        if (businessPage) await page.evaluate(() => {
            window.businessOperation = new Promise((resolve, reject) => {
                const deadline = setTimeout(() => { observer.disconnect(); reject(new Error("Business control did not finish rendering.")); }, 10000);
                let busy = false;
                const observer = new MutationObserver(mutations => {
                    const status = document.querySelector('.business-status');
                    if (status?.getAttribute('aria-busy') === 'true' || mutations.some(mutation => mutation.target === status && mutation.attributeName === 'aria-busy' && mutation.oldValue === 'true')) busy = true;
                    if (busy && status?.getAttribute('aria-busy') === 'false') { observer.disconnect(); clearTimeout(deadline); resolve(); }
                });
                observer.observe(document.body, { subtree: true, attributes: true, attributeOldValue: true, childList: true });
            });
        });
        await page.getByRole('button', { name, exact: true }).click();
        if (businessPage && !['Sign out', 'Refresh list'].includes(name)) await page.evaluate(() => window.businessOperation);
    }
    async function fill(label, value) { await page.getByLabel(label, { exact: true }).fill(String(value)); }
    async function message(text) { await page.locator('.business-status').getByText(text, { exact: false }).waitFor(); }
    async function go(resource) { await page.goto(`${webOrigin}/workspace/${first.tenantId}/${resource}`); await page.locator('.business-status[aria-busy=false]').waitFor(); }
    async function saved() { await page.evaluate(() => window.businessOperation); await message('Request completed.'); }
    async function capture(name) { await audit(page, name, output); }
    await page.goto(webOrigin);
    await page.getByRole('button', { name: 'Sign in', exact: true }).waitFor();
    assert.equal((await context.request.post(webOrigin + '/auth/login', { form: {} })).status(), 400);
    await click('Sign in'); await page.waitForURL(webOrigin + '/workspace');
    await page.getByLabel('North workspace', { exact: true }).check(); await click('Enter workspace');
    await page.getByRole('heading', { name: 'North workspace', exact: true }).waitFor();
    await page.getByRole('link', { name: 'Organizations and programs', exact: true }).click();
    await page.getByRole('heading', { name: 'Organizations', exact: true }).waitFor();
    await page.locator('.business-status[aria-busy=false]').waitFor();
    await capture('customers-empty-desktop');
    const customersUrl = page.url();
    await page.goto(customersUrl + '?skipcheck=1#previous');
    await page.locator('.business-status[aria-busy="false"]').waitFor();
    await page.locator('.skip-link').focus(); await page.keyboard.press('Enter');
    assert.equal(new URL(page.url()).pathname, new URL(customersUrl).pathname);
    assert.equal(new URL(page.url()).search, '?skipcheck=1');
    assert.equal(new URL(page.url()).hash, '#main');
    assert.equal(await page.evaluate(() => document.activeElement.id), 'main');
    await page.goto(customersUrl);
    await page.locator('.business-status[aria-busy="false"]').waitFor();
    await fill('Display name', '<script>North client</script>');
    lostSave = true; await click('Create organization');
    await message('save outcome is unknown');
    assert.equal(organizations.length, 1);
    assert.ok(await page.getByLabel('Display name', { exact: true }).isDisabled());
    await capture('customers-unknown');
    const blockedNavigation = page.waitForEvent('dialog');
    await page.getByRole('link', { name: 'Catalog', exact: true }).click({ noWaitAfter: true });
    await blockedNavigation;
    await page.locator('.business-status').getByText('save outcome is unknown', { exact: false }).waitFor();
    assert.ok(page.url().endsWith('/customers'));
    failure = 403; await click('Retry saved request'); await message('earlier save outcome is still unknown');
    assert.ok(await page.getByLabel('Display name', { exact: true }).isDisabled());
    assert.equal(await page.getByRole('complementary', { name: 'Saved request recovery', exact: true }).count(), 1);
    failure = undefined;
    await click('Retry saved request'); await message('earlier request was already saved');
    assert.equal(organizations.length, 1);
    assert.equal(await page.locator('script').filter({ hasText: 'North client' }).count(), 0);
    await page.getByRole('heading', { name: '<script>North client</script>', exact: true }).waitFor();
    await page.getByRole('link', { name: 'Open organization and programs', exact: true }).click();
    await page.waitForURL(`${webOrigin}/workspace/${first.tenantId}/customers/organizations/${organizations[0].organizationId}/programs`);
    await page.getByRole('heading', { name: 'Organization and programs', exact: true }).waitFor();
    await page.locator('.business-status[aria-busy=false]').waitFor();
    await fill('Display name', 'School program'); await click('Create program'); await saved();
    assert.equal(programs[0].organizationId, organizations[0].organizationId);
    await page.getByRole('link', { name: 'Individuals', exact: true }).click();
    await fill('Display name', 'Jordan North'); await fill('Email (optional)', 'jordan@example.test'); await fill('Phone (optional)', '+977 123456');
    await click('Create individual'); await saved();
    await page.getByText('Edit contact', { exact: true }).click();
    await fill('Contact display name', 'Jordan Updated'); await fill('Contact email (optional)', 'updated@example.test');
    await click('Save contact'); await saved(); await page.getByRole('heading', { name: 'Jordan Updated', exact: true }).waitFor();
    await click('Mark individual inactive'); await saved();
    assert.equal(individuals[0].availability, 'inactive'); assert.equal(individuals[0].revision, 3);
    await capture('individual-contact-desktop');
    await page.reload(); await fill('Individual reference', individuals[0].individualId); await click('Find individual');
    await page.getByRole('heading', { name: 'Jordan Updated', exact: true }).waitFor();
    await click('Mark individual active'); await saved();
    await page.goto(`${webOrigin}/workspace/${first.tenantId}/customers/organizations/${organizations[0].organizationId}/representatives`);
    await page.locator('.business-status[aria-busy=false]').waitFor();
    await fill('Individual reference', individuals[0].individualId); await fill('Program reference (optional)', programs[0].programId); await click('Link representative'); await saved();
    await page.getByLabel('Deactivate this exact relationship, preserving its history.', { exact: true }).check(); await click('Unlink representative'); await saved();
    await click('Find relationship'); await saved(); assert.equal(representatives[0].availability, 'inactive');
    await capture('representatives-desktop');
    await page.getByRole('link', { name: 'Catalog', exact: true }).click();
    await fill('Unit name', 'Each'); await fill('Unit code', 'EA'); await fill('Decimal places', 0); await click('Create unit'); await saved();
    await fill('Item name', 'Printed service'); await fill('Item code (optional)', 'PRINT');
    await page.getByLabel('Stock mode', { exact: true }).selectOption('availabilityOnly');
    await click('Create item'); await saved();
    assert.equal(items[0].baseUnitId, units[0].unitId);
    await click('Mark available'); await saved(); assert.equal(items[0].availability, 'available');
    await page.getByText('Edit item display', { exact: true }).click();
    await fill('New item name', 'Printed service revised'); await fill('New item description (optional)', 'A safe <script>description</script>'); await click('Save item display'); await saved();
    await capture('catalog-desktop');
    await page.setViewportSize({ width: 320, height: 844 }); await capture('catalog-mobile-320');
    await page.setViewportSize({ width: 1440, height: 1000 });
    await fill('Unit name', 'Pack'); await fill('Unit code', 'PACK'); await click('Create unit'); await saved();
    await fill('Source unit reference', units[0].unitId); await fill('Target unit reference', units[1].unitId);
    await fill('Numerator', 10); await fill('Denominator', 1);
    await page.getByLabel('Publish this exact conversion as a new revision.', { exact: true }).check();
    await click('Publish conversion'); await saved(); await click('Read conversion revision'); await saved(); assert.equal(conversions.length, 1);
    await page.getByRole('link', { name: 'Work with prices', exact: false }).click();
    await page.getByRole('heading', { name: 'Pricing', exact: true }).waitFor();
    await page.locator('.business-status[aria-busy=false]').waitFor();
    assert.equal(await page.getByLabel('Item reference', { exact: true }).inputValue(), items[0].itemId);
    await page.getByText('Publish a pricing policy', { exact: true }).click();
    await fill('Minimum unit price', 0); await fill('Maximum unit price', 1000);
    await page.getByLabel('Publish these limits as the next policy revision.', { exact: true }).check();
    await click('Publish pricing policy'); await saved();
    await fill('Base unit price', '12.5'); await click('Save price draft'); await saved();
    await page.getByRole('heading', { name: 'Price revision 1', exact: true }).waitFor();
    await page.getByLabel('Publish this exact revision and its validity period.', { exact: true }).check();
    await click('Publish price revision'); await saved(); assert.equal(prices[0].state, 'Published');
    await click('Resolve current price'); await saved(); await page.getByText('Current price resolved', { exact: true }).waitFor();
    await click('Load price history'); await saved(); assert.equal(await page.getByRole('button', { name: 'Revision 1 · Default · Published', exact: true }).count(), 1);
    await capture('pricing-desktop');
    await page.setViewportSize({ width: 320, height: 844 }); await capture('pricing-mobile-320');
    await page.emulateMedia({ forcedColors: 'active', reducedMotion: 'reduce' }); await capture('pricing-forced-colors');
    await page.emulateMedia({ forcedColors: 'none' }); await page.setViewportSize({ width: 1440, height: 1000 });
    for (const [status, title] of [['PriceMissing', 'No matching published price'], ['PriceExpired', 'Matching price has expired'], ['PriceConflict', 'Conflicting published prices'], ['PriceOverrideRequired', 'A separately authorized price override is required']]) {
        pricingStatus = status; await click('Resolve current price'); await page.getByText(title, { exact: true }).waitFor();
    }
    await page.getByRole('link', { name: 'Orders', exact: true }).click();
    await page.getByText('Choose a saved organization and program', { exact: true }).click();
    await page.getByRole('button', { name: 'Browse saved organizations', exact: true }).click();
    await page.getByLabel('Saved organization', { exact: true }).selectOption(organizations[0].organizationId);
    await page.getByRole('button', { name: 'Browse programs for selected organization', exact: true }).click();
    await page.getByLabel('Saved program (optional)', { exact: true }).selectOption(programs[0].programId);
    await page.getByRole('button', { name: 'Use selected customer context', exact: true }).click();
    await page.getByText('Selected customer references copied into this draft. Review them before saving.', { exact: true }).waitFor();
    assert.equal(await page.getByLabel('Customer organization reference (optional)', { exact: true }).inputValue(), organizations[0].organizationId);
    assert.equal(await page.getByLabel('Customer program reference (optional, belongs to organization)', { exact: true }).inputValue(), programs[0].programId);
    await fill('Order summary', 'A durable direct order'); await fill('Description 1', 'Printing'); await fill('Unit price 1', 12.5); await fill('Quantity 1', 2);
    const beforePreview = orders.length;
    await click('Preview supplied prices'); await saved();
    await page.getByRole('heading', { name: 'Server price preview', exact: true }).waitFor();
    assert.equal(orders.length, beforePreview); assert.equal(await page.getByRole('complementary', { name: 'Saved request recovery', exact: true }).count(), 0);
    await capture('order-price-preview-desktop');
    await fill('Unit price 1', 15); await page.getByLabel('Order summary', { exact: true }).focus();
    await page.getByText('The entered prices changed. Preview again to see the current calculation.', { exact: true }).waitFor();
    assert.equal(await page.getByRole('heading', { name: 'Server price preview', exact: true }).count(), 0);
    await click('Preview supplied prices'); await saved(); await page.locator('#order-preview').waitFor();
    await page.setViewportSize({ width: 320, height: 844 }); await capture('order-price-preview-mobile-320'); await page.setViewportSize({ width: 1440, height: 1000 });
    failure = 403; await click('Preview supplied prices'); await message('do not have access'); failure = undefined;
    assert.equal(await page.getByRole('button', { name: 'Retry saved request', exact: true }).count(), 0);
    await fill('Unit price 1', 12.5);
    await click('Add line'); await fill('Description 2', 'Setup'); await fill('Unit price 2', 5);
    await click('Save order draft'); await saved(); assert.equal(orders[0].total, 30);
    await capture('orders-desktop');
    await fill('Order summary', 'A revised direct order'); await page.getByLabel('Replace all content of this saved draft with the entered values.', { exact: true }).check();
    await click('Replace order draft'); await saved(); assert.equal(orders[0].revision, 2);
    await click('Check available actions'); await saved(); await click('Load order history'); await saved();
    assert.equal(await page.getByText('Revision 2 · revised', { exact: true }).count(), 1);
    await page.getByText('Commit or abandon this draft', { exact: true }).click();
    await page.getByLabel('Commit this exact saved revision.', { exact: true }).check(); await click('Commit order'); await saved(); assert.equal(orders[0].state, 'committed');
    await page.getByRole('link', { name: 'Reload saved order', exact: true }).click(); await page.getByText('committed', { exact: true }).first().waitFor(); assert.equal(await page.getByRole('button', { name: 'Replace order draft', exact: true }).count(), 0);
    await go('orders'); await fill('Order summary', 'Catalog order'); await page.getByLabel('Pricing mode', { exact: true }).selectOption('catalog');
    await page.getByText('Choose a saved catalog item for order line 1', { exact: true }).click();
    await page.getByRole('button', { name: 'Browse saved items for order line 1', exact: true }).click();
    await page.getByLabel('Saved item for order line 1', { exact: true }).selectOption(items[0].itemId);
    await page.getByRole('button', { name: 'Use saved item for order line 1', exact: true }).click();
    await page.getByText('Item and base-unit references copied into this line. Review before saving.', { exact: true }).waitFor();
    assert.equal(await page.getByLabel('Catalog item reference 1', { exact: true }).inputValue(), items[0].itemId);
    assert.equal(await page.getByLabel('Catalog unit reference 1', { exact: true }).inputValue(), items[0].baseUnitId);
    await capture('order-catalog-picker-desktop');
    await click('Save order draft'); await saved(); assert.equal(orders[0].total, 12.5);
    await page.getByRole('link', { name: 'Reload saved order', exact: true }).click(); await page.locator('.business-status[aria-busy=false]').waitFor();
    assert.equal(await page.getByLabel('Pricing mode', { exact: true }).inputValue(), 'catalog');
    assert.equal(await page.getByLabel('Conversion revision 1 (optional)', { exact: true }).inputValue(), '');
    await page.getByText('Commit or abandon this draft', { exact: true }).click(); await page.getByLabel('Permanently abandon this exact saved draft.', { exact: true }).check();
    await click('Abandon draft'); await saved(); assert.equal(orders[0].state, 'abandoned');
    await page.setViewportSize({ width: 320, height: 844 }); await capture('orders-mobile-320'); await page.setViewportSize({ width: 1440, height: 1000 });
    await page.getByRole('link', { name: 'Pricing', exact: true }).click();
    await fill('Price revision reference', prices[0].revisionId);
    failure = 409; await page.getByLabel('Publish this exact revision and its validity period.', { exact: true }).check(); await click('Publish price revision'); await message('conflicts with current records'); await capture('pricing-conflict'); failure = undefined;
    await page.getByText('Retire a revision', { exact: true }).click(); await page.getByLabel('Retire the referenced price revision.', { exact: true }).check(); await click('Retire price revision'); await saved(); assert.equal(prices[0].state, 'Retired');
    await go(`catalog/items/${items[0].itemId}`);
    await page.locator('summary').filter({ hasText: /^Retire item$/ }).click(); await page.getByLabel('Permanently retire this item.', { exact: true }).check();
    await click('Retire item'); await saved(); assert.equal(items[0].status, 'retired');
    await go(`catalog/units/${units[0].unitId}`);
    await page.getByText('Edit unit name', { exact: true }).click(); await fill('New unit name', 'Pack renamed'); await click('Save unit name'); await saved();
    await page.locator('summary').filter({ hasText: /^Retire unit$/ }).click(); await page.getByLabel('Permanently retire this unit.', { exact: true }).check();
    await click('Retire unit'); await saved(); assert.equal(units[0].status, 'retired');
    await page.getByRole('link', { name: 'Billing', exact: true }).click();
    await page.getByRole('heading', { name: 'Billing', exact: true }).waitFor();
    await page.locator('.business-status[aria-busy=false]').waitFor();
    const committedForInvoice = orders.find(x => x.state === 'committed');
    await go(`orders/${committedForInvoice.orderId}`);
    await page.getByRole('link', { name: 'Invoice and settlement', exact: true }).click();
    await page.waitForURL(`${webOrigin}/workspace/${first.tenantId}/billing/orders/${committedForInvoice.orderId}`);
    await page.getByRole('region', { name: 'Committed Order for billing', exact: true }).waitFor();
    await page.locator('.business-status[aria-busy=false]').waitFor();
    assert.equal(await page.getByLabel('Committed Order reference', { exact: true }).inputValue(), committedForInvoice.orderId);
    assert.equal(await page.getByLabel('Expected committed Order revision', { exact: true }).inputValue(), String(committedForInvoice.revision));
    await page.getByLabel('Issue the full invoice for this exact committed revision and debtor.', { exact: true }).check();
    await click('Issue full invoice'); await saved(); assert.equal(issuedInvoices.length, 1);
    await click('Find invoice for Order'); await saved(); await page.getByRole('heading', { name: 'Issued invoice 1', exact: true }).waitFor();
    await click('Load current invoice balance'); await saved();
    await page.getByRole('heading', { name: 'Current invoice balance', exact: true }).waitFor();
    await page.getByRole('button', { name: 'Use this debtor and invoice for payment entry', exact: true }).click();
    await message('Debtor and invoice references copied.');
    assert.equal(await page.getByLabel('Payment debtor reference', { exact: true }).inputValue(), organizations[0].organizationId);
    assert.equal(await page.getByLabel('Receivable invoice reference', { exact: true }).inputValue(), issuedInvoices[0].invoiceId);
    await page.getByRole('button', { name: 'Apply debtor filter', exact: true }).click();
    await message('Directory filter applied.');
    await click('Refresh receivables'); await saved();
    await page.evaluate(() => { window.printCalls = 0; window.print = () => window.printCalls++; });
    await page.getByRole('button', { name: 'Print invoice record', exact: true }).click();
    await until(() => page.evaluate(() => window.printCalls === 1));
    await page.emulateMedia({ media: 'print' });
    assert.equal(await page.getByRole('navigation', { name: 'Workspace business', exact: true }).isVisible(), false);
    assert.equal(await page.getByRole('heading', { name: 'Billing', exact: true }).isVisible(), false);
    assert.equal(await page.locator('.invoice-document').isVisible(), true);
    assert.equal(await page.getByRole('heading', { name: 'Issued invoice 1', exact: true }).isVisible(), true);
    assert.equal(await page.getByRole('button', { name: 'Print invoice record', exact: true }).isVisible(), false);
    await page.screenshot({ path: path.join(output, 'invoice-print-record.png'), fullPage: true });
    await page.emulateMedia({ media: 'screen' });
    await capture('invoice-continuation-desktop');
    await page.setViewportSize({ width: 320, height: 844 }); await capture('invoice-continuation-mobile-320'); await page.setViewportSize({ width: 1440, height: 1000 });

    assert.equal(await page.locator('script').filter({ hasText: 'North client' }).count(), 0);
    await fill('Payment debtor reference', organizations[0].organizationId); await fill('Received amount (NPR)', 50);
    await fill('Receipt evidence', '<script>Receipt evidence</script>');
    await page.getByLabel('I reviewed the exact debtor, amount, channel and receipt evidence.', { exact: true }).check();
    lostSave = true; await click('Record pending payment'); await message('save outcome is unknown');
    assert.equal(customerPayments.length, 1); await capture('billing-unknown-payment');
    await click('Retry saved request'); await message('earlier request was already saved'); assert.equal(customerPayments.length, 1);
    await click('Load payment by reference'); await saved(); assert.equal(customerPayments[0].state, 'Pending');
    await capture('billing-pending-payment');
    // Seed another operator's receipt to exercise this actor's reconciliation UI; native tests prove actual independent authority.
    const independentPayment = { ...customerPayments[0], paymentId: crypto.randomUUID(), recordedByAccountId: crypto.randomUUID(), evidence: 'Other operator receipt' };
    customerPayments.push(independentPayment);
    paymentEvents.push({ eventId: crypto.randomUUID(), paymentId: independentPayment.paymentId, revision: 1, operation: 'Record', state: 'Pending', actorAccountId: independentPayment.recordedByAccountId,
        recordedAt: now(), evidence: independentPayment.evidence, receivableId: null, amount: null, allocationId: null });
    await go(`billing/payments/${independentPayment.paymentId}`);
    await page.getByText('Confirm received funds', { exact: true }).click();
    await fill('Received-funds confirmation evidence', 'Independently counted funds');
    await page.getByLabel('I independently verified that the funds were actually received.', { exact: true }).check();
    await click('Confirm payment'); await saved(); assert.equal(independentPayment.state, 'Confirmed');
    await page.getByText('Allocate confirmed funds', { exact: true }).click();
    await fill('Receivable invoice reference', issuedInvoices[0].invoiceId); await fill('Allocation amount (NPR)', 20);
    await page.getByLabel('I reviewed this exact invoice and allocation amount.', { exact: true }).check();
    await click('Allocate payment funds'); await saved(); const allocatedEvent = paymentEvents.at(-1); assert.equal(allocatedEvent.operation, 'Allocate');
    await page.getByText('Reject or dispute evidence', { exact: true }).click(); await fill('Rejection or dispute evidence', 'Receipt evidence disputed');
    await page.getByLabel('I reviewed the current payment and this evidence decision.', { exact: true }).check();
    await click('Dispute payment'); await saved(); assert.equal(independentPayment.state, 'Disputed');
    await page.getByText('Correct a receipt or allocation', { exact: true }).click();
    await fill('Correction reason', 'Wrong invoice allocation'); await fill('Allocation event reference to release', allocatedEvent.eventId);
    assert.equal(await page.getByRole('button', { name: 'Release entire allocation', exact: true }).isDisabled(), true);
    await fill('Type “Release allocation” or “Reverse payment” to confirm the chosen correction', 'Release allocation');
    await click('Release entire allocation'); await saved(); assert.equal(independentPayment.allocatedAmount, 0);
    await fill('Received-funds confirmation evidence', 'Fresh independent receipt confirmation');
    await page.getByLabel('I independently verified that the funds were actually received.', { exact: true }).check();
    await click('Confirm payment'); await saved();
    await fill('Correction reason', 'Correct original receipt evidence');
    await fill('Type “Release allocation” or “Reverse payment” to confirm the chosen correction', 'Reverse payment');
    await click('Reverse entire payment'); await saved(); assert.equal(independentPayment.state, 'Reversed');
    await click('Load payment by reference'); await saved(); await click('Load payment history'); await saved();
    await page.getByText('Revision 7 · Reverse · Reversed', { exact: true }).waitFor();
    await click('Refresh payments'); await saved(); await click('Refresh receivables'); await saved();
    await fill('Balance debtor reference', organizations[0].organizationId); await click('Load debtor balance'); await saved();
    await capture('billing-desktop');
    await page.setViewportSize({ width: 320, height: 844 }); await capture('billing-mobile-320');
    await page.setViewportSize({ width: 1440, height: 1000 });
    await go(`billing/payments/${customerPayments[0].paymentId}`);
    await page.getByText('Reject or dispute evidence', { exact: true }).click(); await fill('Rejection or dispute evidence', 'Receipt was not received');
    await page.getByLabel('I reviewed the current payment and this evidence decision.', { exact: true }).check();
    await click('Reject payment'); await saved(); assert.equal(customerPayments[0].state, 'Rejected');
    failure = 403; await click('Refresh payments'); await message('Each operation needs its own permission');
    assert.equal(await page.getByRole('heading', { name: 'Record received-money evidence', exact: true }).count(), 1); failure = undefined;
    await page.getByText('Correct a receipt or allocation', { exact: true }).click();
    await page.getByRole('button', { name: 'Verify identity for corrections', exact: true }).click();
    await page.waitForURL(`${webOrigin}/workspace/${first.tenantId}/billing`);
    await page.getByRole('heading', { name: 'Billing', exact: true }).waitFor();
    assert.equal(verificationChecks, 1);
    await go('customers');
    failure = 403; await click('Refresh list'); await message('Each operation needs its own permission');
    assert.equal(await page.getByRole('heading', { name: 'Create an organization', exact: true }).count(), 1);
    await capture('customers-permission-denied');
    failure = undefined; malformed = true; await click('Refresh list'); await message('unsupported response'); assert.equal(await page.locator('.record-list li').count(), 0); malformed = false;
    failure = 503; await click('Refresh list'); await message('service is unavailable'); failure = undefined;
    holdBusiness = true; await click('Refresh list'); await until(() => Boolean(releaseBusiness));
    await page.locator('.business-status[aria-busy=true]').waitFor();
    assert.ok(await page.getByRole('button', { name: 'Create organization', exact: true }).isDisabled());
    await capture('customers-loading'); holdBusiness = false; releaseBusiness(); releaseBusiness = undefined; await saved();
    for (let i = organizations.length; i < 25; i++) organizations.push({ organizationId: crypto.randomUUID(), tenantId: first.tenantId, displayName: 'Customer ' + i, createdAt: now() });
    await click('Refresh list'); await saved(); await click('Next records'); await saved(); await page.getByText('No records on this page.', { exact: false }).waitFor(); await click('First records'); await saved();
    await page.setViewportSize({ width: 320, height: 844 }); await capture('customers-mobile-320');
    const beforeDenied = requests.length; denyWorkspace = true;
    await go('catalog'); await page.getByRole('heading', { name: 'Workspace unavailable', exact: true }).waitFor();
    assert.equal(requests.length, beforeDenied); assert.equal(await page.getByRole('heading', { name: 'Create a unit', exact: true }).count(), 0); denyWorkspace = false;
    await page.goto(`${webOrigin}/workspace/${second.tenantId}/customers`); await page.getByText('Each operation needs its own permission', { exact: false }).waitFor();
    assert.equal(await page.locator('.record-list li').count(), 0);
    await go('customers'); await message('Request completed.');
    assert.equal((await context.request.post(page.url(), { data: { displayName: 'Forged' } })).ok(), false);
    assert.equal((await context.request.post(webOrigin + '/auth/logout', { form: {} })).status(), 400);
    assert.equal((await context.request.post(webOrigin + '/_blazor/negotiate?negotiateVersion=1', { headers: { Origin: 'https://untrusted.example.test' } })).status(), 400);
    const html = await page.content(); assert.ok(!html.includes(accessToken)); assert.ok(!html.includes(clientSecret)); assert.ok(!html.includes('private SQL diagnostic'));
    assert.deepEqual(await page.evaluate(() => ({ local: Object.keys(localStorage), session: Object.keys(sessionStorage) })), { local: [], session: [] });
    holdBusiness = true; releaseBusiness = undefined; await click('Refresh list'); await until(() => Boolean(releaseBusiness));
    const signOutPage = await context.newPage(); await signOutPage.goto(webOrigin + '/workspace');
    await signOutPage.getByRole('button', { name: 'Sign out', exact: true }).click();
    await signOutPage.getByRole('heading', { name: 'You are signed out.', exact: true }).waitFor();
    holdBusiness = false; releaseBusiness(); releaseBusiness = undefined;
    await page.getByRole('heading', { name: 'Workspace unavailable', exact: true }).waitFor();
    assert.equal(await page.locator('.record-list li').count(), 0);
    await capture('signed-out-inflight-read'); await signOutPage.close();
    await page.goto(`${webOrigin}/workspace/${first.tenantId}/catalog`); assert.equal(new URL(page.url()).pathname, '/');
    assert.ok(pkceChecks >= 2); assert.deepEqual(pageErrors, []);
    events.push('Rendered Customers organization/program creation, exact lost-response replay, navigation guard, contact edits and availability passed.',
        'Rendered Catalog create/read/display/availability and immutable direct conversion passed.',
        'Rendered Representatives link/read/unlink and Orders manual/catalog create, PUT replacement, history, commit/abandon and reload passed.',
        'Rendered Pricing policy/draft/publication/selection/history/retirement and conflict/missing/expired/override states passed.',
        'Rendered Billing invoice issue/frozen lookup, payment unknown-result replay, reconciliation, allocation, dispute, typed corrections, history and debtor balances passed against the contract fixture.',
        'Controlled tenant denial, permission denial, malformed data, outage/loading, pagination, CSRF, logout, token custody and 320px axe/reflow checks passed.');
    fs.writeFileSync(path.join(output, 'business-browser-results.json'), JSON.stringify({ browser: await browser.version(), events, verificationChecks, requestCount: requests.length, liveZitadel: 'NOT_RUN_CONTROLLED_LOCAL_ISSUER', providerPersistence: 'NOT_RUN_CONTROLLED_CONTRACT_FIXTURE' }, null, 2));
    console.log(events.join('\n'));
}
run().catch(async error => { console.error(error); console.error(JSON.stringify({ url: activePage?.url(), browserResponses, requests: requests.slice(-12), fields: activePage ? await activePage.locator('input').evaluateAll(inputs => inputs.filter(i => i.type !== 'hidden').map(i => ({id:i.id,value:i.value,disabled:i.matches(':disabled')}))) : [], status: activePage ? await activePage.locator('.business-status').textContent() : '' })); if (activePage) await activePage.screenshot({ path: path.join(output, 'failure.png'), fullPage: true }).catch(() => {}); process.exitCode = 1; }).finally(async () => {
    holdBusiness = false; releaseBusiness?.();
    if (browser) await browser.close(); await stopWeb(); fixture.closeAllConnections(); await new Promise(resolve => fixture.close(resolve));
    fs.closeSync(log); fs.rmSync(temporary, { recursive: true, force: true });
});
