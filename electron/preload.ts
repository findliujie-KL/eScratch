import { contextBridge, ipcRenderer } from 'electron'

contextBridge.exposeInMainWorld('electronAPI', {
  readMarkdownClipboard: () => ipcRenderer.invoke('read-markdown-clipboard'),
  editorPaste: () => ipcRenderer.invoke('editor-paste'),
  setStartAtLogin: (enabled: boolean) => ipcRenderer.invoke('set-start-at-login', enabled),
  onResumeEntry: (callback: () => void) => {
    const listener = () => callback()
    ipcRenderer.on('resume-entry', listener)
    return () => ipcRenderer.removeListener('resume-entry', listener)
  },
  getHistory: () => ipcRenderer.invoke('get-history'),
  saveToHistory: (text: string) => ipcRenderer.invoke('save-to-history', text),
  deleteHistoryEntry: (id: string) => ipcRenderer.invoke('delete-history-entry', id),
  clearHistory: () => ipcRenderer.invoke('clear-history'),
  setHistoryLimit: (limit: number) => ipcRenderer.invoke('set-history-limit', limit),
  copyToClipboard: (text: string) => ipcRenderer.invoke('copy-to-clipboard', text),
  syncText: (text: string) => ipcRenderer.invoke('sync-text', text),
  closeWindow: () => ipcRenderer.invoke('close-window'),
  recognizeImage: (imageBytes: Uint8Array) => ipcRenderer.invoke('recognize-image', imageBytes),
  getOcrLanguages: () => ipcRenderer.invoke('get-ocr-languages'),
  downloadOcrLanguage: (code: string) => ipcRenderer.invoke('download-ocr-language', code),
  removeOcrLanguage: (code: string) => ipcRenderer.invoke('remove-ocr-language', code),
  setOcrLanguages: (codes: string[]) => ipcRenderer.invoke('set-ocr-languages', codes),
  onOcrDownloadProgress: (callback: (progress: { code: string; receivedBytes: number; totalBytes: number | null }) => void) => {
    const listener = (_event: Electron.IpcRendererEvent, progress: { code: string; receivedBytes: number; totalBytes: number | null }) => callback(progress)
    ipcRenderer.on('ocr-download-progress', listener)
    return () => ipcRenderer.removeListener('ocr-download-progress', listener)
  },
  getConfig: () => ipcRenderer.invoke('get-config'),
  restoreDefaults: () => ipcRenderer.invoke('restore-defaults'),
  setShortcut: (shortcut: string) => ipcRenderer.invoke('set-shortcut', shortcut),
  setLocalShortcut: (name: 'new' | 'copy' | 'markdown', shortcut: string) => ipcRenderer.invoke('set-local-shortcut', name, shortcut),
  setShowWordCount: (value: boolean) => ipcRenderer.invoke('set-show-word-count', value),
  setAlwaysOnTop: (alwaysOnTop: boolean) => ipcRenderer.invoke('set-always-on-top', alwaysOnTop),
  setIndent: (indentType: string, indentSize: number) => ipcRenderer.invoke('set-indent', indentType, indentSize),
  setShowWhitespace: (showWhitespace: boolean) => ipcRenderer.invoke('set-show-whitespace', showWhitespace),
  setShowInMenuBar: (showInMenuBar: boolean) => ipcRenderer.invoke('set-show-in-menu-bar', showInMenuBar),
})
