import { app, BrowserWindow, globalShortcut, ipcMain, clipboard, Tray, Menu, nativeImage, net } from 'electron'
import path from 'node:path'
import fs from 'node:fs'
import { createWorker } from 'tesseract.js'

process.env.DIST = path.join(__dirname, '../dist')
app.setName('eScratch')

let win: BrowserWindow | null = null
let tray: Tray | null = null
let isQuitting = false
let menuBarEnabled = false
let currentText = ''
let ocrWorker: Awaited<ReturnType<typeof createWorker>> | null = null
const VITE_DEV_SERVER_URL = process.env['VITE_DEV_SERVER_URL']

// Preserve settings and OCR downloads created under the original app name.
const legacyUserDataPath = path.join(app.getPath('appData'), 'one-time-editor')
const currentUserDataPath = app.getPath('userData')
if (legacyUserDataPath !== currentUserDataPath && fs.existsSync(legacyUserDataPath)) {
  fs.mkdirSync(currentUserDataPath, { recursive: true })
  for (const name of ['config.json', 'history.json']) {
    const source = path.join(legacyUserDataPath, name)
    const destination = path.join(currentUserDataPath, name)
    if (fs.existsSync(source) && !fs.existsSync(destination)) fs.copyFileSync(source, destination)
  }
  const legacyLanguages = path.join(legacyUserDataPath, 'ocr-languages')
  const currentLanguages = path.join(currentUserDataPath, 'ocr-languages')
  if (fs.existsSync(legacyLanguages) && !fs.existsSync(currentLanguages)) {
    fs.cpSync(legacyLanguages, currentLanguages, { recursive: true })
  }
}

// Config and history file paths
const configPath = path.join(app.getPath('userData'), 'config.json')
const historyPath = path.join(app.getPath('userData'), 'history.json')

const defaultMod = 'Control'
const defaultNewShortcut = `${defaultMod}+T`
const defaultCopyShortcut = `${defaultMod}+Shift+C`

const shortcutValidator = /^(Command|Control|Alt|Shift|Meta|Super)(\+(Command|Control|Alt|Shift|Meta|Super))*\+[A-Za-z0-9]$/

const OCR_LANGUAGES = [
  { code: 'afr', name: 'Afrikaans' },
  { code: 'amh', name: 'Amharic' },
  { code: 'ara', name: 'Arabic' },
  { code: 'asm', name: 'Assamese' },
  { code: 'aze', name: 'Azerbaijani' },
  { code: 'aze_cyrl', name: 'Azerbaijani (Cyrillic)' },
  { code: 'bel', name: 'Belarusian' },
  { code: 'ben', name: 'Bengali' },
  { code: 'bod', name: 'Tibetan' },
  { code: 'bos', name: 'Bosnian' },
  { code: 'bre', name: 'Breton' },
  { code: 'bul', name: 'Bulgarian' },
  { code: 'cat', name: 'Catalan / Valencian' },
  { code: 'ceb', name: 'Cebuano' },
  { code: 'ces', name: 'Czech' },
  { code: 'eng', name: 'English' },
  { code: 'chi_sim', name: 'Chinese (Simplified)' },
  { code: 'chi_tra', name: 'Chinese (Traditional)' },
  { code: 'chr', name: 'Cherokee' },
  { code: 'cos', name: 'Corsican' },
  { code: 'cym', name: 'Welsh' },
  { code: 'dan', name: 'Danish' },
  { code: 'deu', name: 'German' },
  { code: 'frk', name: 'German (Fraktur)' },
  { code: 'div', name: 'Dhivehi' },
  { code: 'dzo', name: 'Dzongkha' },
  { code: 'ell', name: 'Greek (Modern)' },
  { code: 'enm', name: 'English (Middle)' },
  { code: 'epo', name: 'Esperanto' },
  { code: 'est', name: 'Estonian' },
  { code: 'eus', name: 'Basque' },
  { code: 'fao', name: 'Faroese' },
  { code: 'fas', name: 'Persian' },
  { code: 'fil', name: 'Filipino' },
  { code: 'fin', name: 'Finnish' },
  { code: 'fra', name: 'French' },
  { code: 'frm', name: 'French (Middle)' },
  { code: 'fry', name: 'West Frisian' },
  { code: 'gla', name: 'Scottish Gaelic' },
  { code: 'gle', name: 'Irish' },
  { code: 'glg', name: 'Galician' },
  { code: 'grc', name: 'Greek (Ancient)' },
  { code: 'guj', name: 'Gujarati' },
  { code: 'hat', name: 'Haitian Creole' },
  { code: 'heb', name: 'Hebrew' },
  { code: 'hin', name: 'Hindi' },
  { code: 'hrv', name: 'Croatian' },
  { code: 'hun', name: 'Hungarian' },
  { code: 'hye', name: 'Armenian' },
  { code: 'iku', name: 'Inuktitut' },
  { code: 'ind', name: 'Indonesian' },
  { code: 'isl', name: 'Icelandic' },
  { code: 'ita', name: 'Italian' },
  { code: 'ita_old', name: 'Italian (Old)' },
  { code: 'jav', name: 'Javanese' },
  { code: 'jpn', name: 'Japanese' },
  { code: 'jpn_vert', name: 'Japanese (Vertical)' },
  { code: 'kan', name: 'Kannada' },
  { code: 'kat', name: 'Georgian' },
  { code: 'kat_old', name: 'Georgian (Old)' },
  { code: 'kaz', name: 'Kazakh' },
  { code: 'khm', name: 'Central Khmer' },
  { code: 'kir', name: 'Kyrgyz' },
  { code: 'kmr', name: 'Kurdish (Kurmanji)' },
  { code: 'kor', name: 'Korean' },
  { code: 'kor_vert', name: 'Korean (Vertical)' },
  { code: 'lao', name: 'Lao' },
  { code: 'lat', name: 'Latin' },
  { code: 'lav', name: 'Latvian' },
  { code: 'lit', name: 'Lithuanian' },
  { code: 'ltz', name: 'Luxembourgish' },
  { code: 'mal', name: 'Malayalam' },
  { code: 'mar', name: 'Marathi' },
  { code: 'mkd', name: 'Macedonian' },
  { code: 'mlt', name: 'Maltese' },
  { code: 'mon', name: 'Mongolian' },
  { code: 'mri', name: 'Maori' },
  { code: 'msa', name: 'Malay' },
  { code: 'mya', name: 'Burmese' },
  { code: 'nep', name: 'Nepali' },
  { code: 'nld', name: 'Dutch / Flemish' },
  { code: 'nor', name: 'Norwegian' },
  { code: 'oci', name: 'Occitan' },
  { code: 'ori', name: 'Odia' },
  { code: 'pan', name: 'Punjabi' },
  { code: 'pol', name: 'Polish' },
  { code: 'por', name: 'Portuguese' },
  { code: 'pus', name: 'Pashto' },
  { code: 'que', name: 'Quechua' },
  { code: 'ron', name: 'Romanian' },
  { code: 'rus', name: 'Russian' },
  { code: 'san', name: 'Sanskrit' },
  { code: 'sin', name: 'Sinhala' },
  { code: 'slk', name: 'Slovak' },
  { code: 'slv', name: 'Slovenian' },
  { code: 'snd', name: 'Sindhi' },
  { code: 'spa', name: 'Spanish' },
  { code: 'spa_old', name: 'Spanish (Old)' },
  { code: 'sqi', name: 'Albanian' },
  { code: 'srp', name: 'Serbian' },
  { code: 'srp_latn', name: 'Serbian (Latin)' },
  { code: 'sun', name: 'Sundanese' },
  { code: 'swa', name: 'Swahili' },
  { code: 'swe', name: 'Swedish' },
  { code: 'syr', name: 'Syriac' },
  { code: 'tam', name: 'Tamil' },
  { code: 'tat', name: 'Tatar' },
  { code: 'tel', name: 'Telugu' },
  { code: 'tgk', name: 'Tajik' },
  { code: 'tha', name: 'Thai' },
  { code: 'tir', name: 'Tigrinya' },
  { code: 'ton', name: 'Tonga' },
  { code: 'tur', name: 'Turkish' },
  { code: 'uig', name: 'Uyghur' },
  { code: 'ukr', name: 'Ukrainian' },
  { code: 'urd', name: 'Urdu' },
  { code: 'uzb', name: 'Uzbek' },
  { code: 'uzb_cyrl', name: 'Uzbek (Cyrillic)' },
  { code: 'vie', name: 'Vietnamese' },
  { code: 'yid', name: 'Yiddish' },
  { code: 'yor', name: 'Yoruba' },
] as const

interface Config {
  shortcut: string
  newShortcut: string
  copyShortcut: string
  alwaysOnTop: boolean
  indentType: 'space' | 'tab'
  indentSize: number
  showWhitespace: boolean
  showInMenuBar: boolean
  ocrLanguages: string[]
  historyLimit: number
}

interface HistoryEntry {
  id: string
  text: string
  createdAt: string
}

function defaultConfig(): Config {
  return {
    shortcut: `${defaultMod}+J`,
    newShortcut: defaultNewShortcut,
    copyShortcut: defaultCopyShortcut,
    alwaysOnTop: false,
    indentType: 'space',
    indentSize: 2,
    showWhitespace: false,
    showInMenuBar: false,
    ocrLanguages: ['eng'],
    historyLimit: 10,
  }
}

function loadConfig(): Config {
  try {
    if (fs.existsSync(configPath)) {
      const saved = JSON.parse(fs.readFileSync(configPath, 'utf-8'))
      return {
        shortcut: saved.shortcut || `${defaultMod}+J`,
        newShortcut: typeof saved.newShortcut === 'string' && shortcutValidator.test(saved.newShortcut)
          ? saved.newShortcut
          : defaultNewShortcut,
        copyShortcut: typeof saved.copyShortcut === 'string' && shortcutValidator.test(saved.copyShortcut)
          ? saved.copyShortcut
          : defaultCopyShortcut,
        alwaysOnTop: saved.alwaysOnTop === true,
        indentType: saved.indentType === 'tab' ? 'tab' : 'space',
        indentSize: [2, 4, 6, 8].includes(Number(saved.indentSize)) ? Number(saved.indentSize) : 2,
        showWhitespace: saved.showWhitespace === true,
        showInMenuBar: saved.showInMenuBar === true,
        ocrLanguages: Array.isArray(saved.ocrLanguages) && saved.ocrLanguages.length
          ? saved.ocrLanguages.filter((code: unknown) => typeof code === 'string')
          : ['eng'],
        historyLimit: Number.isInteger(saved.historyLimit) && saved.historyLimit >= 1 && saved.historyLimit <= 1000
          ? saved.historyLimit
          : 10,
      }
    }
  } catch {}
  return defaultConfig()
}

function saveConfig(config: Config) {
  fs.writeFileSync(configPath, JSON.stringify(config, null, 2))
}

function loadHistory(): HistoryEntry[] {
  try {
    if (fs.existsSync(historyPath)) {
      return JSON.parse(fs.readFileSync(historyPath, 'utf-8'))
    }
  } catch {}
  return []
}

function saveHistory(history: HistoryEntry[]) {
  fs.writeFileSync(historyPath, JSON.stringify(history, null, 2))
}

function applyHistoryLimit(history: HistoryEntry[]) {
  return history.slice(0, loadConfig().historyLimit)
}

function loadLimitedHistory() {
  const history = loadHistory()
  const limitedHistory = applyHistoryLimit(history)
  if (limitedHistory.length !== history.length) saveHistory(limitedHistory)
  return limitedHistory
}

function getOcrLanguageDir() {
  return path.join(app.getPath('userData'), 'ocr-languages')
}

function ensureBundledEnglishAvailable() {
  const languageDir = getOcrLanguageDir()
  fs.mkdirSync(languageDir, { recursive: true })
  const destination = path.join(languageDir, 'eng.traineddata.gz')
  if (!fs.existsSync(destination)) {
    const appPath = app.getAppPath()
    const readableAppPath = app.isPackaged ? `${appPath}.unpacked` : appPath
    const source = path.join(readableAppPath, 'node_modules', '@tesseract.js-data', 'eng', '4.0.0_best_int', 'eng.traineddata.gz')
    fs.copyFileSync(source, destination)
  }
  return languageDir
}

function getOcrLanguageState() {
  const languageDir = ensureBundledEnglishAvailable()
  const selected = new Set(loadConfig().ocrLanguages)
  return OCR_LANGUAGES.map((language) => {
    const filePath = path.join(languageDir, `${language.code}.traineddata.gz`)
    const installed = fs.existsSync(filePath)
    return {
      ...language,
      installed,
      selected: installed && selected.has(language.code),
      sizeBytes: installed ? fs.statSync(filePath).size : null,
    }
  })
}

async function resetOcrWorker() {
  const worker = ocrWorker
  ocrWorker = null
  await worker?.terminate()
}

function createWindow(config: Config) {
  win = new BrowserWindow({
    width: 700,
    height: 500,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
    titleBarStyle: 'hiddenInset',
    trafficLightPosition: { x: 15, y: 15 },
    frame: false,
    show: false,
    skipTaskbar: false,
    alwaysOnTop: config.alwaysOnTop,
  })

  win.on('ready-to-show', () => {
    win?.show()
    win?.focus()
  })


  win.on('close', (event) => {
    if (!isQuitting && (process.platform === 'win32' || menuBarEnabled)) {
      event.preventDefault()
      copyText()
      win?.hide()
      return
    }
    copyText()
    if (!isQuitting) saveCurrentTextToHistory()
  })

  win.on('closed', () => {
    win = null
  })

  if (VITE_DEV_SERVER_URL) {
    win.loadURL(VITE_DEV_SERVER_URL)
  } else {
    win.loadFile(path.join(process.env.DIST!, 'index.html'))
  }
}

function showWindow() {
  if (!win) {
    createWindow(loadConfig())
    return
  }
  if (win.isMinimized()) win.restore()
  win.show()
  win.focus()
}

function toggleWindow() {
  if (!win) {
    createWindow(loadConfig())
    return
  }
  if (win.isMinimized()) {
    win.restore()
    win.show()
    win.focus()
    return
  }
  if (win.isVisible()) {
    copyText()
    if (process.platform === 'darwin') {
      app.hide()
    } else {
      win.hide()
    }
  } else {
    showWindow()
  }
}

function copyText() {
  if (currentText.trim()) {
    clipboard.writeText(currentText)
  }
}

function saveCurrentTextToHistory() {
  if (!currentText.trim()) return
  const history = loadHistory()
  const entry: HistoryEntry = {
    id: Date.now().toString(),
    text: currentText,
    createdAt: new Date().toISOString(),
  }
  history.unshift(entry)
  saveHistory(applyHistoryLimit(history))
}

function registerShortcut(config: Config) {
  globalShortcut.unregisterAll()
  try {
    globalShortcut.register(config.shortcut, toggleWindow)
  } catch {
    globalShortcut.register(`${defaultMod}+J`, toggleWindow)
  }
}

function createTray() {
  if (tray) return
  let icon
  if (process.platform === 'darwin') {
    // A monochrome template lets macOS adapt the icon to light/dark menu bars.
    icon = nativeImage.createFromDataURL('data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACQAAAAkCAYAAADhAJiYAAAAVUlEQVR4nO2TyQkAMAzDsv/S7QiF1iZHJchX6OFEAHhZppsXpIIgu4+gG8HLR/0RlOojyC5Q+1qMulxQqo8gu0DtazHqckGpPoLsArWvbJD65gQBnNj8Hv8BB9uGHwAAAABJRU5ErkJggg==').resize({ width: 18, height: 18 })
    icon.setTemplateImage(true)
  } else {
    const iconPath = app.isPackaged
      ? path.join(process.resourcesPath, 'icon.ico')
      : path.join(__dirname, '../build/icon.ico')
    icon = nativeImage.createFromPath(iconPath)
  }
  tray = new Tray(icon)
  tray.setToolTip('eScratch')
  tray.setContextMenu(Menu.buildFromTemplate([
    { label: 'Show / Hide Editor', click: toggleWindow },
    { type: 'separator' },
    { label: 'Quit eScratch', click: () => {
      isQuitting = true
      app.quit()
    } },
  ]))
  tray.on('double-click', showWindow)
  if (process.platform === 'darwin') app.dock.hide()
}

function applyMenuBarPreference(enabled: boolean) {
  menuBarEnabled = process.platform === 'darwin' && enabled
  if (process.platform !== 'darwin') return
  if (menuBarEnabled) {
    createTray()
  } else {
    tray?.destroy()
    tray = null
    app.dock.show()
  }
}

app.on('before-quit', () => {
  isQuitting = true
})

app.whenReady().then(() => {
  const config = loadConfig()
  applyMenuBarPreference(config.showInMenuBar)
  if (process.platform === 'win32') createTray()
  createWindow(config)
  registerShortcut(config)

  // IPC handlers
  ipcMain.handle('get-history', () => {
    return loadLimitedHistory()
  })

  ipcMain.handle('save-to-history', (_event, text: string) => {
    if (!text.trim()) return loadHistory()
    const history = loadHistory()
    const entry: HistoryEntry = {
      id: Date.now().toString(),
      text,
      createdAt: new Date().toISOString(),
    }
    history.unshift(entry)
    const limitedHistory = applyHistoryLimit(history)
    saveHistory(limitedHistory)
    return limitedHistory
  })

  ipcMain.handle('delete-history-entry', (_event, id: string) => {
    let history = loadHistory()
    history = history.filter(h => h.id !== id)
    saveHistory(history)
    return history
  })

  ipcMain.handle('clear-history', () => {
    saveHistory([])
    return []
  })

  ipcMain.handle('set-history-limit', (_event, requestedLimit: number) => {
    const historyLimit = Math.max(1, Math.min(1000, Math.round(Number(requestedLimit) || 10)))
    const config = loadConfig()
    config.historyLimit = historyLimit
    saveConfig(config)
    const history = loadHistory().slice(0, historyLimit)
    saveHistory(history)
    return { historyLimit, history }
  })

  ipcMain.handle('copy-to-clipboard', (_event, text: string) => {
    clipboard.writeText(text)
  })

  ipcMain.handle('sync-text', (_event, text: string) => {
    currentText = text
  })

  ipcMain.handle('close-window', () => {
    win?.close()
  })

  ipcMain.handle('recognize-image', async (_event, imageBytes: Uint8Array) => {
    if (!imageBytes?.byteLength) throw new Error('The pasted image is empty.')
    if (!ocrWorker) {
      const appPath = app.getAppPath()
      const readableAppPath = app.isPackaged ? `${appPath}.unpacked` : appPath
      const languageDir = ensureBundledEnglishAvailable()
      const installedCodes = new Set(getOcrLanguageState().filter((language) => language.installed).map((language) => language.code))
      const selectedLanguages = loadConfig().ocrLanguages.filter((code) => installedCodes.has(code as typeof OCR_LANGUAGES[number]['code']))
      const languages = selectedLanguages.length ? selectedLanguages : ['eng']
      ocrWorker = await createWorker(languages, 1, {
        langPath: languageDir,
        workerPath: path.join(readableAppPath, 'node_modules', 'tesseract.js', 'src', 'worker-script', 'node', 'index.js'),
        corePath: path.join(readableAppPath, 'node_modules', 'tesseract.js-core'),
        cachePath: path.join(app.getPath('userData'), 'ocr-cache'),
      })
    }
    const result = await ocrWorker.recognize(Buffer.from(imageBytes))
    return result.data.text.trim()
  })

  ipcMain.handle('get-ocr-languages', () => getOcrLanguageState())

  ipcMain.handle('download-ocr-language', async (event, code: string) => {
    const language = OCR_LANGUAGES.find((item) => item.code === code)
    if (!language) throw new Error('Unsupported OCR language.')
    if (code === 'eng') return getOcrLanguageState()

    const languageDir = ensureBundledEnglishAvailable()
    const destination = path.join(languageDir, `${code}.traineddata.gz`)
    const temporary = `${destination}.download`
    const url = `https://cdn.jsdelivr.net/npm/@tesseract.js-data/${code}@1.0.0/4.0.0_best_int/${code}.traineddata.gz`

    try {
      const response = await net.fetch(url)
      if (!response.ok || !response.body) throw new Error(`Download failed (${response.status}).`)
      const totalBytes = Number(response.headers.get('content-length')) || null
      const reader = response.body.getReader()
      const chunks: Uint8Array[] = []
      let receivedBytes = 0
      while (true) {
        const { done, value } = await reader.read()
        if (done) break
        chunks.push(value)
        receivedBytes += value.byteLength
        event.sender.send('ocr-download-progress', { code, receivedBytes, totalBytes })
      }
      const data = Buffer.concat(chunks.map((chunk) => Buffer.from(chunk)))
      if (data.length < 2 || data[0] !== 0x1f || data[1] !== 0x8b) {
        throw new Error('The downloaded language file is invalid.')
      }
      fs.writeFileSync(temporary, data)
      fs.renameSync(temporary, destination)
      event.sender.send('ocr-download-progress', { code, receivedBytes: data.length, totalBytes: data.length })
      return getOcrLanguageState()
    } catch (error) {
      if (fs.existsSync(temporary)) fs.unlinkSync(temporary)
      throw error
    }
  })

  ipcMain.handle('remove-ocr-language', async (_event, code: string) => {
    if (code === 'eng') throw new Error('English is included with the app and cannot be removed.')
    if (!OCR_LANGUAGES.some((item) => item.code === code)) throw new Error('Unsupported OCR language.')
    await resetOcrWorker()
    const filePath = path.join(ensureBundledEnglishAvailable(), `${code}.traineddata.gz`)
    if (fs.existsSync(filePath)) fs.unlinkSync(filePath)
    const config = loadConfig()
    config.ocrLanguages = config.ocrLanguages.filter((languageCode) => languageCode !== code)
    if (!config.ocrLanguages.length) config.ocrLanguages = ['eng']
    saveConfig(config)
    return getOcrLanguageState()
  })

  ipcMain.handle('set-ocr-languages', async (_event, codes: string[]) => {
    const installedCodes = new Set(getOcrLanguageState().filter((language) => language.installed).map((language) => language.code))
    const selected = Array.isArray(codes)
      ? [...new Set(codes)].filter((code) => installedCodes.has(code as typeof OCR_LANGUAGES[number]['code']))
      : []
    if (!selected.length) throw new Error('Select at least one installed OCR language.')
    const config = loadConfig()
    config.ocrLanguages = selected
    saveConfig(config)
    await resetOcrWorker()
    return getOcrLanguageState()
  })

  ipcMain.handle('get-config', () => {
    return loadConfig()
  })

  ipcMain.handle('restore-defaults', async () => {
    const previous = loadConfig()
    const config = defaultConfig()
    // Reserve the default before releasing the user's working shortcut.
    if (!globalShortcut.isRegistered(config.shortcut) && !globalShortcut.register(config.shortcut, toggleWindow)) {
      throw new Error('The default shortcut is in use by another app. Close that app and retry.')
    }
    try {
      await resetOcrWorker()
      saveConfig(config)
    } catch (error) {
      if (previous.shortcut !== config.shortcut) globalShortcut.unregister(config.shortcut)
      throw error
    }
    if (previous.shortcut !== config.shortcut) globalShortcut.unregister(previous.shortcut)
    win?.setAlwaysOnTop(config.alwaysOnTop)
    applyMenuBarPreference(config.showInMenuBar)
    const history = loadLimitedHistory()
    return { config, history }
  })

  ipcMain.handle('set-shortcut', (_event, shortcut: string) => {
    if (!shortcutValidator.test(shortcut)) return false
    const config = loadConfig()
    config.shortcut = shortcut
    saveConfig(config)
    registerShortcut(config)
    return true
  })

  ipcMain.handle('set-local-shortcut', (_event, name: string, shortcut: string) => {
    if (name !== 'new' && name !== 'copy') return false
    if (!shortcutValidator.test(shortcut)) return false
    const config = loadConfig()
    if (name === 'new') {
      config.newShortcut = shortcut
    } else {
      config.copyShortcut = shortcut
    }
    saveConfig(config)
    return true
  })

  ipcMain.handle('set-always-on-top', (_event, alwaysOnTop: boolean) => {
    const config = loadConfig()
    config.alwaysOnTop = alwaysOnTop === true
    saveConfig(config)
    win?.setAlwaysOnTop(config.alwaysOnTop)
    return config.alwaysOnTop
  })

  ipcMain.handle('set-indent', (_event, indentType: string, indentSize: number) => {
    const config = loadConfig()
    config.indentType = indentType === 'tab' ? 'tab' : 'space'
    config.indentSize = [2, 4, 6, 8].includes(indentSize) ? indentSize : 2
    saveConfig(config)
  })

  ipcMain.handle('set-show-whitespace', (_event, showWhitespace: boolean) => {
    const config = loadConfig()
    config.showWhitespace = showWhitespace === true
    saveConfig(config)
    return config.showWhitespace
  })

  ipcMain.handle('set-show-in-menu-bar', (_event, showInMenuBar: boolean) => {
    const config = loadConfig()
    config.showInMenuBar = showInMenuBar === true
    saveConfig(config)
    applyMenuBarPreference(config.showInMenuBar)
    return config.showInMenuBar
  })
})

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin' && process.platform !== 'win32') {
    app.quit()
  }
})

app.on('activate', () => {
  if (!win) {
    createWindow(loadConfig())
  } else {
    win.show()
    win.focus()
  }
})

app.on('will-quit', async () => {
  saveCurrentTextToHistory()
  globalShortcut.unregisterAll()
  await ocrWorker?.terminate()
  ocrWorker = null
  tray?.destroy()
  tray = null
})
