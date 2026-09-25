const assert = require('node:assert/strict');
const { test } = require('node:test');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
// Exercise the actual resume effect with a controlled IPC completion, including
// a user editing while the previous draft is being saved.
const source = fs.readFileSync(path.join(__dirname, '../src/App.tsx'), 'utf8');
const effect = source.match(/useEffect\(\(\) => window\.electronAPI\.onResumeEntry\(\(\) => \{([\s\S]*?)\n  \}\), \[\]\)/)[1];
async function resume(text, { fail = false, edit = false } = {}) {
  let finish, callback; const archived = []; const field = { value: text, focus() {} };
  const host = {
    resuming: { current: false }, resumeGeneration: { current: 0 }, textareaRef: { current: field },
    window: { electronAPI: {
      saveToHistory(value) { archived.push(value); return new Promise((resolve, reject) => { finish = () => fail ? reject(Error('disk full')) : resolve([]); }); },
      getHistory() { return Promise.resolve([]); }, syncText() {},
    } },
    setText(value) { field.value = value; }, setHistory() {}, setShowHistory() {}, setShowSettings() {}, setPasteMenu() {}, setPasteMessage() {},
  };
  callback = vm.runInNewContext(`() => {${effect}}`, host);
  callback(); callback(); // repeated activation during save must not cancel clearing
  if (edit) field.value = 'new typing';
  finish?.(); await new Promise(resolve => setImmediate(resolve));
  return { text: field.value, archived };
}
test('Resume archives once and opens a blank draft', async () => assert.deepEqual(await resume('old draft'), { text: '', archived: ['old draft'] }));
test('Failed history write preserves draft', async () => assert.equal((await resume('old draft', { fail: true })).text, 'old draft'));
test('Typing during history save is preserved', async () => assert.equal((await resume('old draft', { edit: true })).text, 'new typing'));
test('Blank draft does not create history', async () => assert.deepEqual(await resume(''), { text: '', archived: [] }));
