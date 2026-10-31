'use strict';
const {test}=require('node:test');const assert=require('node:assert/strict');const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const root=path.resolve(__dirname,'..','package','extension');
test('both packages are restricted to Flow and contain no debugger or remote code loading',()=>{
  for(const flavor of ['chromium','firefox']){
    const folder=path.join(root,flavor),manifest=JSON.parse(fs.readFileSync(path.join(folder,'manifest.json')));
    assert.deepEqual(manifest.permissions,['tabs','cookies']);
    assert.deepEqual(manifest.host_permissions,['https://flow.google.com/*']);
    assert.equal(manifest.web_accessible_resources,undefined);
    for(const file of ['complete.html','complete.js'])assert.ok(fs.existsSync(path.join(folder,file)));
    for(const entry of manifest.content_scripts){assert.deepEqual(entry.matches,['https://flow.google.com/*']);for(const file of entry.js)assert.ok(fs.existsSync(path.join(folder,file)));}
    const engine=fs.readFileSync(path.join(folder,'engine.js'),'utf8');
    assert.doesNotMatch(engine,/\b(?:fetch|XMLHttpRequest|eval)\s*\(|chrome\.debugger|document\.cookie/);
  }
});
test('bundled runtime ignores unrelated origins and child frames',()=>{
  const source=fs.readFileSync(path.join(root,'chromium','engine.js'),'utf8');
  for(const frame of [false,true]){
    const win={};win.top=frame?{}:win;
    const context={window:win,location:{origin:frame?'https://flow.google.com':'https://example.org'}};
    vm.runInNewContext(source,context);
    assert.equal(Object.getOwnPropertyDescriptor(win,'default_AiSandboxAngularFrontend'),undefined);
  }
});
