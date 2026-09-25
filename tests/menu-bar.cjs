const assert = require('node:assert/strict');
const { test } = require('node:test');
const { EventEmitter } = require('node:events');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');

// Exercise the real main process with an in-memory Electron host, without
// opening windows, touching the clipboard, or changing the user's notes.
async function launch(platform = 'darwin', initialConfig = null, blockedShortcut = null, login = false, portable = false) {
  const windows = [], trays = [], shortcuts = new Map(), ipcHandlers = new Map(), writes = [];
  const storedFiles = new Map();
  const normalizePath = file => file.replaceAll('\\', '/');
  if (initialConfig !== null) storedFiles.set('/test-data/config.json', JSON.stringify(initialConfig));
  const app = new EventEmitter();
  Object.assign(app, {
    isPackaged: portable,
    getAppPath: () => '/app',
    getLoginItemSettings: () => ({ openAtLogin: !!app.loginEnabled, wasOpenedAtLogin: login }),
    setLoginItemSettings(options) { app.loginOptions = options; app.loginEnabled = options.openAtLogin; },
    getPath: () => '/test-data', whenReady: () => Promise.resolve(),
    setName(name) { this.name = name; },
    dock: {
      hidden: false,
      hide() { this.hidden = true; app.dockHidden = true; },
      show() { this.hidden = false; app.dockHidden = false; return Promise.resolve(); },
    },
    hide() { app.hidden = true; windows.forEach(w => w.hide()); },
    quit() { app.emit('before-quit'); app.emit('will-quit'); app.quitCalled = true; }
  });
  class Window extends EventEmitter {
    constructor(options) { super(); this.options = options; this.visible = false; this.messages = []; this.webContents = { send: (...args) => this.messages.push(args) }; windows.push(this); }
    loadFile() { this.emit('ready-to-show'); }
    show() { this.visible = true; }
    hide() { this.visible = false; }
    focus() {}
    setAlwaysOnTop(value) { this.alwaysOnTop = value; }
    isVisible() { return this.visible; }
    isMinimized() { return !!this.minimized; }
    restore() { this.minimized = false; }
    close() {
      let prevented = false;
      this.emit('close', { preventDefault() { prevented = true; } });
      if (!prevented) { this.destroyed = true; this.emit('closed'); }
    }
  }
  class Tray extends EventEmitter {
    constructor() { super(); trays.push(this); }
    setToolTip() {}
    setContextMenu(menu) { this.menu = menu; }
    destroy() { this.destroyed = true; }
  }
  const electron = {
    app, BrowserWindow: Window, Tray,
    Menu: { buildFromTemplate: value => value },
    nativeImage: {
      createFromDataURL: () => ({ resize() { return this; }, setTemplateImage() {}, isEmpty() { return false; } }),
      createFromPath: () => ({ isEmpty() { return false; } }),
    },
    globalShortcut: {
      unregisterAll: () => shortcuts.clear(), unregister: key => shortcuts.delete(key),
      isRegistered: key => shortcuts.has(key),
      register: (key, fn) => { if (key === blockedShortcut) return false; shortcuts.set(key, fn); return true; }
    },
    ipcMain: { handle: (name, handler) => ipcHandlers.set(name, handler) }, clipboard: { writeText() {} }
  };
  const fsMock = {
    existsSync: file => storedFiles.has(normalizePath(file)),
    readFileSync: file => storedFiles.get(normalizePath(file)) || '',
    writeFileSync: (file, data) => {
      storedFiles.set(normalizePath(file), data);
      writes.push({ file, data });
    },
  };
  const source = fs.readFileSync(path.join(__dirname, '../electron/main.ts'), 'utf8');
  const code = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, esModuleInterop: true } }).outputText;
  vm.runInNewContext(code, {
    require: name => name === 'electron' ? electron : name === 'node:fs' ? fsMock : require(name),
    exports: {}, __dirname: '/app/dist-electron', process: { platform, argv: login ? ['--login'] : [], execPath: '/runtime/electron.exe', resourcesPath: '/resources', env: portable ? { PORTABLE_EXECUTABLE_FILE: 'C:/Apps/eScratch.exe' } : {} }, console
  });
  await new Promise(resolve => setImmediate(resolve));
  return { app, windows, trays, shortcuts, ipcHandlers, writes, storedFiles };
}

test('Tray-only mode is the default on Mac', async () => {
  const { app, trays } = await launch();
  assert.equal(trays.length, 1);
  assert.equal(app.dockHidden, true);
});
test('Mac menu-bar mode removes the Dock icon and provides a menu-bar entry', async () => {
  const { app, trays } = await launch('darwin', { showInMenuBar: true });
  assert.equal(trays.length, 1);
  assert.equal(app.dockHidden, true);
  assert.ok(trays[0].menu.some(item => item.label === 'Quit eScratch'));
});
test('closing a menu-bar Mac window preserves it and Control+J brings it back', async () => {
  const { windows, shortcuts } = await launch('darwin', { showInMenuBar: true });
  const window = windows[0];
  window.close();
  assert.ok(!window.destroyed, 'closing must preserve the current draft window');
  assert.equal(window.visible, false);
  shortcuts.get('Control+J')();
  assert.equal(windows.length, 1);
  assert.equal(window.visible, true);
});
test('closing a preserved menu-bar window does not duplicate the draft in history', async () => {
  const { app, windows, ipcHandlers, writes } = await launch('darwin', { showInMenuBar: true });
  await ipcHandlers.get('sync-text')(null, 'draft');
  windows[0].close();
  windows[0].show();
  windows[0].close();
  assert.equal(writes.length, 0, 'hiding the preserved window must not save the draft');
  app.quit();
  assert.equal(writes.length, 1, 'quitting must save the draft exactly once');
  assert.equal(JSON.parse(writes[0].data)[0].text, 'draft');
});
test('closing a Mac window hides it without destroying the draft', async () => {
  const { windows, ipcHandlers, writes } = await launch();
  await ipcHandlers.get('sync-text')(null, 'draft');
  windows[0].close();
  assert.ok(!windows[0].destroyed);
  assert.equal(writes.length, 0);
});
test('menu can toggle the editor and quit the background app', async () => {
  const { app, trays, windows } = await launch('darwin', { showInMenuBar: true });
  assert.equal(trays.length, 1);
  const toggle = trays[0].menu.find(item => item.label === 'Show / Hide Editor');
  toggle.click();
  assert.equal(windows[0].visible, false);
  toggle.click();
  assert.equal(windows[0].visible, true);
  trays[0].menu.find(item => item.label === 'Quit eScratch').click();
  assert.equal(app.quitCalled, true);
  windows[0].close();
  assert.equal(windows[0].destroyed, true, 'Quit must not be intercepted as hide');
});
test('Windows close hides the editor in the tray and double-click restores it', async () => {
  const { app, trays, windows } = await launch('win32', { showInMenuBar: true });
  assert.equal(trays.length, 1);
  assert.ok(!app.dockHidden);
  windows[0].close();
  assert.ok(!windows[0].destroyed);
  assert.equal(windows[0].visible, false);
  trays[0].emit('double-click');
  assert.equal(windows[0].visible, true);
});

test('Windows tray menu can quit the app completely', async () => {
  const { app, trays, windows } = await launch('win32');
  trays[0].menu.find(item => item.label === 'Quit eScratch').click();
  assert.equal(app.quitCalled, true);
  windows[0].close();
  assert.equal(windows[0].destroyed, true);
});

test('close-window IPC hides the Windows editor', async () => {
  const { windows, ipcHandlers } = await launch('win32');
  await ipcHandlers.get('close-window')();
  assert.equal(windows[0].visible, false);
  assert.ok(!windows[0].destroyed);
});

test('history defaults to 10 entries, can be resized, and can be cleared', async () => {
  const { ipcHandlers } = await launch('win32');
  assert.equal((await ipcHandlers.get('get-config')()).historyLimit, 10);
  let history = [];
  for (let index = 0; index < 12; index += 1) {
    history = await ipcHandlers.get('save-to-history')(null, `entry ${index}`);
  }
  assert.equal(history.length, 10);
  assert.equal(history[0].text, 'entry 11');
  const resized = await ipcHandlers.get('set-history-limit')(null, 3);
  assert.equal(resized.historyLimit, 3);
  assert.equal(resized.history.length, 3);
  assert.equal((await ipcHandlers.get('clear-history')()).length, 0);
  assert.equal((await ipcHandlers.get('get-history')()).length, 0);
});

test('Mac shortcut hides the application to return focus to the previous app', async () => {
  const { app, shortcuts, windows } = await launch('darwin', { showInMenuBar: true });
  shortcuts.get('Control+J')();
  assert.equal(app.hidden, true);
  assert.equal(windows[0].visible, false);
});
test('shortcut restores a minimized editor', async () => {
  const { shortcuts, windows } = await launch();
  windows[0].minimized = true;
  shortcuts.get('Control+J')();
  assert.equal(windows[0].minimized, false);
  assert.equal(windows[0].visible, true);
});
test('enabling the menu-bar preference from settings creates the tray and hides the Dock', async () => {
  const { app, trays, ipcHandlers, writes } = await launch();
  assert.equal(trays.length, 1);
  const applied = await ipcHandlers.get('set-show-in-menu-bar')(null, true);
  assert.equal(applied, true);
  assert.equal(trays.length, 1);
  assert.equal(app.dockHidden, true);
  assert.equal(writes.length, 1);
  assert.equal(JSON.parse(writes[0].data).showInMenuBar, true);
});
test('legacy menu-bar preferences cannot disable tray-only mode', async () => {
  const { app, trays, ipcHandlers, writes } = await launch('darwin', { showInMenuBar: true });
  const applied = await ipcHandlers.get('set-show-in-menu-bar')(null, false);
  assert.equal(applied, false);
  assert.ok(!trays[0].destroyed);
  assert.equal(app.dockHidden, true);
  assert.equal(writes.length, 1);
  assert.equal(JSON.parse(writes[0].data).showInMenuBar, false);
});

test('restore defaults resets settings and runtime state, keeps the draft and downloaded languages, and limits history to ten', async () => {
  const host = await launch('win32', { shortcut: 'Control+K', newShortcut: 'Control+N', copyShortcut: 'Alt+C', alwaysOnTop: true, indentType: 'tab', indentSize: 8, showWhitespace: true, ocrLanguages: ['spa'], historyLimit: 20 });
  const defaults = await launch('win32');
  host.storedFiles.set('/test-data/ocr-languages/spa.traineddata.gz', 'installed language');
  for (let i = 0; i < 12; i++) await host.ipcHandlers.get('save-to-history')(null, `note ${i}`);
  await host.ipcHandlers.get('sync-text')(null, 'current draft');
  const result = await host.ipcHandlers.get('restore-defaults')();
  assert.equal(JSON.stringify(result.config), JSON.stringify(await defaults.ipcHandlers.get('get-config')()));
  assert.equal(result.history.length, 10);
  assert.equal(result.history[0].text, 'note 11');
  assert.equal(host.shortcuts.has('Control+J'), true);
  assert.equal(host.shortcuts.has('Control+K'), false);
  assert.equal(host.windows[0].alwaysOnTop, false);
  assert.equal(host.storedFiles.get('/test-data/ocr-languages/spa.traineddata.gz'), 'installed language');
  host.app.quit();
  assert.equal(JSON.parse(host.storedFiles.get('/test-data/history.json'))[0].text, 'current draft');
});

test('restore defaults leaves settings and working shortcut intact when the default shortcut is unavailable', async () => {
  const host = await launch('win32', { shortcut: 'Control+K', historyLimit: 30 }, 'Control+J');
  const before = await host.ipcHandlers.get('get-config')();
  await assert.rejects(host.ipcHandlers.get('restore-defaults')(), /in use/);
  assert.equal(JSON.stringify(await host.ipcHandlers.get('get-config')()), JSON.stringify(before));
  assert.equal(host.shortcuts.has('Control+K'), true);
  assert.equal(host.writes.length, 0);
});

test('restore defaults keeps macOS tray-only mode', async () => {
  const host = await launch('darwin', { showInMenuBar: true });
  await host.ipcHandlers.get('restore-defaults')();
  assert.equal(host.app.dockHidden, true);
  assert.ok(!host.trays[0].destroyed);
});

test('Windows has no taskbar icon and resumes only once per hide', async () => {
  const { windows, trays } = await launch('win32'); const w = windows[0];
  assert.equal(w.options.skipTaskbar, true);
  trays[0].emit('double-click'); assert.equal(w.messages.length, 0);
  w.close(); trays[0].emit('double-click');
  assert.equal(w.messages.filter(m => m[0] === 'resume-entry').length, 1);
  trays[0].emit('double-click'); assert.equal(w.messages.length, 1);
  w.minimized = true; w.emit('minimize'); trays[0].emit('double-click');
  assert.equal(w.messages.length, 2); assert.equal(w.minimized, false);
});
test('Login launch remains hidden and startup targets the portable launcher', async () => {
  const { app, windows, ipcHandlers } = await launch('win32', null, null, true, true);
  assert.equal(windows[0].visible, false);
  assert.equal(await ipcHandlers.get('set-start-at-login')(null, true), true);
  assert.equal(app.loginOptions.path, 'C:/Apps/eScratch.exe');
  assert.deepEqual(Array.from(app.loginOptions.args), ['--login']);
  assert.equal(await ipcHandlers.get('set-start-at-login')(null, false), false);
});
test('Development startup registers the application directory', async () => {
  const { app, ipcHandlers } = await launch('win32');
  await ipcHandlers.get('set-start-at-login')(null, true);
  assert.deepEqual(Array.from(app.loginOptions.args), ['/app', '--login']);
});

test('Word count defaults on, persists off, and restores with defaults', async () => {
  const { ipcHandlers } = await launch('win32');
  assert.equal((await ipcHandlers.get('get-config')()).showWordCount, true);
  assert.equal(await ipcHandlers.get('set-show-word-count')(null, false), false);
  assert.equal((await ipcHandlers.get('get-config')()).showWordCount, false);
  assert.equal((await ipcHandlers.get('restore-defaults')()).config.showWordCount, true);
});
test('Always on top updates the native window and saved setting', async () => {
  const { ipcHandlers, windows } = await launch('win32');
  await ipcHandlers.get('set-always-on-top')(null, true);
  assert.equal(windows[0].alwaysOnTop, true);
  assert.equal((await ipcHandlers.get('get-config')()).alwaysOnTop, true);
  await ipcHandlers.get('set-always-on-top')(null, false);
  assert.equal(windows[0].alwaysOnTop, false);
});
