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
}

export interface ElectronAPI {
  getHistory: () => Promise<HistoryEntry[]>
  saveToHistory: (text: string) => Promise<HistoryEntry[]>
  deleteHistoryEntry: (id: string) => Promise<HistoryEntry[]>
  copyToClipboard: (text: string) => Promise<void>
  syncText: (text: string) => Promise<void>
  getConfig: () => Promise<AppConfig>
  setShortcut: (shortcut: string) => Promise<boolean>
  setLocalShortcut: (name: 'new' | 'copy', shortcut: string) => Promise<boolean>
  setAlwaysOnTop: (alwaysOnTop: boolean) => Promise<boolean>
  setIndent: (indentType: string, indentSize: number) => Promise<void>
  setShowWhitespace: (showWhitespace: boolean) => Promise<boolean>
}

declare global {
  interface Window {
    electronAPI: ElectronAPI
  }
}
