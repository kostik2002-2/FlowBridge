'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { createPayload } = require('../tools/payload.cjs');

test('EXE embeds exact Chromium files and only the verified signed Firefox XPI', () => {
  const entries = [...createPayload().matchAll(/\{"([^"]+)", "([A-Za-z0-9+/=]+)"\}/g)];
  const payload = new Map(entries.map(([, name, data]) => [name, Buffer.from(data, 'base64')]));
  assert.equal(payload.size, entries.length, 'duplicate payload paths');
  const folder = path.resolve(__dirname, '../package/extension/chromium');
  const names = [
    'manifest.json', 'engine.js', 'content.js', 'popup.html', 'popup.css', 'popup.js', 'complete.html', 'complete.js',
    ...[16,20,24,32,40,48,64,96,128,256].map(size => 'icon-' + size + '.png')
  ].sort();
  assert.deepEqual(fs.readdirSync(folder).sort(), names, 'unexpected build output');
  assert.deepEqual([...payload.keys()].sort(), [
    ...names.map(name => 'chromium/' + name), 'firefox/FlowBridge-0.1.1-signed.xpi'
  ].sort());
  for (const name of names) assert.deepEqual(payload.get('chromium/' + name), fs.readFileSync(path.join(folder, name)));
  const signed = payload.get('firefox/FlowBridge-0.1.1-signed.xpi');
  assert.equal(crypto.createHash('sha256').update(signed).digest('hex'), '7da9235f189cf8b62d5dc82c5c3204e555112c91f3324a3cae05f8054c5a5fb1');
});
