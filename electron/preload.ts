import { contextBridge, ipcRenderer } from 'electron'

contextBridge.exposeInMainWorld('electronAPI', {
  getHistory: () => ipcRenderer.invoke('get-history'),
  saveToHistory: (text: string) => ipcRenderer.invoke('save-to-history', text),
  deleteHistoryEntry: (id: string) => ipcRenderer.invoke('delete-history-entry', id),
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
  setShortcut: (shortcut: string) => ipcRenderer.invoke('set-shortcut', shortcut),
  setLocalShortcut: (name: 'new' | 'copy', shortcut: string) => ipcRenderer.invoke('set-local-shortcut', name, shortcut),
  setAlwaysOnTop: (alwaysOnTop: boolean) => ipcRenderer.invoke('set-always-on-top', alwaysOnTop),
  setIndent: (indentType: string, indentSize: number) => ipcRenderer.invoke('set-indent', indentType, indentSize),
  setShowWhitespace: (showWhitespace: boolean) => ipcRenderer.invoke('set-show-whitespace', showWhitespace),
  setShowInMenuBar: (showInMenuBar: boolean) => ipcRenderer.invoke('set-show-in-menu-bar', showInMenuBar),
})
