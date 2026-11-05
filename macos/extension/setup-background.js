'use strict';
const api = globalThis.browser || globalThis.chrome;
api.runtime.onInstalled.addListener(details => {
  if (details.reason !== 'install') return;
  api.tabs.create({ url: api.runtime.getURL('complete.html') }).catch(error => {
    console.error('FlowBridge: open the extension popup to finish setup.', error.message);
  });
});
