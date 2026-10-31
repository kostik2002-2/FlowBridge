'use strict';
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'..'),sizes=[16,20,24,32,40,48,64,96,128,256];
function png(bytes,size){assert.equal(bytes.subarray(0,8).toString('hex'),'89504e470d0a1a0a');assert.equal(bytes.readUInt32BE(16),size);assert.equal(bytes.readUInt32BE(20),size);}
for(const size of sizes)png(fs.readFileSync(path.join(root,'assets','icon-'+size+'.png')),size);
const ico=fs.readFileSync(path.join(root,'assets','flowbridge.ico'));
assert.equal(ico.readUInt16LE(2),1);assert.equal(ico.readUInt16LE(4),sizes.length);
for(let i=0;i<sizes.length;i++){const entry=6+i*16,length=ico.readUInt32LE(entry+8),offset=ico.readUInt32LE(entry+12);assert.ok(offset+length<=ico.length);assert.equal(ico[entry]||256,sizes[i]);png(ico.subarray(offset,offset+length),sizes[i]);}
for(const flavor of ['chromium','firefox']){
 const folder=path.join(root,'package','extension',flavor),manifest=JSON.parse(fs.readFileSync(path.join(folder,'manifest.json')));
 for(const size of sizes){png(fs.readFileSync(path.join(folder,manifest.icons[size])),size);assert.equal(manifest.action.default_icon[size],manifest.icons[size]);}
 assert.ok(fs.readFileSync(path.join(folder,'popup.html'),'utf8').includes('<img class="mark"'));
 const content=fs.readFileSync(path.join(folder,'content.js'),'utf8');assert.ok(content.includes('data:image/png;base64,'));assert.ok(!content.includes('__FLOWBRIDGE_ICON_DATA__'));
}
console.log('PASS: ICO has 10 valid PNG frames; Chromium/Firefox manifest and toolbar icons resolve; popup and in-page assets embedded.');
