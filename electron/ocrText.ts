const betweenHan = /(?<=\p{Script=Han})[ \t]+(?=\p{Script=Han})/gu
const beforeChinesePunctuation = /(?<=\p{Script=Han})[ \t]+(?=[，。！？；：、!?])/gu
const afterChinesePunctuation = /(?<=[，。！？；：、!?])[ \t]+(?=\p{Script=Han})/gu
const singleLineBreak = /(?<=[^\r\n])[ \t]*\r?\n[ \t]*(?=[^\r\n])/g
const listStart = /^(?:[-*•]|\d+[.)])\s/u
const han = /^\p{Script=Han}$/u

export function cleanOcrText(input: string, joinWrappedLines: boolean): string {
  const text = input.trim().replace(betweenHan, '').replace(beforeChinesePunctuation, '').replace(afterChinesePunctuation, '')
  if (!joinWrappedLines) return text
  return text.replace(singleLineBreak, (breakText, offset: number) => {
    const before = text[offset - 1]
    const after = text[offset + breakText.length]
    const nextLine = text.slice(offset + breakText.length).split(/[\r\n]/, 1)[0]
    if (listStart.test(nextLine) || /[:：]/u.test(before)) return '\n'
    return (han.test(before) || /[，。！？；：、]/u.test(before)) &&
      (han.test(after) || /[，。！？；：、]/u.test(after)) ? '' : ' '
  })
}
