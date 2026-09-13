import { useState, useEffect, useRef, useCallback } from 'react'
import type { HistoryEntry } from './types'

type ShortcutTarget = 'toggle' | 'new' | 'copy'

function matchShortcut(e: React.KeyboardEvent, shortcut: string): boolean {
  if (!shortcut) return false
  const parts = shortcut.split('+')
  if (parts.length < 2) return false
  const key = parts[parts.length - 1]
  const modifiers = parts.slice(0, -1)

  const expectMeta = modifiers.includes('Command') || modifiers.includes('Meta')
  const expectCtrl = modifiers.includes('Control')
  const expectAlt = modifiers.includes('Alt')
  const expectShift = modifiers.includes('Shift')

  if (!!e.metaKey !== expectMeta) return false
  if (!!e.ctrlKey !== expectCtrl) return false
  if (!!e.altKey !== expectAlt) return false
  if (!!e.shiftKey !== expectShift) return false

  const eventKey = e.key.length === 1 ? e.key.toUpperCase() : e.key
  return eventKey === key
}

function formatShortcut(s: string): string {
  if (!s) return ''
  if (typeof navigator !== 'undefined' && navigator.platform.includes('Mac')) {
    return s.replace(/Command/g, 'Cmd').replace(/Control/g, 'Ctrl')
  }
  return s.replace(/Control/g, 'Ctrl')
}

type WhitespaceToken =
  | { type: 'text'; text: string }
  | { type: 'space'; text: string }
  | { type: 'tab'; text: string }

function splitWhitespace(value: string): WhitespaceToken[] {
  const tokens: WhitespaceToken[] = []
  let buffer = ''
  let mode: 'text' | 'space' | 'tab' = 'text'

  const flush = () => {
    if (buffer) {
      tokens.push({ type: mode, text: buffer })
      buffer = ''
    }
  }

  for (const ch of value) {
    const next: 'text' | 'space' | 'tab' =
      ch === '\t' ? 'tab' : ch === ' ' || ch === '\u3000' ? 'space' : 'text'
    if (next !== mode) {
      flush()
      mode = next
    }
    buffer += ch
  }
  flush()
  return tokens
}

function App() {
  const [text, setText] = useState('')
  const [history, setHistory] = useState<HistoryEntry[]>([])
  const [showHistory, setShowHistory] = useState(false)
  const [showSettings, setShowSettings] = useState(false)
  const [toggleShortcut, setToggleShortcut] = useState('')
  const [toggleShortcutInput, setToggleShortcutInput] = useState('')
  const [newShortcut, setNewShortcut] = useState('')
  const [newShortcutInput, setNewShortcutInput] = useState('')
  const [copyShortcut, setCopyShortcut] = useState('')
  const [copyShortcutInput, setCopyShortcutInput] = useState('')
  const [alwaysOnTop, setAlwaysOnTop] = useState(false)
  const [recordingTarget, setRecordingTarget] = useState<ShortcutTarget | null>(null)
  const [copyFeedback, setCopyFeedback] = useState(false)
  const [indentType, setIndentType] = useState<'space' | 'tab'>('space')
  const [indentSize, setIndentSize] = useState(2)
  const [showWhitespace, setShowWhitespace] = useState(false)
  const [theme, setTheme] = useState<'dark' | 'light'>(() => {
    return (localStorage.getItem('theme') as 'dark' | 'light') || 'dark'
  })
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const overlayInnerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    document.documentElement.className = theme === 'light' ? 'light' : ''
    localStorage.setItem('theme', theme)
  }, [theme])

  useEffect(() => {
    window.electronAPI.getHistory().then(setHistory)
    window.electronAPI.getConfig().then((config) => {
      setToggleShortcut(config.shortcut)
      setToggleShortcutInput(config.shortcut)
      setNewShortcut(config.newShortcut)
      setNewShortcutInput(config.newShortcut)
      setCopyShortcut(config.copyShortcut)
      setCopyShortcutInput(config.copyShortcut)
      setAlwaysOnTop(config.alwaysOnTop)
      setIndentType(config.indentType)
      setIndentSize(config.indentSize)
      setShowWhitespace(config.showWhitespace)
    })
  }, [])

  // Sync text to main process for copy shortcut
  useEffect(() => {
    window.electronAPI.syncText(text)
  }, [text])

  useEffect(() => {
    if (!showHistory && !showSettings) {
      textareaRef.current?.focus()
    }
  }, [showHistory, showSettings])

  const syncOverlayScroll = useCallback(() => {
    const textarea = textareaRef.current
    const inner = overlayInnerRef.current
    if (!textarea || !inner) return
    inner.style.width = `${textarea.clientWidth}px`
    inner.style.transform = `translate(${-textarea.scrollLeft}px, ${-textarea.scrollTop}px)`
  }, [])

  useEffect(() => {
    if (!showWhitespace) return
    syncOverlayScroll()
    const textarea = textareaRef.current
    if (!textarea) return
    const observer = new ResizeObserver(() => syncOverlayScroll())
    observer.observe(textarea)
    return () => observer.disconnect()
  }, [showWhitespace, text, syncOverlayScroll])

  const saveCurrentText = useCallback(async () => {
    if (text.trim()) {
      const updated = await window.electronAPI.saveToHistory(text)
      setHistory(updated)
    }
  }, [text])

  const handleNew = useCallback(async () => {
    await saveCurrentText()
    setText('')
    textareaRef.current?.focus()
  }, [saveCurrentText])

  const handleCopy = useCallback(async () => {
    if (!text.trim()) return
    await window.electronAPI.copyToClipboard(text)
    setCopyFeedback(true)
    setTimeout(() => setCopyFeedback(false), 1500)
  }, [text])

  const handleSelectHistory = useCallback(async (entry: HistoryEntry) => {
    if (text.trim()) {
      const updated = await window.electronAPI.saveToHistory(text)
      setHistory(updated)
    }
    setText(entry.text)
    setShowHistory(false)
    textareaRef.current?.focus()
  }, [text])

  const handleDeleteHistory = useCallback(async (id: string, e: React.MouseEvent) => {
    e.stopPropagation()
    const updated = await window.electronAPI.deleteHistoryEntry(id)
    setHistory(updated)
  }, [])

  const handleShortcutKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (recordingTarget === null) return
    e.preventDefault()
    e.stopPropagation()

    const modifierKeys = ['Control', 'Alt', 'Shift', 'Meta']
    if (modifierKeys.includes(e.key)) return

    const parts: string[] = []
    if (e.metaKey) parts.push('Command')
    if (e.ctrlKey) parts.push('Control')
    if (e.altKey) parts.push('Alt')
    if (e.shiftKey) parts.push('Shift')

    const key = e.key.length === 1 ? e.key.toUpperCase() : e.key
    parts.push(key)
    const recorded = parts.join('+')

    if (recordingTarget === 'toggle') {
      setToggleShortcutInput(recorded)
    } else if (recordingTarget === 'new') {
      setNewShortcutInput(recorded)
    } else if (recordingTarget === 'copy') {
      setCopyShortcutInput(recorded)
    }
    setRecordingTarget(null)
  }, [recordingTarget])

  const handleSaveToggleShortcut = useCallback(async () => {
    if (toggleShortcutInput.trim()) {
      const success = await window.electronAPI.setShortcut(toggleShortcutInput)
      if (success) {
        setToggleShortcut(toggleShortcutInput)
      }
    }
  }, [toggleShortcutInput])

  const handleSaveNewShortcut = useCallback(async () => {
    if (newShortcutInput.trim()) {
      const success = await window.electronAPI.setLocalShortcut('new', newShortcutInput)
      if (success) {
        setNewShortcut(newShortcutInput)
      }
    }
  }, [newShortcutInput])

  const handleSaveCopyShortcut = useCallback(async () => {
    if (copyShortcutInput.trim()) {
      const success = await window.electronAPI.setLocalShortcut('copy', copyShortcutInput)
      if (success) {
        setCopyShortcut(copyShortcutInput)
      }
    }
  }, [copyShortcutInput])

  const handleAlwaysOnTopChange = useCallback(async (e: React.ChangeEvent<HTMLInputElement>) => {
    const next = e.target.checked
    const applied = await window.electronAPI.setAlwaysOnTop(next)
    setAlwaysOnTop(applied)
  }, [])

  const handleIndentTypeChange = useCallback(async (e: React.ChangeEvent<HTMLSelectElement>) => {
    const newType = e.target.value as 'space' | 'tab'
    setIndentType(newType)
    await window.electronAPI.setIndent(newType, indentSize)
  }, [indentSize])

  const handleIndentSizeChange = useCallback(async (e: React.ChangeEvent<HTMLSelectElement>) => {
    const newSize = Number(e.target.value)
    setIndentSize(newSize)
    await window.electronAPI.setIndent(indentType, newSize)
  }, [indentType])

  const handleShowWhitespaceChange = useCallback(async (e: React.ChangeEvent<HTMLInputElement>) => {
    const next = e.target.checked
    const applied = await window.electronAPI.setShowWhitespace(next)
    setShowWhitespace(applied)
  }, [])

  const handleTabKey = useCallback((e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key !== 'Tab' || e.nativeEvent.isComposing) return
    e.preventDefault()
    const textarea = e.currentTarget
    const start = textarea.selectionStart
    const end = textarea.selectionEnd
    const value = textarea.value

    if (e.shiftKey) {
      // Shift+Tab: remove indent from beginning of current line
      const lineStart = value.lastIndexOf('\n', start - 1) + 1
      const linePrefix = value.slice(lineStart, start)
      let removeCount = 0
      if (indentType === 'tab' && linePrefix.startsWith('\t')) {
        removeCount = 1
      } else {
        const match = linePrefix.match(/^ +/)
        if (match) {
          removeCount = Math.min(match[0].length, indentSize)
        }
      }
      if (removeCount > 0) {
        const newValue = value.slice(0, lineStart) + value.slice(lineStart + removeCount)
        setText(newValue)
        const newPos = Math.max(lineStart, start - removeCount)
        requestAnimationFrame(() => {
          textarea.selectionStart = newPos
          textarea.selectionEnd = Math.max(lineStart, end - removeCount)
        })
      }
    } else {
      // Tab: insert indent
      const indent = indentType === 'tab' ? '\t' : ' '.repeat(indentSize)
      const newValue = value.slice(0, start) + indent + value.slice(end)
      setText(newValue)
      const newPos = start + indent.length
      requestAnimationFrame(() => {
        textarea.selectionStart = newPos
        textarea.selectionEnd = newPos
      })
    }
  }, [indentType, indentSize])

  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.nativeEvent.isComposing) return
    if (matchShortcut(e, newShortcut)) {
      e.preventDefault()
      handleNew()
      return
    }
    if (matchShortcut(e, copyShortcut)) {
      e.preventDefault()
      handleCopy()
      return
    }
    if (e.key === 's' && (e.metaKey || e.ctrlKey) && !e.shiftKey && !e.altKey) {
      e.preventDefault()
      saveCurrentText()
      return
    }
    if (e.key === 'Escape') {
      if (showHistory) {
        setShowHistory(false)
      } else if (showSettings) {
        setShowSettings(false)
      }
    }
  }, [showHistory, showSettings, saveCurrentText, handleNew, handleCopy, newShortcut, copyShortcut])

  const formatDate = (iso: string) => {
    const d = new Date(iso)
    const month = d.getMonth() + 1
    const day = d.getDate()
    const hours = d.getHours().toString().padStart(2, '0')
    const minutes = d.getMinutes().toString().padStart(2, '0')
    return `${month}/${day} ${hours}:${minutes}`
  }

  const truncate = (s: string, len: number) => {
    const line = s.split('\n')[0]
    return line.length > len ? line.slice(0, len) + '...' : line
  }

  return (
    <div className="app" onKeyDown={handleKeyDown}>
      {/* Titlebar (drag region) */}
      <div className="titlebar">
        <span className="titlebar-text">One-Time Editor</span>
        <div className="titlebar-buttons">
          <button
            className="btn btn-new"
            onClick={handleNew}
            title={newShortcut ? `New (${formatShortcut(newShortcut)})` : 'New'}
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
              <polyline points="14 2 14 8 20 8" />
              <line x1="12" y1="18" x2="12" y2="12" />
              <line x1="9" y1="15" x2="15" y2="15" />
            </svg>
          </button>
          <button
            className={`btn btn-copy ${copyFeedback ? 'copied' : ''}`}
            onClick={handleCopy}
            title={copyShortcut ? `Copy (${formatShortcut(copyShortcut)})` : 'Copy'}
          >
            {copyFeedback ? (
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <polyline points="20 6 9 17 4 12" />
              </svg>
            ) : (
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <rect x="9" y="9" width="13" height="13" rx="2" ry="2" />
                <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
              </svg>
            )}
          </button>
          <button
            className={`btn btn-history ${showHistory ? 'active' : ''}`}
            onClick={() => { setShowHistory(!showHistory); setShowSettings(false) }}
            title="History"
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="12" cy="12" r="10" />
              <polyline points="12 6 12 12 16 14" />
            </svg>
          </button>
          <button
            className={`btn btn-settings ${showSettings ? 'active' : ''}`}
            onClick={() => { setShowSettings(!showSettings); setShowHistory(false) }}
            title="Settings"
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="12" cy="12" r="3" />
              <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z" />
            </svg>
          </button>
          <button
            className="btn btn-theme"
            onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}
            title="Toggle theme"
          >
            {theme === 'dark' ? (
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <circle cx="12" cy="12" r="5" />
                <line x1="12" y1="1" x2="12" y2="3" />
                <line x1="12" y1="21" x2="12" y2="23" />
                <line x1="4.22" y1="4.22" x2="5.64" y2="5.64" />
                <line x1="18.36" y1="18.36" x2="19.78" y2="19.78" />
                <line x1="1" y1="12" x2="3" y2="12" />
                <line x1="21" y1="12" x2="23" y2="12" />
                <line x1="4.22" y1="19.78" x2="5.64" y2="18.36" />
                <line x1="18.36" y1="5.64" x2="19.78" y2="4.22" />
              </svg>
            ) : (
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
              </svg>
            )}
          </button>
        </div>
      </div>

      {/* Main area */}
      <div className="main-area">
        {(showSettings || showHistory) && (
          <div className="panel-backdrop" onClick={() => { setShowSettings(false); setShowHistory(false) }} />
        )}
        <div className="editor-wrap">
          <textarea
            ref={textareaRef}
            className="editor"
            style={{ tabSize: indentSize }}
            value={text}
            onChange={(e) => setText(e.target.value)}
            onKeyDown={handleTabKey}
            onScroll={syncOverlayScroll}
            placeholder="Type here..."
            spellCheck={false}
            autoFocus
          />
          {showWhitespace && (
            <div className="editor-overlay" aria-hidden="true">
              <div
                ref={overlayInnerRef}
                className="editor-overlay-inner"
                style={{ tabSize: indentSize }}
              >
                {splitWhitespace(text).map((token, index) => {
                  if (token.type === 'space') {
                    return (
                      <span key={index} className="ws-space">
                        {token.text}
                      </span>
                    )
                  }
                  if (token.type === 'tab') {
                    return (
                      <span key={index} className="ws-tab">
                        {token.text}
                      </span>
                    )
                  }
                  return <span key={index}>{token.text}</span>
                })}
              </div>
            </div>
          )}
        </div>

        {/* History panel */}
        {showHistory && (
          <div className="panel history-panel">
            <div className="panel-header">
              <h3>History</h3>
            </div>
            <div className="panel-content">
              {history.length === 0 ? (
                <div className="empty-message">No history</div>
              ) : (
                history.map((entry) => (
                  <div
                    key={entry.id}
                    className="history-item"
                    onClick={() => handleSelectHistory(entry)}
                  >
                    <div className="history-item-text">
                      {truncate(entry.text, 60)}
                    </div>
                    <div className="history-item-footer">
                      <span className="history-item-date">
                        {formatDate(entry.createdAt)}
                      </span>
                      <button
                        className="history-delete-btn"
                        onClick={(e) => handleDeleteHistory(entry.id, e)}
                        title="Delete"
                      >
                        &times;
                      </button>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        )}

        {/* Settings panel */}
        {showSettings && (
          <div className="panel settings-panel">
            <div className="panel-header">
              <h3>Settings</h3>
            </div>
            <div className="panel-content">
              <div className="settings-item">
                <div className="settings-row">
                  <div>
                    <div className="settings-label">Always on Top</div>
                    <div className="setting-description">
                      Keep this window above other applications.
                    </div>
                  </div>
                  <label className="switch" htmlFor="always-on-top-toggle">
                    <input
                      id="always-on-top-toggle"
                      type="checkbox"
                      checked={alwaysOnTop}
                      onChange={handleAlwaysOnTopChange}
                    />
                    <span className="switch-slider" />
                  </label>
                </div>
              </div>
              <div className="settings-item">
                <div className="settings-label">Indent</div>
                <div className="settings-row" style={{ marginBottom: 8 }}>
                  <span className="setting-description">Type</span>
                  <select
                    className="settings-select"
                    value={indentType}
                    onChange={handleIndentTypeChange}
                  >
                    <option value="space">Spaces</option>
                    <option value="tab">Tab</option>
                  </select>
                </div>
                <div className="settings-row">
                  <span className="setting-description">Size</span>
                  <select
                    className="settings-select"
                    value={indentSize}
                    onChange={handleIndentSizeChange}
                  >
                    <option value={2}>2</option>
                    <option value={4}>4</option>
                    <option value={6}>6</option>
                    <option value={8}>8</option>
                  </select>
                </div>
              </div>
              <div className="settings-item">
                <div className="settings-row">
                  <div>
                    <div className="settings-label">Show Whitespace</div>
                    <div className="setting-description">
                      Display spaces, tabs, and full-width spaces as markers.
                    </div>
                  </div>
                  <label className="switch" htmlFor="show-whitespace-toggle">
                    <input
                      id="show-whitespace-toggle"
                      type="checkbox"
                      checked={showWhitespace}
                      onChange={handleShowWhitespaceChange}
                    />
                    <span className="switch-slider" />
                  </label>
                </div>
              </div>
              <div className="settings-item">
                <div className="settings-label">Toggle Window</div>
                <div className="shortcut-current">
                  Current: <code>{toggleShortcut}</code>
                </div>
                <input
                  type="text"
                  className={`shortcut-input ${recordingTarget === 'toggle' ? 'recording' : ''}`}
                  value={recordingTarget === 'toggle' ? 'Press keys...' : toggleShortcutInput}
                  onKeyDown={handleShortcutKeyDown}
                  onFocus={() => setRecordingTarget('toggle')}
                  onBlur={() => setRecordingTarget((t) => (t === 'toggle' ? null : t))}
                  readOnly
                  placeholder="Click to record shortcut"
                />
                <button className="btn-save" onClick={handleSaveToggleShortcut}>
                  Save
                </button>
                <div className="shortcut-hint">
                  Works globally. Hiding the window also copies the editor text to clipboard.
                </div>
              </div>
              <div className="settings-item">
                <div className="settings-label">New</div>
                <div className="shortcut-current">
                  Current: <code>{newShortcut}</code>
                </div>
                <input
                  type="text"
                  className={`shortcut-input ${recordingTarget === 'new' ? 'recording' : ''}`}
                  value={recordingTarget === 'new' ? 'Press keys...' : newShortcutInput}
                  onKeyDown={handleShortcutKeyDown}
                  onFocus={() => setRecordingTarget('new')}
                  onBlur={() => setRecordingTarget((t) => (t === 'new' ? null : t))}
                  readOnly
                  placeholder="Click to record shortcut"
                />
                <button className="btn-save" onClick={handleSaveNewShortcut}>
                  Save
                </button>
                <div className="shortcut-hint">
                  Active only when this window has focus.
                </div>
              </div>
              <div className="settings-item">
                <div className="settings-label">Copy</div>
                <div className="shortcut-current">
                  Current: <code>{copyShortcut}</code>
                </div>
                <input
                  type="text"
                  className={`shortcut-input ${recordingTarget === 'copy' ? 'recording' : ''}`}
                  value={recordingTarget === 'copy' ? 'Press keys...' : copyShortcutInput}
                  onKeyDown={handleShortcutKeyDown}
                  onFocus={() => setRecordingTarget('copy')}
                  onBlur={() => setRecordingTarget((t) => (t === 'copy' ? null : t))}
                  readOnly
                  placeholder="Click to record shortcut"
                />
                <button className="btn-save" onClick={handleSaveCopyShortcut}>
                  Save
                </button>
                <div className="shortcut-hint">
                  Active only when this window has focus.
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

export default App
