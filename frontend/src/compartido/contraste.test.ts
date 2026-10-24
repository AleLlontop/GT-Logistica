/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const css = readFileSync('src/index.css', 'utf8')
const light = Object.fromEntries([...css.match(/@theme\s*\{([\s\S]*?)\n\}/)![1].matchAll(/(--color-[\w-]+):\s*([^;]+);/g)].map(m => [m[1], m[2].trim()]))
const dark = { ...light, ...Object.fromEntries([...css.match(/:root\[data-theme='dark'\]\s*\{([\s\S]*?)\n\}/)![1].matchAll(/(--color-[\w-]+):\s*([^;]+);/g)].map(m => [m[1], m[2].trim()])) }
function luminancia(hex: string) {
  const [r, g, b] = hex.slice(1).match(/.{2}/g)!.map(v => { const c = parseInt(v, 16) / 255; return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4 })
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}
function resolve(tokens: Record<string, string>, name: string): string {
  const value = tokens['--color-' + name]
  return value.startsWith('var(') ? resolve(tokens, value.slice(12, -1)) : value
}
const pares = [
  ...['ink', 'ink-soft', 'muted', 'faint'].flatMap(text => ['canvas', 'surface', 'surface-soft', 'surface-mute'].map(bg => [text, bg])),
  ['brand', 'surface'], ['brand', 'brand-soft'], ['on-brand', 'brand-fill'], ['on-brand', 'brand-hover'],
  ['on-solid', 'danger-fill'], ['on-solid', 'danger-hover'], ['danger-text', 'danger-bg'],
  ['estado-rendido', 'estado-rendido-bg'], ['estado-pendiente', 'estado-pendiente-bg'],
  ['estado-facturado', 'estado-facturado-bg'], ['estado-anulado', 'estado-anulado-bg'],
  ['encabezado', 'surface-soft'], ['sidebar-text', 'sidebar'], ['sidebar-muted', 'sidebar'],
  ['sidebar-selected', 'sidebar-active'], ['sidebar-hover-text', 'sidebar-hover'],
  ['topbar-text', 'topbar-bg'], ['topbar-muted', 'topbar-bg'],
  ['topbar-text', 'topbar-hover'], ['topbar-focus', 'topbar-bg'],
  ['topbar-focus', 'topbar-hover'],
]
for (const [theme, tokens] of Object.entries({ light, dark })) describe('Contraste ' + theme, () => {
  for (const [fg, bg] of pares) it(`${fg} sobre ${bg}: mínimo 4,5:1`, () => {
    const a = luminancia(resolve(tokens, fg)), b = luminancia(resolve(tokens, bg))
    expect((Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05)).toBeGreaterThanOrEqual(4.5)
  })
  for (const [fg, bg] of [['line-strong', 'surface-soft'], ['brand', 'surface'], ['chart-primary', 'surface'], ['chart-secondary', 'surface'], ['chart-accent', 'surface']]) it(`${fg}: mínimo 3:1 para controles/gráficos`, () => {
    const a = luminancia(resolve(tokens, fg)), b = luminancia(resolve(tokens, bg))
    expect((Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05)).toBeGreaterThanOrEqual(3)
  })
})
