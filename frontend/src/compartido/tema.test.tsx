/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { act, cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { SelectorTema } from './SelectorTema'

const bootstrap = readFileSync('public/tema-inicial.js', 'utf8')
function iniciar(saved: string | null = null, oscuro = false, bloqueado = false) {
  const values = new Map(saved === null ? [] : [['gyt.tema', saved]])
  const eventos = new Map<string, (e: unknown) => void>()
  const sistema = new Map<string, (e: unknown) => void>()
  const storage = {
    getItem: vi.fn((key: string) => { if (bloqueado) throw new Error('storage bloqueado'); return values.get(key) ?? null }),
    setItem: vi.fn((key: string, value: string) => { if (bloqueado) throw new Error('storage bloqueado'); values.set(key, value) }),
  }
  const media = { matches: oscuro, addEventListener: (key: string, listener: (e: unknown) => void) => sistema.set(key, listener) }
  const browser: { localStorage: typeof storage; matchMedia: () => typeof media; addEventListener: (key: string, listener: (e: unknown) => void) => void; gytTema?: Window['gytTema'] } = {
    localStorage: storage, matchMedia: () => media, addEventListener: (key, listener) => eventos.set(key, listener),
  }
  new Function('window', 'document', bootstrap)(browser, document)
  window.gytTema = browser.gytTema
  return { browser, storage, values, eventos, sistema, media }
}

afterEach(() => { cleanup(); delete window.gytTema; document.documentElement.removeAttribute('data-theme'); document.documentElement.style.colorScheme = '' })

describe('Temas G&T Logística', () => {
  it('resuelve el tema antes de React, respeta el sistema y prioriza una elección guardada', () => {
    iniciar(null, true)
    expect(document.documentElement).toHaveAttribute('data-theme', 'dark')
    expect(document.documentElement.style.colorScheme).toBe('dark')
    iniciar('light', true)
    expect(document.documentElement).toHaveAttribute('data-theme', 'light')
    iniciar('valor inválido', true)
    expect(document.documentElement).toHaveAttribute('data-theme', 'dark')
    const html = readFileSync('index.html', 'utf8')
    expect(html.indexOf('<script src="/tema-inicial.js"></script>')).toBeGreaterThan(0)
    expect(html.indexOf('<script src="/tema-inicial.js"></script>')).toBeLessThan(html.indexOf('type="module"'))
  })
  it('permite cambiar con teclado, persiste la elección y la recupera al recargar', async () => {
    const { storage, values } = iniciar('light')
    render(<SelectorTema />)
    const user = userEvent.setup()
    await user.tab()
    expect(screen.getByRole('button', { name: 'Activar tema oscuro' })).toHaveFocus()
    await user.keyboard(' ')
    expect(document.documentElement).toHaveAttribute('data-theme', 'dark')
    expect(storage.setItem).toHaveBeenCalledWith('gyt.tema', 'dark')
    expect(screen.getByRole('button', { name: 'Activar tema claro' })).toBeVisible()
    cleanup()
    iniciar(values.get('gyt.tema')!)
    expect(document.documentElement).toHaveAttribute('data-theme', 'dark')
  })
  it('sincroniza pestañas y sigue al sistema sólo hasta una elección explícita', () => {
    const { browser, eventos, sistema } = iniciar(null, false)
    render(<SelectorTema />)
    act(() => sistema.get('change')!({ matches: true }))
    expect(screen.getByRole('button', { name: 'Activar tema claro' })).toBeVisible()
    act(() => browser.gytTema!.set('light'))
    act(() => sistema.get('change')!({ matches: true }))
    expect(document.documentElement).toHaveAttribute('data-theme', 'light')
    act(() => eventos.get('storage')!({ key: 'gyt.tema', newValue: 'dark' }))
    expect(document.documentElement).toHaveAttribute('data-theme', 'dark')
  })
  it('funciona con storage bloqueado sin impedir usar la aplicación', async () => {
    iniciar(null, true, true)
    render(<SelectorTema />)
    await userEvent.setup().click(screen.getByRole('button', { name: 'Activar tema claro' }))
    expect(document.documentElement).toHaveAttribute('data-theme', 'light')
  })
})
