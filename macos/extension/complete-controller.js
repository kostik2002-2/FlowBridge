const api = globalThis.browser || globalThis.chrome;
const status = document.querySelector('#status');
const button = document.querySelector('#prepare');
let running = false;
async function finish(requestPermission) {
  if (running) return;
  running = true;
  button.disabled = true;
  try {
    const origins = { origins: ['https://flow.google.com/*'] };
    const allowed = requestPermission
      ? await api.permissions.request(origins)
      : await api.permissions.contains(origins);
    if (!allowed) {
      status.textContent = 'Разрешите расширению доступ только к flow.google.com.';
      button.hidden = false;
      return;
    }
    status.textContent = 'Расширение установлено. Подготавливаем Flow…';
    await module.exports.prepareFlow(api);
  } catch (error) {
    status.textContent = 'Подготовка не завершена: ' + error.message + '. Проверьте разрешение для Flow в настройках расширения и повторите.';
    button.textContent = 'Повторить подготовку';
    button.hidden = false;
  } finally {
    running = false;
    button.disabled = false;
  }
}
button.addEventListener('click', () => finish(true));
finish(false);
