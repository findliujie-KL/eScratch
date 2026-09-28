const { test } = require('node:test')
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const ts = require('typescript')
const vm = require('node:vm')
const host = { exports: {} }
vm.runInNewContext(ts.transpileModule(fs.readFileSync(path.join(__dirname, '../electron/ocrText.ts'), 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText, host)
const clean = host.exports.cleanOcrText

test('Chinese OCR spacing does not change English or numbers', () => {
  assert.equal(clean('中 文 English words 123 中 文', false), '中文 English words 123 中文')
  assert.equal(clean('变 化 ， 风 险 。 English 123', false), '变化，风险。 English 123')
  assert.equal(clean('控制 ? 如果', false), '控制?如果')
})
test('Wrapped OCR lines join within a paragraph and preserve blank lines', () => {
  assert.equal(clean('这 种\n变 化\n\nEnglish words\ncontinue here', true), '这种变化\n\nEnglish words continue here')
  assert.equal(clean('第一项\n2. 第二项', true), '第一项\n2. 第二项')
  assert.equal(clean('句子。\n下一句\n\nEnglish.\nNext', true), '句子。下一句\n\nEnglish. Next')
})
