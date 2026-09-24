function recoverWords(original: string, final: string): Map<number, string> {
  const result = new Map<number, string>()
  const a = [...original.matchAll(/\S+/g)], b = [...final.matchAll(/\S+/g)]
  if (!b.length) return result
  const positions: number[] = []
  let cursor = 0
  for (const word of b) {
    while (cursor < a.length && a[cursor][0] !== word[0]) cursor++
    if (cursor === a.length) return result
    positions.push(cursor++)
  }
  cursor = a.length - 1
  for (let i = b.length - 1; i >= 0; i--) {
    while (cursor >= 0 && a[cursor][0] !== b[i][0]) cursor--
    if (cursor !== positions[i]) return result
    cursor--
  }
  let previous = -1
  b.forEach((word, i) => {
    if (positions[i] > previous + 1) result.set(word.index!, original.slice(a[previous + 1].index!, a[positions[i]].index!))
    previous = positions[i]
  })
  if (previous < a.length - 1) result.set(final.length, original.slice(a[previous].index! + a[previous][0].length))
  return result
}

type Part = { text: string; markdown: string }
const escape = (text: string) => text.replace(/([\\`*_{}\[\]<>#~|])/g, '\\$1')
const normalize = (text: string) => text.replace(/\s+/g, ' ').trim()

export function recoverDeletions(original: string, final: string): Map<number, string> {
  const result = new Map<number, string>()
  if (!final || original.length <= final.length) return result
  const positions: number[] = []
  let cursor = 0
  for (let i = 0; i < final.length; i++) {
    cursor = original.indexOf(final[i], cursor)
    if (cursor < 0) return result
    positions.push(cursor++)
  }
  cursor = original.length - 1
  for (let i = final.length - 1; i >= 0; i--) {
    cursor = original.lastIndexOf(final[i], cursor)
    if (cursor !== positions[i]) return recoverWords(original, final)
    cursor--
  }
  cursor = 0
  for (let i = 0; i <= final.length; i++) {
    const end = i === final.length ? original.length : positions[i]
    if (end > cursor) {
      const deleted = original.slice(cursor, end)
      if (deleted.trim()) result.set(i, deleted)
    }
    cursor = end + 1
  }
  return result
}

export function pasteAsMarkdown(plain: string, html: string, hasRtf: boolean): string {
  if (!html.trim() || html.length > 2_000_000 || plain.length > 2_000_000) return plain
  const fragment = html.match(/<!--StartFragment-->([\s\S]*?)<!--EndFragment-->/)
  // Template content is inert and never attached to the live document.
  const template = document.createElement('template')
  template.innerHTML = fragment ? fragment[1] : html
  const parts: Part[] = []
  const mark = (markdown: string) => { parts.push({ text: '', markdown }) }
  const text = (value: string) => { for (let i = 0; i < value.length; i++) parts.push({ text: value[i], markdown: escape(value[i]) }) }
  const visit = (node: Node, depth: number) => {
    if (depth > 100) return
    if (node.nodeType === Node.COMMENT_NODE) return
    if (node.nodeType === Node.TEXT_NODE) { text((node.textContent || '').replace(/\s+/g, ' ')); return }
    if (!(node instanceof Element)) { node.childNodes.forEach(n => visit(n, depth + 1)); return }
    const name = node.tagName.toLowerCase()
    if (['script', 'style', 'head', 'iframe', 'object'].includes(name)) return
    if (name === 'br') { text('\n'); return }
    const style = (node.getAttribute('style') || '').toLowerCase()
    if (/(?:display\s*:\s*none|visibility\s*:\s*hidden)/.test(style)) return
    const bold = ['b', 'strong'].includes(name) || /font-weight\s*:\s*(bold|[7-9]00)/.test(style)
    const italic = ['i', 'em'].includes(name) || /font-style\s*:\s*italic/.test(style)
    const strike = ['s', 'strike', 'del'].includes(name) || style.includes('line-through')
    const block = ['p', 'div', 'li', 'tr', 'blockquote'].includes(name) || /^h[1-6]$/.test(name)
    if (block && parts.length) text('\n')
    if (/^h[1-6]$/.test(name)) mark('#'.repeat(Number(name[1])) + ' ')
    if (name === 'li') mark(node.parentElement?.tagName === 'OL' ? '1. ' : '- ')
    if (name === 'blockquote') mark('> ')
    mark((bold ? '**' : '') + (italic ? '*' : '') + (strike ? '~~' : ''))
    const href = node.getAttribute('href') || ''
    const link = name === 'a' && /^(https?:\/\/|mailto:)/i.test(href)
    if (link) mark('[')
    node.childNodes.forEach(n => visit(n, depth + 1))
    if (link) mark('](' + href.replace(/ /g, '%20').replace(/\(/g, '%28').replace(/\)/g, '%29') + ')')
    mark((strike ? '~~' : '') + (italic ? '*' : '') + (bold ? '**' : ''))
    if (['td', 'th'].includes(name)) text('\t')
    if (block) text('\n')
  }
  visit(template.content, 0)
  let final = ''
  const map: number[] = []
  parts.forEach((part, i) => {
    for (let j = 0; j < part.text.length; j++) {
      const c = part.text[j]
      if (/\s/.test(c)) { if (!final || final.endsWith(' ')) continue; final += ' ' }
      else final += c
      map.push(i)
    }
  })
  if (final.endsWith(' ')) { final = final.slice(0, -1); map.pop() }
  const before = new Map<number, string>()
  const comparisonPlain = template.content.querySelector('li') ? plain.replace(/^[ \t]*(?:\d+[.)]|[•·])[^\S\r\n]+/gm, '') : plain
  if (hasRtf) recoverDeletions(normalize(comparisonPlain), final).forEach((value, key) => before.set(key < map.length ? map[key] : map.length ? map[map.length - 1] + 1 : parts.length, value))
  let output = ''
  for (let i = 0; i <= parts.length; i++) {
    if (before.has(i)) { const deleted = before.get(i)!; output += (deleted.match(/^\s*/)?.[0] || '') + '~~' + escape(deleted.trim()) + '~~' + (deleted.match(/\s*$/)?.[0] || '') }
    if (i < parts.length) output += parts[i].markdown
  }
  return output.replace(/\n[ \t]*\n(?:[ \t]*\n)+/g, '\n\n').trim()
}
