(() => {
  'use strict';
  const api = globalThis.browser || globalThis.chrome;
  let panel, button, line, timer, dismissed = false;
  let current = { state: 'waiting', count: 0 };
  const labels = { waiting: 'Проверяем совместимость…', matched: 'Проверка найдена', applied: 'Исправление применено', late: 'Обновите вкладку', incompatible: 'Нужна новая версия', 'not-found': 'Совместимость не подтверждена' };
  function view() {
    if (!document.documentElement || dismissed) return;
    if (!panel) {
      panel = document.createElement('aside'); panel.id = 'flowbridge-status';
      panel.style.cssText = 'position:fixed;bottom:14px;right:14px;z-index:2147483647';
      const shadow = panel.attachShadow({ mode: 'closed' });
      const style = document.createElement('style');
      style.textContent = ':host{all:initial}section{display:flex;gap:12px;align-items:center;background:#122229;color:#e7faf5;border:1px solid #294951;border-radius:12px;padding:10px 14px;box-shadow:0 5px 24px #0003;font:12px/1.4 system-ui}strong{display:block;font-size:11px;color:#69dab5}button{background:none;border:0;color:#adc3c6;font-size:18px;cursor:pointer;padding:3px}';
      const box = document.createElement('section'); const text = document.createElement('div');
      const title = document.createElement('strong'); title.textContent = 'FLOWBRIDGE · PREVIEW';
      const logo = document.createElement('img'); logo.src = '__FLOWBRIDGE_ICON_DATA__'; logo.alt = ''; logo.width = logo.height = 16; logo.style.cssText = 'vertical-align:middle;margin-right:6px'; title.prepend(logo);
      line = document.createElement('span'); text.append(title, line);
      button = document.createElement('button'); button.textContent = '×'; button.title = 'Скрыть сообщение';
      button.addEventListener('click', () => { dismissed = true; panel.remove(); });
      box.append(text, button); shadow.append(style, box); document.documentElement.append(panel);
    }
    line.textContent = labels[current.state];
    clearTimeout(timer);
    if (current.state === 'applied') timer = setTimeout(() => { dismissed = true; panel?.remove(); }, 12000);
  }
  addEventListener('message', event => {
    if (event.source !== window || event.origin !== location.origin) return;
    const data = event.data;
    if (data?.source !== 'flowbridge-local-v1' || !Object.hasOwn(labels, data.state)) return;
    // Status is advisory: the web page can spoof MAIN-world messages.
    current = { state: data.state, count: Number.isSafeInteger(data.count) ? Math.min(Math.max(data.count, 0), 99999) : 0 };
    view();
    if (current.state === 'applied' && location.pathname === '/unsupported-country') {
      try {
        const key = 'flowbridge-recovery-v1'; const last = Number(sessionStorage.getItem(key) || 0);
        if (Date.now() - last > 60000) { sessionStorage.setItem(key, String(Date.now())); location.replace('https://flow.google.com/'); }
      } catch { /* Manual reload remains available when browser storage is disabled. */ }
    }
  });
  api.runtime.onMessage.addListener((message, sender, respond) => {
    if (message?.type !== 'flowbridge-status') return false;
    respond({ version: '0.1.0', ...current }); return false;
  });
  if (document.documentElement) view(); else document.addEventListener('DOMContentLoaded', view, { once: true });
})();
