'use strict';
const fs = require('node:fs'), path = require('node:path');
const root = path.resolve(__dirname, '..');
const source = name => fs.readFileSync(path.join(root, 'src', name), 'utf8');
const engine = '/* FlowBridge 0.1.1 — independently authored preview */\n(() => {\nconst module = {exports:{}};\n' + source('adapter.cjs') + '\n' + source('runtime.js') + '\n})();\n';
const icons = Object.fromEntries([16,20,24,32,40,48,64,96,128,256].map(size=>[String(size),'icon-'+size+'.png']));
const manifest = { icons, manifest_version: 3, name: 'FlowBridge Preview', version: '0.1.1', description: 'Помощник совместимости Google Flow. Предварительная версия.', permissions: ['tabs', 'cookies'], host_permissions: ['https://flow.google.com/*'], action: { default_popup: 'popup.html', default_icon: icons, default_title: 'FlowBridge' }, content_scripts: [{ matches: ['https://flow.google.com/*'], js: ['content.js'], run_at: 'document_start' }, { matches: ['https://flow.google.com/*'], js: ['engine.js'], run_at: 'document_start', world: 'MAIN' }] };
for (const flavor of ['chromium','firefox']) {
  const folder = path.join(root, 'package', 'extension', flavor); fs.mkdirSync(folder, {recursive:true});
  const config = structuredClone(manifest);
  if (flavor === 'chromium') config.minimum_chrome_version = '120';
  else config.browser_specific_settings = { gecko: { id: 'flowbridge-preview@local.invalid', strict_min_version: '142.0', data_collection_permissions: { required: ['none'] } } };
  fs.writeFileSync(path.join(folder, 'manifest.json'), JSON.stringify(config,null,2));
  fs.writeFileSync(path.join(folder, 'engine.js'), engine);
  fs.writeFileSync(path.join(folder,'complete.js'), '(() => { const module = {exports:{}};\n' + source('prepare.cjs') + '\nmodule.exports.prepareFlow(globalThis.browser || globalThis.chrome).catch(error => { document.querySelector("#status").textContent = "Расширение установлено, но подготовка Flow не завершена: " + error.message; });\n})();\n');
  for (const file of Object.values(icons)) fs.copyFileSync(path.join(root,'assets',file),path.join(folder,file));
  const iconData='data:image/png;base64,'+fs.readFileSync(path.join(root,'assets','icon-32.png')).toString('base64');
  for (const f of ['content.js','popup.html','popup.css','popup.js','complete.html']) {
    const text=source(f).replace('__FLOWBRIDGE_ICON_DATA__',iconData);
    fs.writeFileSync(path.join(folder,f),text);
  }
}
console.log('Built Chromium and Firefox extension packages (Firefox unsigned).');
