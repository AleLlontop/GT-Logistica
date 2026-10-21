import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, expect, it, vi } from 'vitest'
import { UtilizacionFlota } from './UtilizacionFlota'
import { consultarAnalisis, consultarGrupos, consultarReferencias } from '../servicios/servicioAnalisis'
import { generarReporte } from '../servicios/servicioReportes'
import type { AnalisisFlota } from '../servicios/tiposAnalisis'
vi.mock('../servicios/servicioAnalisis', async original => ({ ...await original<typeof import('../servicios/servicioAnalisis')>(), consultarAnalisis: vi.fn(), consultarGrupos: vi.fn(), consultarReferencias: vi.fn() }))
vi.mock('../servicios/servicioReportes', async original => ({ ...await original<typeof import('../servicios/servicioReportes')>(), generarReporte: vi.fn() }))
vi.mock('../componentes/GraficosRecharts', () => ({ default: () => <p>Representación visual</p> }))
const contexto = { desde: '2026-10-01', hasta: '2026-10-31', consultadoEn: '2026-10-20T14:00:00Z', hoyArgentina: '2026-10-20', criterioTemporal: 'fechaViaje' as const }
const grupo = { top: [{ id: '1', nombre: 'Tipo actual', cantidad: 9 }], otros: { id: 'otros', nombre: 'Otros', cantidad: 5 }, totalCategorias: 12 }
const d: AnalisisFlota = { contexto, resumen: { cantidadUnidades: 21, totalViajesConVehiculo: 14, sinVehiculoEnPeriodo: 3 }, series: [], distribuciones: { tipo: grupo, transportista: grupo }, detalle: { pagina: 1, tamanioPagina: 20, total: 21, items: [
  { id: 1, patente: 'AAA111', tipo: { id: 1, nombre: 'Tipo actual' }, transportista: { id: 2, nombre: 'Propietario actual' }, activo: false, estadoOperativoGuardado: 'disponible', disponibilidadActual: 'fueraDeServicio', cantidadViajes: 0 },
  { id: 2, patente: 'BBB222', tipo: { id: 1, nombre: 'Tipo actual' }, transportista: { id: 2, nombre: 'Propietario actual' }, activo: true, estadoOperativoGuardado: 'disponible', disponibilidadActual: 'disponible', cantidadViajes: 0 },
] } }
function montar(permisos = ['reportes.consultar', 'reportes.flota.consultar', 'reportes.emitir']) { vi.mocked(consultarAnalisis).mockResolvedValue(d); render(<MemoryRouter><UtilizacionFlota sesionClave="ana" permisos={permisos} /></MemoryRouter>) }
afterEach(() => vi.clearAllMocks())
it('ceros/inactivas/atributos actuales, sin vehículo separado y sin enlaces de gestión', async () => {
  montar(); await screen.findByRole('cell', { name: 'AAA111' })
  expect(screen.getByRole('cell', { name: 'Inactiva' })).toBeInTheDocument(); expect(screen.getAllByRole('cell', { name: '0' })).toHaveLength(2)
  expect(screen.getByText('Sin vehículo en el período (fuera del filtro de unidades)')).toBeInTheDocument(); expect(screen.getByRole('columnheader', { name: 'Disponibilidad actual' })).toBeInTheDocument()
  expect(screen.queryByRole('link', { name: 'AAA111' })).not.toBeInTheDocument(); expect(screen.queryByText(/kilómetros|ocupación|combustible/i)).not.toBeInTheDocument()
  await userEvent.setup().click(screen.getByRole('button', { name: 'Ver tabla de Viajes por tipo actual' })); expect(screen.getByRole('cell', { name: 'Otros' })).toBeInTheDocument()
})
it('todos/false y orden explícitos, paginación conserva ranking del servidor y exporta', async () => {
  vi.mocked(generarReporte).mockResolvedValue({ caso: 'entregado', entrega: 'descargado' } as never); montar(); await screen.findByRole('cell', { name: 'AAA111' })
  expect(vi.mocked(consultarAnalisis).mock.lastCall?.[1]).not.toHaveProperty('activo')
  await userEvent.setup().selectOptions(screen.getByLabelText('Actividad de la unidad'), 'false'); await userEvent.setup().selectOptions(screen.getByLabelText('Orden del ranking'), 'cantidadAsc'); await userEvent.setup().click(screen.getByRole('button', { name: 'Aplicar filtros' }))
  await waitFor(() => expect(consultarAnalisis).toHaveBeenLastCalledWith('flota', expect.objectContaining({ activo: false, orden: 'cantidadAsc' })))
  await userEvent.setup().click(screen.getByRole('button', { name: 'Generar reporte' })); await userEvent.setup().click(screen.getByRole('button', { name: 'Excel' })); expect(generarReporte).toHaveBeenCalledWith('analisis-flota', 'excel', expect.objectContaining({ activo: false }))
})
it('categorías completas y referencias usan endpoints mínimos sin gestión', async () => {
  vi.mocked(consultarReferencias).mockResolvedValue({ pagina: 1, tamanioPagina: 20, total: 1, items: [{ id: 1, nombre: 'Tipo actual' }] }); vi.mocked(consultarGrupos).mockResolvedValue({ contexto, detalle: { pagina: 1, tamanioPagina: 20, total: 12, items: [{ id: '12', nombre: 'Tipo doce', cantidad: 1 }] } })
  montar(); await screen.findByRole('cell', { name: 'AAA111' }); await userEvent.setup().type(screen.getByRole('combobox', { name: 'Tipo actual' }), 'Tipo'); await userEvent.setup().click(await screen.findByRole('option', { name: 'Tipo actual' }))
  await userEvent.setup().click(screen.getByRole('button', { name: 'Aplicar filtros' })); await waitFor(() => expect(consultarAnalisis).toHaveBeenLastCalledWith('flota', expect.objectContaining({ tipoVehiculoId: 1 })))
  await userEvent.setup().click(screen.getByRole('button', { name: 'Ver todas las categorías de Viajes por tipo actual' })); expect(await screen.findByText('Tipo doce')).toBeInTheDocument()
})
it('permiso fuente solo sin base no consulta', () => { montar(['reportes.flota.consultar']); expect(consultarAnalisis).not.toHaveBeenCalled(); expect(screen.getByText(/No tenés permiso/)).toBeInTheDocument() })
