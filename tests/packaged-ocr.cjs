// Run with a packaged node_modules directory outside the development checkout,
// plus a PNG containing "Hello eScratch 123". This prevents missing dependencies
// from being resolved accidentally from the development node_modules folder.
const path = require('node:path')
const root = path.resolve(process.argv[2])
const image = path.resolve(process.argv[3])
const { createWorker } = require(path.join(root, 'tesseract.js'))
;(async () => {
  const worker = await createWorker('eng', 1, {
    workerPath: path.join(root, 'tesseract.js/src/worker-script/node/index.js'),
    langPath: path.join(root, '@tesseract.js-data/eng/4.0.0_best_int'),
    cacheMethod: 'none',
  })
  try {
    const { data } = await worker.recognize(image)
    if (!data.text.includes('Hello') || !data.text.includes('123')) throw new Error('Unexpected OCR result: ' + data.text)
    console.log('PASS: isolated packaged OCR: ' + data.text.trim())
  } finally { await worker.terminate() }
})().catch(error => { console.error(error); process.exitCode = 1 })
