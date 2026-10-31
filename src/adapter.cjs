'use strict';

// This adapter observes the computed-signal factory in Google's public frontend.
// It does not replace constructors or intercept HTTP responses.
function isCountryComputation(fn) {
  if (typeof fn !== 'function') return false;
  const text = Function.prototype.toString.call(fn);
  if (text.length > 650 || !/this\s*\.\s*config\s*\(\s*\)/.test(text)) return false;
  const readers = [...text.matchAll(/_\.[\w$]+\(\s*[\w$]+\s*,\s*(\d+)\s*\)/g)];
  return readers.length === 1 && readers[0][1] === '31' &&
    /!=\s*null\s*\?/.test(text) && /:\s*!0\s*;?\s*\}?$/.test(text.trim());
}

function attachAdapter(root, onState) {
  const property = 'default_AiSandboxAngularFrontend';
  const prior = Object.getOwnPropertyDescriptor(root, property);
  if (prior) { onState('late'); return { dispose() {} }; }
  let stopped = false;
  let namespace;
  let hits = 0;
  const nativeFactories = new WeakMap();
  const instrumented = new WeakSet();
  const restorations = [];
  function report(state) { if (!stopped) onState(state, hits); }
  function interceptFactory(factory) {
    if (typeof factory !== 'function') return factory;
    if (nativeFactories.has(factory)) return nativeFactories.get(factory);
    const wrapped = new Proxy(factory, {
      apply(target, receiver, args) {
        if (stopped || !isCountryComputation(args[0])) return Reflect.apply(target, receiver, args);
        const compute = args[0];
        report('matched');
        const replacement = function () {
          const result = Reflect.apply(compute, this, arguments);
          if (!stopped && result === false) { hits++; report('applied'); return true; }
          return result;
        };
        return Reflect.apply(target, receiver, [replacement, ...args.slice(1)]);
      }
    });
    nativeFactories.set(factory, wrapped);
    nativeFactories.set(wrapped, wrapped);
    return wrapped;
  }
  function watch(object) {
    if (!object || typeof object !== 'object' || instrumented.has(object)) return;
    instrumented.add(object);
    // Google renamed its computed-signal export from V to W on 2026-10-31.
    // Both are observed; only the narrowly matched country computation changes.
    for (const key of ['V', 'W']) {
      const original = Object.getOwnPropertyDescriptor(object, key);
      if (original && (!original.configurable || !('value' in original))) { report('incompatible'); continue; }
      let raw = original?.value;
      let value = interceptFactory(raw);
      const get = () => value;
      const set = next => { raw = next; value = interceptFactory(next); };
      Object.defineProperty(object, key, { configurable: true, enumerable: original?.enumerable ?? true, get, set });
      restorations.push(() => {
        if (Object.getOwnPropertyDescriptor(object, key)?.get !== get) return;
        if (raw === undefined && !original) delete object[key];
        else Object.defineProperty(object, key, original ? { ...original, value: raw } : { configurable: true, enumerable: true, writable: true, value: raw });
      });
    }
  }
  const globalGet = () => namespace;
  const globalSet = value => { namespace = value; watch(value); };
  Object.defineProperty(root, property, { configurable: true, enumerable: true, get: globalGet, set: globalSet });
  report('waiting');
  return {
    get hits() { return hits; },
    dispose() {
      if (stopped) return;
      stopped = true;
      restorations.forEach(restore => restore());
      if (Object.getOwnPropertyDescriptor(root, property)?.get === globalGet) {
        if (namespace === undefined) delete root[property];
        else Object.defineProperty(root, property, { configurable: true, enumerable: true, writable: true, value: namespace });
      }
    }
  };
}

module.exports = { isCountryComputation, attachAdapter };
