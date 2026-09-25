const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const ts = require('typescript');
const vm = require('node:vm');
const host = { exports: {} };
vm.runInNewContext(ts.transpileModule(fs.readFileSync(path.join(__dirname, '../src/wordCount.ts'), 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText, host);
const reference = JSON.parse(fs.readFileSync(path.join(__dirname, 'word-count-cases.json'), 'utf8'));
for (const {text, expected} of reference.cases) {
  test('Matches Word: ' + JSON.stringify(text), () => assert.equal(host.exports.countWords(text), expected));
}
