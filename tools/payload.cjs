'use strict';
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'..');const rows=[];
for(const flavor of ['chromium','firefox'])for(const name of fs.readdirSync(path.join(root,'package','extension',flavor)).sort()) {
  const data=fs.readFileSync(path.join(root,'package','extension',flavor,name)).toString('base64');
  rows.push('      {"'+flavor+'/'+name+'", "'+data+'"}');
}
const signed=fs.readFileSync(path.join(root,'package','signing','FlowBridge-0.1.1-signed.xpi'));
const crypto=require('node:crypto');
if(crypto.createHash('sha256').update(signed).digest('hex')!=='7da9235f189cf8b62d5dc82c5c3204e555112c91f3324a3cae05f8054c5a5fb1')throw Error('Signed Firefox XPI hash mismatch');
rows.push('      {"firefox/FlowBridge-0.1.1-signed.xpi", "'+signed.toString('base64')+'"}');
fs.writeFileSync(path.join(root,'installer','Payload.cs'),'using System.Collections.Generic;\nnamespace FlowBridge { static class Payload { public static readonly Dictionary<string,string> Files = new Dictionary<string,string> {\n'+rows.join(',\n')+'\n}; } }\n');
console.log('Generated embedded extension payload.');
