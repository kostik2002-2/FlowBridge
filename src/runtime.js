'use strict';
if (window === window.top && location.origin === 'https://flow.google.com') {
  const guard = Symbol.for('flowbridge.runtime.v1');
  if (!window[guard]) {
    window[guard] = true;
    let last = { state: 'waiting', count: 0 };
    const send = () => window.postMessage({ source: 'flowbridge-local-v1', ...last }, location.origin);
    const report = (state, count = 0) => { last = { state, count: Math.min(count, 99999) }; send(); };
    let adapter;
    try { adapter = module.exports.attachAdapter(window, report); }
    catch { report('incompatible'); }
    // A short bounded handshake tolerates either order of the two content worlds.
    const clock = setInterval(send, 700);
    setTimeout(() => {
      clearInterval(clock);
      if (last.state === 'waiting') report('not-found');
    }, 16000);
    addEventListener('pagehide', () => { clearInterval(clock); adapter?.dispose(); }, { once: true });
  }
}
