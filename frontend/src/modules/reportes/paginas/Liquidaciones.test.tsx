import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, expect, it, vi } from 'vitest'
import { LiquidacionesReporte } from './Liquidaciones'
import { consultarAnalisis } from '../servicios/servicioAnalisis'
import { generarReporte } from '../servicios/servicioReportes'
import type { AnalisisLiquidaciones, AnalisisPagos } from '../servicios/tiposAnalisis'
vi.mock('../servicios/servicioAnalisis', async original => ({ ...await original<typeof import('../servicios/servicioAnalisis')>(), consultarAnalisis: vi.fn() }))
vi.mock('../servicios/servicioReportes', async original => ({ ...await original<typeof import('../servicios/servicioReportes')>(), generarReporte: vi.fn() }))
vi.mock('../componentes/GraficosRecharts', () => ({ default: () => <p>Representación visual</p> }))
const contexto = { desde: '2026-09-15', hasta: '2026-10-10', consultadoEn: '2026-10-20T14:00:00Z', hoyArgentina: '2026-10-20', criterioTemporal: 'periodoLiquidado' as const, periodoLiquidadoDesde: '2026-09', periodoLiquidadoHasta: '2026-10' }
const grupo = { top: [], otros: null, totalCategorias: 0 }, transportista = { id: 2, nombre: 'Transportista histórico' }
const documentos: AnalisisLiquidaciones = { contexto, resumen: { cantidad: 2, importeLiquidado: '300.00', pagadoVigenteDocumental: '50.00', pendienteVigenteDocumental: '250.00', cantidadAnuladas: 0 }, series: [], distribuciones: { estado: grupo, transportista: grupo }, detalle: { pagina: 1, tamanioPagina: 20, total: 2, items: [{ id: 1, numero: 'LQ-1', periodo: '2026-09', transportista, estado: 'pendiente', importeTotal: '300.00', pagadoVigente: '50.00', pendienteVigente: '250.00' }] } }
const pagos: AnalisisPagos = { contexto: { ...contexto, criterioTemporal: 'fechaPago' }, resumen: { cantidadPagosVigentes: 2, importePagadoVigente: '150.00', cantidadRevertidos: 1, importeRevertidoHistorico: '200.00' }, series: [], distribuciones: { transportista: grupo }, detalle: { pagina: 1, tamanioPagina: 20, total: 3, items: [
  { id: 1, numero: 'OP-1', liquidacionId: 1, liquidacionNumero: 'LQ-1', periodoLiquidado: '2026-08', transportista, fechaPago: '2026-10-01', importe: '50.00', vigente: true, origen: 'manual', oppId: null, oppNumero: null, revertidaEn: null, revertidaPor: null },
  { id: 2, numero: 'OP-2', liquidacionId: 2, liquidacionNumero: 'LQ-2', periodoLiquidado: '2026-08', transportista, fechaPago: '2026-10-02', importe: '100.00', vigente: true, origen: 'opp', oppId: 8, oppNumero: 'OPP-8', revertidaEn: null, revertidaPor: null },
  { id: 3, numero: 'OP-3', liquidacionId: 3, liquidacionNumero: 'LQ-3', periodoLiquidado: '2026-07', transportista, fechaPago: '2026-10-02', importe: '200.00', vigente: false, origen: 'opp', oppId: 8, oppNumero: 'OPP-8', revertidaEn: '2026-10-15T14:00:00Z', revertidaPor: { id: 3, nombre: 'Autora reversión' } },
] } }
function montar(permisos = ['reportes.consultar', 'liquidaciones.consultar', 'reportes.emitir']) { vi.mocked(consultarAnalisis).mockImplementation(async recurso => recurso === 'liquidaciones' ? documentos : pagos); render(<MemoryRouter initialEntries={['/reportes/liquidaciones?desde=2026-09-15&hasta=2026-10-10']}><LiquidacionesReporte sesionClave="ana" permisos={permisos} /></MemoryRouter>) }
afterEach(() => vi.clearAllMocks())
it('meses completos y pagos parciales documentales; pagos usan días exactos y preservan historia', async () => {
  montar(); await screen.findByRole('link', { name: 'LQ-1' })
  expect(screen.getByText(/Se incluyen completos los meses 2026-09 a 2026-10/)).toBeInTheDocument()
  expect(screen.getByRole('cell', { name: /250,00/ })).toBeInTheDocument()
  await userEvent.setup().click(screen.getByRole('button', { name: 'Pagos' })); await screen.findByRole('cell', { name: 'OP-3' })
  expect(consultarAnalisis).toHaveBeenLastCalledWith('liquidaciones/pagos', expect.objectContaining({ desde: '2026-09-15', hasta: '2026-10-10' }))
  expect(screen.getByRole('cell', { name: 'Manual' })).toBeInTheDocument(); expect(screen.getByRole('cell', { name: 'Revertido' })).toBeInTheDocument(); expect(screen.getByRole('cell', { name: 'Autora reversión' })).toBeInTheDocument()
  expect(screen.queryByRole('link', { name: 'OPP-8' })).not.toBeInTheDocument()
  expect(screen.getByRole('cell', { name: /15\/10\/2026.*11:00/ })).toBeInTheDocument()
})
it('vigencia no se traslada a documentos y ambas exportaciones mantienen perspectiva', async () => {
  vi.mocked(generarReporte).mockResolvedValue({ caso: 'entregado', entrega: 'descargado' } as never); montar(); await screen.findByRole('link', { name: 'LQ-1' })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Generar reporte' })); await userEvent.setup().click(screen.getByRole('button', { name: 'PDF' }))
  expect(generarReporte).toHaveBeenCalledWith('analisis-liquidaciones', 'pdf', expect.anything())
  await userEvent.setup().click(screen.getByRole('button', { name: 'Pagos' })); await screen.findByRole('cell', { name: 'OP-3' })
  await userEvent.setup().selectOptions(screen.getByLabelText('Vigencia de pagos'), 'revertidos'); await userEvent.setup().click(screen.getByRole('button', { name: 'Aplicar filtros' }))
  await waitFor(() => expect(consultarAnalisis).toHaveBeenLastCalledWith('liquidaciones/pagos', expect.objectContaining({ vigencia: 'revertidos' })))
  await userEvent.setup().click(screen.getByRole('button', { name: 'Generar reporte' })); await userEvent.setup().click(screen.getByRole('button', { name: 'Excel' }))
  expect(generarReporte).toHaveBeenCalledWith('analisis-pagos', 'excel', expect.objectContaining({ vigencia: 'revertidos' }))
  await userEvent.setup().click(screen.getByRole('button', { name: 'Documentos' })); await screen.findByRole('link', { name: 'LQ-1' })
  expect(vi.mocked(consultarAnalisis).mock.lastCall?.[1]).not.toHaveProperty('vigencia')
})
it('OPP enlaza sólo con su permiso, consulta no habilita emisión', async () => {
  montar(['reportes.consultar', 'liquidaciones.consultar', 'pagos.consultar']); await screen.findByRole('link', { name: 'LQ-1' })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Pagos' })); await screen.findByRole('cell', { name: 'OP-3' })
  expect(screen.getAllByRole('link', { name: 'OPP-8' })).toHaveLength(2); expect(screen.queryByRole('button', { name: 'Generar reporte' })).not.toBeInTheDocument()
})
