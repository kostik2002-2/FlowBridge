'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '..');
const output = path.join(root, 'package', 'macos');
const version = '0.2.0';
const read = file => fs.readFileSync(path.join(root, file), 'utf8');
fs.mkdirSync(output, { recursive: true });
for (const flavor of ['chromium', 'safari']) {
  const folder = path.join(output, flavor);
  fs.rmSync(folder, { recursive: true, force: true });
  fs.cpSync(path.join(root, 'package', 'extension', 'chromium'), folder, { recursive: true });
  const manifest = JSON.parse(fs.readFileSync(path.join(folder, 'manifest.json')));
  manifest.name = 'FlowBridge Mac Preview';
  manifest.version = version;
  manifest.description = 'Тестовая версия FlowBridge для macOS. Требует проверки на Mac.';
  manifest.background = flavor === 'safari'
    ? { scripts: ['setup-background.js'], persistent: false }
    : { service_worker: 'setup-background.js' };
  if (flavor === 'safari') delete manifest.minimum_chrome_version;
  fs.writeFileSync(path.join(folder, 'manifest.json'), JSON.stringify(manifest, null, 2));
  fs.writeFileSync(path.join(folder, 'setup-background.js'), read('macos/extension/setup-background.js'));
  fs.writeFileSync(path.join(folder, 'complete.html'), read('macos/extension/complete.html'));
  let prepare = read('src/prepare.cjs');
  // Safari does not expose Chromium's partitionKey filter. Keep all other failures visible.
  if (flavor === 'safari') {
    const old = "const partitioned = await api.cookies.getAll({ domain: 'flow.google.com', partitionKey: {} });";
    if (!prepare.includes(old)) throw Error('Cookie implementation changed; review Safari adapter');
    prepare = prepare.replace(old, 'const partitioned = []; // Safari: only cookies exposed by its API.');
  }
  fs.writeFileSync(path.join(folder, 'complete.js'),
    '(() => { const module = {exports:{}};\n' + prepare + '\n' + read('macos/extension/complete-controller.js') + '\n})();\n');
  let popup = read('src/popup.js');
  popup = popup.replace("api.tabs.create({ url: 'https://flow.google.com/' })", "api.tabs.create({ url: api.runtime.getURL('complete.html') })");
  fs.writeFileSync(path.join(folder, 'popup.js'), popup);
  fs.writeFileSync(path.join(folder, 'popup.html'), read('src/popup.html').replace('0.1.0', version));
  const content = fs.readFileSync(path.join(folder, 'content.js'), 'utf8').replace("version: '0.1.0'", "version: '" + version + "'");
  fs.writeFileSync(path.join(folder, 'content.js'), content);
}
const signed = fs.readFileSync(path.join(root, 'package/signing/FlowBridge-0.1.1-signed.xpi'));
if (crypto.createHash('sha256').update(signed).digest('hex') !== '7da9235f189cf8b62d5dc82c5c3204e555112c91f3324a3cae05f8054c5a5fb1') {
  throw Error('Signed Firefox package changed');
}
fs.writeFileSync(path.join(output, 'FlowBridge-0.1.1-signed.xpi'), signed);
console.log('Built experimental macOS Chromium/Safari extensions; preserved signed Firefox 0.1.1.');
