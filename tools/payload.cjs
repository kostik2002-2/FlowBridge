'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '..');
const signedName = 'FlowBridge-0.1.1-signed.xpi';
const signedHash = '7da9235f189cf8b62d5dc82c5c3204e555112c91f3324a3cae05f8054c5a5fb1';

function createPayload() {
  // Firefox is installed exclusively from the signed XPI, without an unsigned duplicate.
  const folder = path.join(root, 'package', 'extension', 'chromium');
  const entries = fs.readdirSync(folder).sort().map(name => [
    'chromium/' + name, fs.readFileSync(path.join(folder, name))
  ]);
  const signed = fs.readFileSync(path.join(root, 'package', 'signing', signedName));
  if (crypto.createHash('sha256').update(signed).digest('hex') !== signedHash) {
    throw Error('Signed Firefox XPI hash mismatch');
  }
  entries.push(['firefox/' + signedName, signed]);
  const rows = entries.map(([name, data]) => '      {' + JSON.stringify(name) + ', "' + data.toString('base64') + '"}');
  return 'using System.Collections.Generic;\nnamespace FlowBridge { static class Payload { public static readonly Dictionary<string,string> Files = new Dictionary<string,string> {\n' + rows.join(',\n') + '\n}; } }\n';
}

if (require.main === module) {
  fs.writeFileSync(path.join(root, 'installer', 'Payload.cs'), createPayload());
  console.log('Generated embedded extension payload.');
}
module.exports = { createPayload };
