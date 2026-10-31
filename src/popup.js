(() => {
  const api = globalThis.browser || globalThis.chrome;
  let tab;
  const title = document.querySelector('#title'), detail = document.querySelector('#detail');
  const states = { applied: ['Исправление применено', 'Проверьте работу редактора. Генерация требует отдельной проверки.'], matched: ['Проверка найдена', 'Механизм совместим с этой загрузкой Flow.'], waiting: ['Ожидаем Flow', 'Дождитесь загрузки страницы.'], late: ['Нужно обновить страницу', 'Нажмите кнопку ниже.'], incompatible: ['Нужна новая версия', 'Код Flow изменился. Расширение не применило исправление.'], 'not-found': ['Совместимость не подтверждена', 'Нужная проверка не обнаружена.'] };
  api.tabs.query({ active: true, currentWindow: true }).then(async tabs => {
    tab = tabs[0];
    if (!tab?.url?.startsWith('https://flow.google.com/')) { title.textContent = 'Откройте Google Flow'; detail.textContent = 'Расширение работает только на сайте Flow.'; return; }
    try {
      const status = await api.tabs.sendMessage(tab.id, { type: 'flowbridge-status' });
      const pair = states[status?.state] || states['not-found']; title.textContent = pair[0]; detail.textContent = pair[1];
    } catch { title.textContent = 'Обновите вкладку'; detail.textContent = 'Открытая до установки страница ещё не подключена.'; }
  }).catch(() => { title.textContent = 'Вкладка недоступна'; detail.textContent = 'Откройте Flow в обычной вкладке браузера.'; });
  document.querySelector('#open').onclick = () => api.tabs.create({ url: 'https://flow.google.com/' });
  document.querySelector('#reload').onclick = () => { if (tab?.url?.startsWith('https://flow.google.com/')) api.tabs.reload(tab.id); };
})();
