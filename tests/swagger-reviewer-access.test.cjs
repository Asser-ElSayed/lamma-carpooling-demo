const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function browser() {
  const ids = new Map();
  function element(tag = 'div') {
    const node = { tag, style: {}, children: [], listeners: {}, value: '', textContent: '',
      append(...nodes) { this.children.push(...nodes); }, prepend(node) { this.children.unshift(node); },
      add(option) { this.children.push(option); }, setAttribute() {},
      addEventListener(name, callback) { this.listeners[name] = callback; }, querySelector() { return {}; }
    };
    Object.defineProperty(node, 'id', { set(value) { this._id = value; ids.set(value, this); }, get() { return this._id; } });
    return node;
  }
  const root = element(); root.id = 'swagger-ui';
  let now = Date.now();
  let activeToken;
  let fail = false;
  const calls = [];
  class Clock extends Date { static now() { return now; } }
  const window = {
    location: { origin: 'https://demo.test', href: 'https://demo.test/swagger/index.html' },
    ui: { preauthorizeApiKey(scheme, token) { assert.equal(scheme, 'Bearer'); activeToken = token; },
      authActions: { logout() { activeToken = undefined; } } },
    setTimeout() { throw new Error('The test browser should be ready to mount.'); }
  };
  const context = vm.createContext({ window, Date: Clock, URL, Map, Error, JSON,
    Option: function(text, value) { return { text, value }; },
    document: { readyState: 'complete', getElementById: id => ids.get(id), createElement: element },
    fetch: async (url, options) => {
      const account = JSON.parse(options.body).account;
      calls.push({ url, account, options });
      return { ok: !fail, json: async () => fail ? { detail: 'Demo setup unavailable.' } :
        { account, accessToken: `${account}.token.${calls.length}`, expiresAtUtc: new Date(now + 3600000).toISOString() } };
    }
  });
  vm.runInContext(fs.readFileSync(path.join(__dirname, '../src/Lamma.Api/Swagger/reviewer-access.js'), 'utf8'), context);
  return {
    calls, ids, window,
    token: () => activeToken,
    advance: ms => { now += ms; },
    fail: () => { fail = true; },
    async select(account) { const select = ids.get('reviewer-account'); select.value = account; await select.listeners.change(); }
  };
}

test('choosing an account obtains and applies a token without copying credentials', async () => {
  const b = browser();
  await b.select('Host');
  assert.equal(b.token(), 'Host.token.1');
  assert.equal(b.calls[0].url, '/swagger/reviewer-token');
  assert.equal(b.calls[0].options.cache, 'no-store');
  assert.match(b.ids.get('reviewer-access-status').textContent, /Authorized as Host/);
});

test('an expired session renews automatically before the next business request', async () => {
  const b = browser();
  await b.select('Rider1');
  b.advance(2 * 3600000);
  const request = await b.window.lammaReviewerRequest({ url: '/bookings', headers: {} });
  assert.equal(b.calls.length, 2);
  assert.equal(request.headers.Authorization, 'Bearer Rider1.token.2');
  assert.equal(b.token(), 'Rider1.token.2');
});

test('switching accounts sends the selected Rider identity', async () => {
  const b = browser();
  await b.select('Host');
  await b.select('Rider2');
  const request = await b.window.lammaReviewerRequest({ url: '/rides/search', headers: {} });
  assert.equal(request.headers.Authorization, 'Bearer Rider2.token.2');
  assert.equal(b.calls.length, 2);
});

test('concurrent requests share one token renewal', async () => {
  const b = browser();
  await b.select('Rider3');
  b.advance(2 * 3600000);
  const requests = await Promise.all([1, 2, 3].map(() => b.window.lammaReviewerRequest({ url: '/rides/search', headers: {} })));
  assert.equal(b.calls.length, 2);
  assert.ok(requests.every(r => r.headers.Authorization === 'Bearer Rider3.token.2'));
});

test('credentials are stripped from requests targeting another origin', async () => {
  const b = browser();
  await b.select('Host');
  const request = await b.window.lammaReviewerRequest({ url: 'https://other.test/rides', headers: { Authorization: 'Bearer secret' } });
  assert.equal(request.headers.Authorization, undefined);
  assert.equal(b.calls.length, 1);
});

test('signing out clears the selected identity and does not fetch another token', async () => {
  const b = browser();
  await b.select('Rider4');
  await b.select('');
  assert.equal(b.token(), undefined);
  const request = await b.window.lammaReviewerRequest({ url: '/rides/search', headers: {} });
  assert.equal(request.headers.Authorization, undefined);
  assert.equal(b.calls.length, 1);
});

test('authorization failure clears the old credential and reports a useful message', async () => {
  const b = browser();
  await b.select('Host');
  b.fail();
  await b.select('Rider1');
  assert.equal(b.token(), undefined);
  assert.match(b.ids.get('reviewer-access-status').textContent, /Demo setup unavailable/);
});
