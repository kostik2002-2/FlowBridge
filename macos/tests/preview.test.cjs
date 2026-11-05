'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const crypto = require('node:crypto');
const cp = require('node:child_process');
const root = path.resolve(__dirname, '../..');
cp.execFileSync(process.execPath, ['macos/build-extension.cjs'], { cwd: root });
const read = (flavor, file) => fs.readFileSync(path.join(root, 'package/macos', flavor, file), 'utf8');

test('Mac previews retain Flow-only access, MAIN document_start and bundled offline resources', () => {
  for (const flavor of ['chromium', 'safari']) {
    const manifest = JSON.parse(read(flavor, 'manifest.json'));
    assert.equal(manifest.version, '0.2.0');
    assert.deepEqual(manifest.permissions, ['tabs', 'cookies']);
    assert.deepEqual(manifest.host_permissions, ['https://flow.google.com/*']);
    assert.equal(manifest.externally_connectable, undefined);
    const engine = manifest.content_scripts.find(entry => entry.world === 'MAIN');
    assert.equal(engine.run_at, 'document_start');
    assert.deepEqual(engine.matches, ['https://flow.google.com/*']);
    assert.equal(read(flavor, 'engine.js'), fs.readFileSync(path.join(root, 'package/extension/chromium/engine.js'), 'utf8'));
    for (const file of ['setup-background.js', 'complete.js', 'popup.js']) {
      assert.doesNotMatch(read(flavor, file), /\beval\s*\(|\bfetch\s*\(|XMLHttpRequest/);
    }
    assert.match(read(flavor, 'popup.js'), /getURL\('complete\.html'\)/);
  }
});

test('Firefox signature input is identical to the production signed XPI', () => {
  const signed = fs.readFileSync(path.join(root, 'package/macos/FlowBridge-0.1.1-signed.xpi'));
  assert.equal(crypto.createHash('sha256').update(signed).digest('hex'), '7da9235f189cf8b62d5dc82c5c3204e555112c91f3324a3cae05f8054c5a5fb1');
  assert.deepEqual(signed, fs.readFileSync(path.join(root, 'package/signing/FlowBridge-0.1.1-signed.xpi')));
});

function harness(flavor, { permission = true, request = true, oldFlow = true, createFailure = false, cookieFailure = false } = {}) {
  const operations = [];
  let click;
  const status = { textContent: '' };
  const button = { hidden: true, disabled: false, textContent: '', addEventListener: (_, callback) => { click = callback; } };
  const tabs = [
    { id: 1, windowId: 7, index: 0, url: 'extension://self/complete.html' },
    { id: 3, windowId: 7, url: 'https://example.com/work' },
    ...(oldFlow ? [{ id: 2, windowId: 7, url: 'https://flow.google.com/old' }] : [])
  ];
  const api = {
    permissions: { contains: async () => permission, request: async () => { operations.push('permission-request'); return request; } },
    cookies: {
      getAll: async details => {
        operations.push(['cookies-read', details]);
        if (cookieFailure) throw Error('cookie access denied');
        if (flavor === 'safari' && 'partitionKey' in details) throw Error('Unsupported partitionKey');
        return [{ name: 'flow', domain: 'flow.google.com', path: '/', storeId: '0', hostOnly: true, secure: true, sameSite: 'lax' },
          { name: 'auth', domain: '.google.com', path: '/' }];
      },
      set: async cookie => { operations.push(['expire', cookie]); }
    },
    tabs: {
      getCurrent: async () => tabs[0],
      query: async () => tabs,
      create: async details => { operations.push(['create', details]); if (createFailure) throw Error('tab creation failed'); return { id: 4 }; },
      get: async id => tabs.find(tab => tab.id === id),
      remove: async id => { operations.push(['remove', id]); }
    }
  };
  vm.runInNewContext(read(flavor, 'complete.js'), {
    browser: api, URL, document: { querySelector: selector => selector === '#status' ? status : button }, console
  });
  return { operations, status, button, click: () => click(), settle: () => new Promise(resolve => setImmediate(resolve)) };
}

for (const flavor of ['chromium', 'safari']) {
  test(flavor + ': preparation creates Flow before closing stale Flow, preserves other sites and Google auth', async () => {
    const h = harness(flavor);
    await h.settle();
    const creates = h.operations.filter(op => op[0] === 'create');
    assert.equal(creates.length, 1);
    assert.equal(creates[0][1].url, 'https://flow.google.com/');
    assert.equal(creates[0][1].windowId, 7);
    assert.deepEqual(h.operations.filter(op => op[0] === 'remove').map(op => op[1]), [2, 1]);
    const cookies = h.operations.filter(op => op[0] === 'expire');
    assert.equal(cookies.length, 1);
    assert.equal(cookies[0][1].name, 'flow');
    assert.equal(cookies[0][1].expirationDate, 0);
    assert.ok(h.operations.indexOf(creates[0]) < h.operations.findIndex(op => op[0] === 'remove'));
  });
  test(flavor + ': permission is requested only by a click and rejection preserves all tabs', async () => {
    const h = harness(flavor, { permission: false, request: false });
    await h.settle();
    assert.deepEqual(h.operations, []);
    assert.equal(h.button.hidden, false);
    h.click(); await h.settle();
    assert.deepEqual(h.operations, ['permission-request']);
  });
  test(flavor + ': failures never close existing tabs or report completed preparation', async () => {
    for (const failure of [{ cookieFailure: true }, { createFailure: true }]) {
      const h = harness(flavor, failure); await h.settle();
      assert.equal(h.operations.filter(op => op[0] === 'remove').length, 0);
      assert.match(h.status.textContent, /Подготовка не завершена/);
      assert.equal(h.button.hidden, false);
    }
  });
  test(flavor + ': no old Flow means no cookie access', async () => {
    const h = harness(flavor, { oldFlow: false }); await h.settle();
    assert.equal(h.operations.filter(op => op[0] === 'cookies-read').length, 0);
  });
}

test('onInstalled runs onboarding only for new installs, not browser restarts or extension updates', async () => {
  let listener; const opened = [];
  vm.runInNewContext(read('safari', 'setup-background.js'), { browser: {
    runtime: { onInstalled: { addListener: fn => { listener = fn; } }, getURL: file => 'extension://self/' + file },
    tabs: { create: async details => { opened.push(details.url); } }
  }, console });
  listener({ reason: 'update' }); listener({ reason: 'browser_update' });
  assert.equal(opened.length, 0);
  listener({ reason: 'install' });
  assert.deepEqual(opened, ['extension://self/complete.html']);
});
