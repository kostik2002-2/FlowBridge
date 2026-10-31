'use strict';
const FLOW_URL = 'https://flow.google.com/';
function isFlow(url) {
  try { return new URL(url).origin === 'https://flow.google.com'; } catch { return false; }
}
function isFlowCookie(cookie) {
  return cookie.domain.replace(/^\./, '').toLowerCase() === 'flow.google.com';
}
async function prepareFlow(api) {
  const setup = await api.tabs.getCurrent();
  if (!Number.isInteger(setup?.id) || !Number.isInteger(setup?.windowId)) throw Error('Не удалось определить окно установки.');
  const tabs = await api.tabs.query({ windowId: setup.windowId });
  const ownPage = (setup.url || '').split(/[?#]/)[0];
  const installerTabs = tabs.filter(tab => tab.id !== setup.id && (
    /^(?:chrome|edge|opera|comet|browser):\/\/extensions\/?(?:[?#].*)?$/.test(tab.url || '') ||
    (ownPage && (tab.url || '').split(/[?#]/)[0] === ownPage)
  ));
  const old = tabs.filter(tab => tab.id !== setup.id && isFlow(tab.url));
  if (old.length) {
    // Never query or expire .google.com authentication cookies.
    const normal = await api.cookies.getAll({ domain: 'flow.google.com' });
    const partitioned = await api.cookies.getAll({ domain: 'flow.google.com', partitionKey: {} });
    const cookies = new Map();
    for (const cookie of [...normal, ...partitioned].filter(isFlowCookie)) {
      cookies.set(JSON.stringify([cookie.name,cookie.domain,cookie.path,cookie.storeId,cookie.partitionKey]),cookie);
    }
    for (const cookie of cookies.values()) {
      // Expire the exact domain/path identity; remove(url,name) can choose a parent-domain cookie with the same name.
      const expired = {
        url: FLOW_URL.slice(0,-1) + (cookie.path || '/'), name: cookie.name, value: '',
        path: cookie.path || '/', expirationDate: 0, storeId: cookie.storeId,
        secure: cookie.secure, httpOnly: cookie.httpOnly, sameSite: cookie.sameSite
      };
      if (!cookie.hostOnly) expired.domain = cookie.domain;
      if (cookie.partitionKey) expired.partitionKey = cookie.partitionKey;
      await api.cookies.set(expired);
    }
  }
  // Create successfully before removing anything. Never close unrelated or other-window tabs.
  const fresh = await api.tabs.create({ windowId: setup.windowId, index: setup.index + 1, url: FLOW_URL, active: true });
  if (!Number.isInteger(fresh?.id)) throw Error('Не удалось открыть новую вкладку Flow.');
  for (const tab of old) {
    // The user may have navigated the tab elsewhere while setup was running.
    try {
      const current = await api.tabs.get(tab.id);
      if (current.windowId === setup.windowId && isFlow(current.url)) await api.tabs.remove(tab.id);
    } catch (error) {
      // A concurrently closed tab needs no further action; other errors must stay visible.
      const remaining = await api.tabs.query({ windowId: setup.windowId });
      if (remaining.some(item => item.id === tab.id && isFlow(item.url))) throw error;
    }
  }
  for (const installerTab of installerTabs) {
    try {
      const current = await api.tabs.get(installerTab.id);
      if (current.url === installerTab.url) await api.tabs.remove(installerTab.id);
    } catch (error) {
      const remaining = await api.tabs.query({windowId: setup.windowId});
      if (remaining.some(tab => tab.id === installerTab.id)) throw error;
    }
  }
  await api.tabs.remove(setup.id);
  return fresh.id;
}
module.exports = { isFlow, isFlowCookie, prepareFlow };
