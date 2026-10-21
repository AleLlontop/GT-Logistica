import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, expect, it, vi } from 'vitest'
import { VencimientosReporte } from './Vencimientos'
import { consultarAnalisis } from '../servicios/servicioAnalisis'
import { generarReporte } from '../servicios/servicioReportes'
import type { RespuestasAnalisis } from '../servicios/tiposAnalisis'
vi.mock('../servicios/servicioAnalisis', async original => ({ ...await original<typeof import('../servicios/servicioAnalisis')>(), consultarAnalisis: vi.fn() }))
vi.mock('../servicios/servicioReportes', async original => ({ ...await original<typeof import('../servicios/servicioReportes')>(), generarReporte: vi.fn() }))
const contexto = { desde: null, hasta: null, consultadoEn: '2026-10-20T14:00:00Z', hoyArgentina: '2026-10-20', criterioTemporal: 'situacionActual' as const }
const documento = { id: 1, tipo: { id: 1, nombre: 'Seguro' }, numero: 'N-1', fechaEmision: '2026-01-01', fechaVencimiento: '2026-10-20', estado: 'vigente' as const, esVigenteDelTipo: true, diasHastaVencimiento: 0, tieneArchivo: true, archivoNombre: 'privado.pdf' }
const datos: Pick<RespuestasAnalisis, 'vencimientos/choferes' | 'vencimientos/flota' | 'vencimientos/facturas'> = {
  'vencimientos/choferes': { contexto, detalle: { pagina: 1, tamanioPagina: 20, total: 21, items: [{ choferId: 4, apellido: 'Pérez', nombre: 'Ana', transportista: { id: 2, nombre: 'Transporte' }, documento }] } },
  'vencimientos/flota': { contexto, detalle: { pagina: 1, tamanioPagina: 20, total: 1, items: [{ vehiculoId: 6, patente: 'AAA111', transportista: { id: 2, nombre: 'Transporte' }, documento: { ...documento, vehiculoId: 6 } }] } },
  'vencimientos/facturas': { contexto, importeTotal: '90071992547409.91', detalle: { pagina: 1, tamanioPagina: 20, total: 1, items: [{ id: 7, numeroComprobante: '0001-00000007', cliente: 'Cliente', total: '90071992547409.91', vencimientoPago: '2026-10-19', dias: -1 }] } },
}
function montar(permisos = ['reportes.consultar', 'choferes.vencimientos.consultar', 'flota.vencimientos.consultar', 'facturacion.consultar', 'reportes.emitir']) { vi.mocked(consultarAnalisis).mockImplementation(async r => datos[r as keyof typeof datos]); render(<MemoryRouter initialEntries={['/reportes/vencimientos?desde=2020-01-01&hasta=2020-02-01']}><VencimientosReporte sesionClave="ana" permisos={permisos} /></MemoryRouter>) }
afterEach(() => vi.clearAllMocks())
it('tres paneles actuales, palabras/plazos originales sin adjuntos ni período global', async () => {
  montar(); expect(await screen.findByRole('cell', { name: 'Al día' })).toBeInTheDocument(); expect(screen.getByRole('cell', { name: 'Vence hoy' })).toBeInTheDocument()
  expect(screen.queryByLabelText('Desde')).not.toBeInTheDocument(); expect(screen.queryByRole('link', { name: 'Pérez, Ana' })).not.toBeInTheDocument(); expect(screen.queryByText('privado.pdf')).not.toBeInTheDocument()
  await userEvent.setup().click(screen.getByRole('button', { name: 'Flota' })); expect(await screen.findByRole('cell', { name: 'Vigente' })).toBeInTheDocument()
  await userEvent.setup().click(screen.getByRole('button', { name: 'Facturas' })); expect(await screen.findByRole('cell', { name: 'Venció hace 1 día' })).toBeInTheDocument(); expect(screen.getAllByText(/90\.071\.992\.547\.409,91/)).toHaveLength(2)
  expect(consultarAnalisis).toHaveBeenLastCalledWith('vencimientos/facturas', { pagina: 1, tamanioPagina: 20 })
})
it('paginación y tres exportaciones no llevan período global', async () => {
  vi.mocked(generarReporte).mockResolvedValue({ caso: 'entregado', entrega: 'descargado' } as never); montar(); await screen.findByRole('cell', { name: 'Al día' })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Siguiente' })); await waitFor(() => expect(consultarAnalisis).toHaveBeenLastCalledWith('vencimientos/choferes', { pagina: 2, tamanioPagina: 20 }))
  for (const [nombre, tipo] of [['Choferes', 'choferes'], ['Flota', 'flota'], ['Facturas', 'facturas']]) {
    await userEvent.setup().click(screen.getByRole('button', { name: nombre })); await screen.findByRole('button', { name: 'Generar reporte' })
    await userEvent.setup().click(screen.getByRole('button', { name: 'Generar reporte' })); await userEvent.setup().click(screen.getByRole('button', { name: 'Excel' }))
    expect(generarReporte).toHaveBeenCalledWith(`analisis-vencimientos-${tipo}`, 'excel', {})
  }
})
it('panel aislado no requiere gestionar, no consulta los denegados ni permite emisión', async () => {
  montar(['reportes.consultar', 'flota.vencimientos.consultar']); await screen.findByRole('cell', { name: 'AAA111' })
  expect(screen.queryByRole('button', { name: 'Choferes' })).not.toBeInTheDocument(); expect(screen.queryByRole('button', { name: 'Generar reporte' })).not.toBeInTheDocument()
  expect(consultarAnalisis).toHaveBeenCalledTimes(1); expect(consultarAnalisis).toHaveBeenCalledWith('vencimientos/flota', expect.anything())
})
it('sin panel autorizado no consulta y explica permiso', () => { montar(['reportes.consultar']); expect(consultarAnalisis).not.toHaveBeenCalled(); expect(screen.getByText(/No tenés permiso/)).toBeInTheDocument() })
