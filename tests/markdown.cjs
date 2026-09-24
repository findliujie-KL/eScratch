const { app, BrowserWindow } = require('electron')
const fs = require('node:fs')
const path = require('node:path')
const ts = require('typescript')
app.setPath('userData', path.join(app.getPath('temp'), 'escratch-markdown-tests-' + process.pid))
app.whenReady().then(async () => {
  const window = new BrowserWindow({ show: false, webPreferences: { sandbox: true } })
  try {
    await window.loadURL('data:text/html,<textarea id="editor"></textarea>')
    const source = fs.readFileSync(path.join(__dirname, '../src/markdownPaste.ts'), 'utf8')
    const code = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText
    const cases = JSON.parse(fs.readFileSync(path.join(__dirname, 'markdown-cases.json'), 'utf8'))
    const result = await window.webContents.executeJavaScript(`(() => {
      const exports = {}; ${code}
      const results = ${JSON.stringify(cases)}.map(test => {
        const actual = exports.pasteAsMarkdown(test.plain, test.html, test.rtf);
        if (actual !== test.expected) throw Error(test.name + ': ' + JSON.stringify(actual));
        return 'PASS: ' + test.name;
      });
      const editor = document.getElementById('editor');
      editor.value = 'Before OLD After'; editor.focus(); editor.setSelectionRange(7, 10);
      document.execCommand('insertText', false, exports.pasteAsMarkdown('pigdog', '<p>dog</p>', true));
      if (editor.value !== 'Before ~~pig~~dog After') throw Error('Selection insertion');
      document.execCommand('undo');
      if (editor.value !== 'Before OLD After') throw Error('Undo');
      results.push('PASS: Native insertion and undo'); return results;
    })()`)
    console.log(result.join('\n'))
    app.exit(0)
  } catch (error) { console.error(error); app.exit(1) }
})
