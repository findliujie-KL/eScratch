export interface HistoryEntry {
  id: string
  text: string
  createdAt: string
}

export interface AppConfig {
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

export interface OcrLanguage {
  code: string
  name: string
  installed: boolean
  selected: boolean
  sizeBytes: number | null
}

export interface OcrDownloadProgress {
  code: string
  receivedBytes: number
  totalBytes: number | null
}

export interface ElectronAPI {
  getHistory: () => Promise<HistoryEntry[]>
  saveToHistory: (text: string) => Promise<HistoryEntry[]>
  deleteHistoryEntry: (id: string) => Promise<HistoryEntry[]>
  clearHistory: () => Promise<HistoryEntry[]>
  setHistoryLimit: (limit: number) => Promise<{ historyLimit: number; history: HistoryEntry[] }>
  copyToClipboard: (text: string) => Promise<void>
  syncText: (text: string) => Promise<void>
  closeWindow: () => Promise<void>
  recognizeImage: (imageBytes: Uint8Array) => Promise<string>
  getOcrLanguages: () => Promise<OcrLanguage[]>
  downloadOcrLanguage: (code: string) => Promise<OcrLanguage[]>
  removeOcrLanguage: (code: string) => Promise<OcrLanguage[]>
  setOcrLanguages: (codes: string[]) => Promise<OcrLanguage[]>
  onOcrDownloadProgress: (callback: (progress: OcrDownloadProgress) => void) => () => void
  getConfig: () => Promise<AppConfig>
  restoreDefaults: () => Promise<{ config: AppConfig; history: HistoryEntry[] }>
  setShortcut: (shortcut: string) => Promise<boolean>
  setLocalShortcut: (name: 'new' | 'copy', shortcut: string) => Promise<boolean>
  setAlwaysOnTop: (alwaysOnTop: boolean) => Promise<boolean>
  setIndent: (indentType: string, indentSize: number) => Promise<void>
  setShowWhitespace: (showWhitespace: boolean) => Promise<boolean>
  setShowInMenuBar: (showInMenuBar: boolean) => Promise<boolean>
}

declare global {
  interface Window {
    electronAPI: ElectronAPI
  }
}
