import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, expect, it, vi } from 'vitest'
import { ViajesReporte } from './Viajes'
import { consultarAnalisis, consultarGrupos, consultarReferencias } from '../servicios/servicioAnalisis'
import { generarReporte } from '../servicios/servicioReportes'
import type { AnalisisViajes } from '../servicios/tiposAnalisis'
vi.mock('../servicios/servicioAnalisis', async original => ({ ...await original<typeof import('../servicios/servicioAnalisis')>(), consultarAnalisis: vi.fn(), consultarGrupos: vi.fn(), consultarReferencias: vi.fn() }))
vi.mock('../servicios/servicioReportes', async original => ({ ...await original<typeof import('../servicios/servicioReportes')>(), generarReporte: vi.fn() }))
vi.mock('../componentes/GraficosRecharts', () => ({ default: () => <p>Representación visual</p> }))
const contexto = { desde: '2026-09-01', hasta: '2026-09-30', consultadoEn: '2026-10-20T14:00:00Z', hoyArgentina: '2026-10-20', criterioTemporal: 'fechaViaje' as const }
function datos(): AnalisisViajes {
  const distribucion = { top: [{ id: null, nombre: 'Sin asignar', cantidad: 2 }], otros: { id: 'otros', nombre: 'Otros', cantidad: 21 }, totalCategorias: 12 }
  const dimporte = { ...distribucion, top: distribucion.top.map(g => ({ ...g, importe: '10.00' })), otros: { ...distribucion.otros, importe: '200.00' } }
  return { contexto, resumen: { cantidad: 23, realizados: 20, pendientesRendicion: 2, pendientes: 1, anulados: 0, importeTotal: '90071992547409.91' }, series: [{ periodo: '2026-09', cantidad: 23, realizados: 20, pendientesRendicion: 2 }], distribuciones: { estado: distribucion, cliente: dimporte, transportista: dimporte }, detalle: { pagina: 1, tamanioPagina: 20, total: 23, items: [{ id: 4, numero: 44, fecha: '2026-09-17', cliente: { id: 1, nombre: 'Cliente histórico' }, origen: 'Rosario', destino: 'Córdoba', chofer: null, vehiculo: null, transportista: null, estado: 'facturado', importe: '90071992547409.91' }] } }
}
function montar(permisos = ['reportes.consultar', 'viajes.consultar', 'reportes.emitir']) { render(<MemoryRouter initialEntries={['/reportes/viajes?desde=2026-09-01&hasta=2026-09-30']}><ViajesReporte sesionClave="ana" permisos={permisos} /></MemoryRouter>) }
afterEach(() => vi.clearAllMocks())
it('detalle conserva diez columnas, KPI completo y página/exportación usan filtros aplicados', async () => {
  vi.mocked(consultarAnalisis).mockResolvedValue(datos()); vi.mocked(generarReporte).mockResolvedValue({ caso: 'entregado', entrega: 'descargado' } as never)
  montar(); await screen.findByRole('link', { name: '44' })
  expect(screen.getAllByRole('columnheader')).toHaveLength(10)
  expect(screen.getByRole('cell', { name: 'Facturado' })).toBeInTheDocument(); expect(screen.getAllByText(/90\.071\.992\.547\.409,91/)).toHaveLength(2)
  fireEvent.change(screen.getByLabelText('Búsqueda'), { target: { value: 'borrador' } })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Siguiente' }))
  await waitFor(() => expect(consultarAnalisis).toHaveBeenLastCalledWith('viajes', expect.objectContaining({ pagina: 2, desde: '2026-09-01' })))
  expect(vi.mocked(consultarAnalisis).mock.lastCall?.[1]).not.toHaveProperty('busqueda')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Generar reporte' })); await userEvent.setup().click(screen.getByRole('button', { name: 'Excel' }))
  await waitFor(() => expect(generarReporte).toHaveBeenCalledWith('analisis-viajes', 'excel', expect.objectContaining({ pagina: 2, desde: '2026-09-01' })))
})
it('estado anulado explícito; categorías completas y null separado de Otros', async () => {
  vi.mocked(consultarAnalisis).mockResolvedValue(datos()); vi.mocked(consultarGrupos).mockResolvedValue({ contexto, detalle: { pagina: 1, tamanioPagina: 20, total: 21, items: [{ id: null, nombre: 'Sin asignar', cantidad: 2 }, { id: '11', nombre: 'Categoría once', cantidad: 1 }] } })
  montar(); await screen.findByRole('link', { name: '44' })
  await userEvent.setup().selectOptions(screen.getByLabelText('Estado'), 'anulado'); await userEvent.setup().click(screen.getByRole('button', { name: 'Aplicar filtros' }))
  await waitFor(() => expect(consultarAnalisis).toHaveBeenLastCalledWith('viajes', expect.objectContaining({ estado: 'anulado', pagina: 1 })))
  expect(await screen.findByText('Estado: anulado')).toBeInTheDocument()
  await userEvent.setup().click(screen.getByRole('button', { name: 'Ver todas las categorías de Viajes por cliente' }))
  expect(await screen.findByText('Categoría once')).toBeInTheDocument()
  expect(consultarGrupos).toHaveBeenCalledWith('viajes', 'cliente', expect.objectContaining({ estado: 'anulado', pagina: 1 }))
})
it('autocomplete mínimo admite inactivo por teclado, limpia y no consulta sin permiso', async () => {
  vi.mocked(consultarAnalisis).mockResolvedValue(datos()); vi.mocked(consultarReferencias).mockResolvedValue({ pagina: 1, tamanioPagina: 20, total: 1, items: [{ id: 7, nombre: 'Acme', activo: false }] })
  montar(); await screen.findByRole('link', { name: '44' })
  const cliente = screen.getByRole('combobox', { name: 'Cliente' }); await userEvent.setup().type(cliente, 'Acme')
  expect(await screen.findByRole('option', { name: 'Acme — Inactivo' })).toBeInTheDocument()
  fireEvent.keyDown(cliente, { key: 'ArrowDown' }); fireEvent.keyDown(cliente, { key: 'Enter' })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Aplicar filtros' }))
  await waitFor(() => expect(consultarAnalisis).toHaveBeenLastCalledWith('viajes', expect.objectContaining({ clienteId: 7 })))
  await userEvent.setup().click(screen.getByRole('button', { name: 'Limpiar' })); expect(screen.getByRole('combobox', { name: 'Cliente' })).toHaveValue('')
})
it('base sin fuente no consulta ni exporta', () => { montar(['reportes.consultar']); expect(consultarAnalisis).not.toHaveBeenCalled(); expect(screen.queryByRole('button', { name: 'Generar reporte' })).not.toBeInTheDocument() })
it('referencia escrita sin seleccionar impide consulta y permite corregir vaciando', async () => {
  vi.mocked(consultarAnalisis).mockResolvedValue(datos()); vi.mocked(consultarReferencias).mockResolvedValue({ pagina: 1, tamanioPagina: 20, total: 0, items: [] })
  montar(); await screen.findByRole('link', { name: '44' }); const cliente = screen.getByRole('combobox', { name: 'Cliente' })
  await userEvent.setup().type(cliente, 'sin seleccionar'); await userEvent.setup().click(screen.getByRole('button', { name: 'Aplicar filtros' }))
  expect(consultarAnalisis).toHaveBeenCalledTimes(1); expect(cliente).toHaveFocus(); expect(cliente).toBeInvalid()
  await userEvent.setup().clear(cliente); await userEvent.setup().click(screen.getByRole('button', { name: 'Aplicar filtros' })); expect(cliente).toBeValid()
})
