import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { GraficoAccesible } from './GraficoAccesible'

const control = vi.hoisted(() => ({ fallar: false, suspender: false, pendiente: new Promise<never>(() => {}) }))
vi.mock('./GraficosRecharts', () => ({ default: () => {
  if (control.fallar) throw new Error('Fallo de render sintético')
  if (control.suspender) throw control.pendiente
  return <div role="img" aria-label="Dibujo cargado" />
} }))
beforeEach(() => { control.fallar = false; control.suspender = false })
afterEach(() => vi.restoreAllMocks())
const datos = [{ etiqueta: 'Octubre', valores: { cobrado: '9999999999999999.99', pagado: '0.01' } }, { etiqueta: 'Otros', valores: { cobrado: '1.10', pagado: '0.00' } }]
const series = [{ clave: 'cobrado', nombre: 'Cobrado' }, { clave: 'pagado', nombre: 'Pagado' }]

describe('gráfico accesible', () => {
  it('título, unidad y leyenda explican datos; teclado abre tabla exacta y categorías completas', async () => {
    const usuario = userEvent.setup(), categorias = vi.fn()
    render(<GraficoAccesible titulo="Flujos mensuales" unidad="ARS" datos={datos} series={series} monetario tipo="lineas" recorte="Top 10 más Otros; 21 categorías en total." onVerCategorias={categorias} />)
    expect(screen.getByRole('heading', { name: 'Flujos mensuales' })).toBeVisible()
    expect(screen.getByText('Unidad: ARS. Los valores exactos están en la tabla.')).toBeVisible()
    expect(screen.getByText('Cobrado (línea continua)')).toBeVisible()
    expect(screen.getByText('Pagado (línea discontinua)')).toBeVisible()
    expect(screen.getByText('Top 10 más Otros; 21 categorías en total.')).toBeVisible()
    await usuario.tab()
    expect(screen.getByRole('button', { name: 'Ver tabla de Flujos mensuales' })).toHaveFocus()
    await usuario.keyboard('{Enter}')
    const tabla = screen.getByRole('table', { name: 'Flujos mensuales: valores exactos en ARS' })
    expect(within(tabla).getByText('$ 9.999.999.999.999.999,99')).toBeVisible()
    expect(within(tabla).getByText('Otros')).toBeVisible()
    expect(within(tabla).getAllByRole('row')).toHaveLength(3)
    await usuario.tab(); await usuario.keyboard('{Enter}')
    expect(categorias).toHaveBeenCalledOnce()
  })

  it('la tabla está disponible mientras lazy sigue pendiente', async () => {
    control.suspender = true
    const usuario = userEvent.setup()
    render(<GraficoAccesible titulo="Cobros" unidad="ARS" datos={datos} series={series} monetario />)
    expect(await screen.findByRole('status')).toHaveTextContent('Cargando gráfico… La tabla ya está disponible.')
    await usuario.click(screen.getByRole('button', { name: 'Ver tabla de Cobros' }))
    expect(screen.getByText('$ 9.999.999.999.999.999,99')).toBeVisible()
  })

  it('fallo de dibujo es error, no cero, y no impide leer la tabla', async () => {
    control.fallar = true; vi.spyOn(console, 'error').mockImplementation(() => {})
    const usuario = userEvent.setup()
    render(<GraficoAccesible titulo="Cobros" unidad="ARS" datos={datos} series={series} monetario />)
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('La tabla de valores exactos sigue disponible.'))
    expect(screen.queryByText('Sin actividad para graficar en este período.')).not.toBeInTheDocument()
    await usuario.click(screen.getByRole('button', { name: 'Ver tabla de Cobros' }))
    expect(screen.getByText('$ 9.999.999.999.999.999,99')).toBeVisible()
  })

  it('cero mantiene categoría y tabla, sin inventar actividad', async () => {
    const usuario = userEvent.setup()
    render(<GraficoAccesible titulo="Viajes" unidad="viajes" datos={[{ etiqueta: 'Unidad sin viajes', valores: { cantidad: 0 } }]} series={[{ clave: 'cantidad', nombre: 'Realizados' }]} />)
    expect(screen.getByRole('status')).toHaveTextContent('Sin actividad para graficar en este período.')
    await usuario.click(screen.getByRole('button', { name: 'Ver tabla de Viajes' }))
    expect(screen.getByRole('cell', { name: 'Unidad sin viajes' })).toBeVisible()
    expect(screen.getByRole('cell', { name: '0' })).toBeVisible()
  })
})
