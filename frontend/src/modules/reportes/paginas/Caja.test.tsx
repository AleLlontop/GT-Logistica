import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, expect, it, vi } from 'vitest'
import { CajaReporte } from './Caja'
import { consultarAnalisis, consultarReferencias } from '../servicios/servicioAnalisis'
import { generarReporte } from '../servicios/servicioReportes'
import type { AnalisisCaja } from '../servicios/tiposAnalisis'
vi.mock('../servicios/servicioAnalisis', async original => ({ ...await original<typeof import('../servicios/servicioAnalisis')>(), consultarAnalisis: vi.fn(), consultarReferencias: vi.fn() }))
vi.mock('../servicios/servicioReportes', async original => ({ ...await original<typeof import('../servicios/servicioReportes')>(), generarReporte: vi.fn() }))
vi.mock('../componentes/GraficosRecharts', () => ({ default: () => <p>Representación visual</p> }))
const contexto = { desde: '2026-10-01', hasta: '2026-10-31', consultadoEn: '2026-10-20T14:00:00Z', hoyArgentina: '2026-10-20', criterioTemporal: 'fechaMovimientoArgentina' as const }
const d: AnalisisCaja = { contexto, resumen: { cantidad: 2, ingresos: '100.00', egresos: '200.00', neto: '-100.00' }, series: [{ periodo: '2026-10', ingresos: '100.00', egresos: '200.00', neto: '-100.00' }], distribuciones: { tipo: { top: [], otros: null, totalCategorias: 0 } }, detalle: { pagina: 1, tamanioPagina: 20, total: 2, items: [
  { id: 1, cajaId: 2, cajaNombre: 'Caja histórica', fecha: '2026-10-02T02:30:00Z', tipo: 'egreso', importe: '200.00', concepto: 'Pago OPP', responsable: { id: 1, nombre: 'Ana' }, referencia: 'OPP-7', ordenDePagoId: null, ordenPagoProveedorId: 7 },
  { id: 2, cajaId: 2, cajaNombre: 'Caja histórica', fecha: '2026-10-03T03:30:00Z', tipo: 'ingreso', importe: '100.00', concepto: 'Compensación OPP', responsable: { id: 2, nombre: 'Beto' }, referencia: 'OP-4', ordenDePagoId: 4, ordenPagoProveedorId: null },
] } }
function montar(permisos = ['reportes.consultar', 'caja.consultar', 'reportes.emitir']) { vi.mocked(consultarAnalisis).mockResolvedValue(d); render(<MemoryRouter initialEntries={['/reportes/caja?desde=2026-10-01&hasta=2026-10-31']}><CajaReporte sesionClave="ana" permisos={permisos} /></MemoryRouter>) }
afterEach(() => vi.clearAllMocks())
it('neto negativo, corte argentino y referencias OP/OPP conservadas sin acceso lateral', async () => {
  montar(); expect(await screen.findByText('Movimiento neto')).toBeInTheDocument()
  expect(screen.getByText(/-.*100,00/)).toBeInTheDocument(); expect(screen.getByRole('cell', { name: /01\/10\/2026.*23:30/ })).toBeInTheDocument()
  expect(screen.getByRole('cell', { name: 'OPP-7' })).toBeInTheDocument(); expect(screen.getByRole('cell', { name: 'OP-4' })).toBeInTheDocument()
  expect(screen.queryByRole('link', { name: 'OPP-7' })).not.toBeInTheDocument(); expect(screen.queryByText(/saldo disponible/i)).not.toBeInTheDocument()
})
it('filtro mínimo de caja aplicado y exportación analítica', async () => {
  vi.mocked(consultarReferencias).mockResolvedValue({ pagina: 1, tamanioPagina: 20, total: 1, items: [{ id: 2, nombre: 'Caja histórica' }] }); vi.mocked(generarReporte).mockResolvedValue({ caso: 'entregado', entrega: 'descargado' } as never)
  montar(); await screen.findByText('Movimiento neto'); const caja = screen.getByRole('combobox', { name: 'Caja' }); await userEvent.setup().type(caja, 'Caja')
  await userEvent.setup().click(await screen.findByRole('option', { name: 'Caja histórica' })); await userEvent.setup().click(screen.getByRole('button', { name: 'Aplicar filtros' }))
  await waitFor(() => expect(consultarAnalisis).toHaveBeenLastCalledWith('caja', expect.objectContaining({ cajaId: 2 })))
  await userEvent.setup().click(screen.getByRole('button', { name: 'Generar reporte' })); await userEvent.setup().click(screen.getByRole('button', { name: 'Excel' }))
  expect(generarReporte).toHaveBeenCalledWith('analisis-caja', 'excel', expect.objectContaining({ cajaId: 2 }))
})
it('OPP sólo enlaza con permiso propio y no implica emisión', async () => { montar(['reportes.consultar', 'caja.consultar', 'pagos.consultar']); expect(await screen.findByRole('link', { name: 'OPP-7' })).toHaveAttribute('href', '/pagos/ordenes/7'); expect(screen.queryByRole('button', { name: 'Generar reporte' })).not.toBeInTheDocument() })
