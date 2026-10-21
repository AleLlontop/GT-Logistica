import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, expect, it, vi } from 'vitest'
import { FacturacionReporte } from './Facturacion'
import { consultarAnalisis } from '../servicios/servicioAnalisis'
import { generarReporte } from '../servicios/servicioReportes'
import type { AnalisisCobros, AnalisisFacturacion } from '../servicios/tiposAnalisis'
vi.mock('../servicios/servicioAnalisis', async original => ({ ...await original<typeof import('../servicios/servicioAnalisis')>(), consultarAnalisis: vi.fn() }))
vi.mock('../servicios/servicioReportes', async original => ({ ...await original<typeof import('../servicios/servicioReportes')>(), generarReporte: vi.fn() }))
vi.mock('../componentes/GraficosRecharts', () => ({ default: () => <p>Representación visual</p> }))
const contexto = { desde: '2026-10-01', hasta: '2026-10-31', consultadoEn: '2026-10-20T14:00:00Z', hoyArgentina: '2026-10-20', criterioTemporal: 'fechaFactura' as const }
const grupo = { top: [], otros: null, totalCategorias: 0 }
const documentos: AnalisisFacturacion = { contexto, resumen: { cantidad: 1, facturado: '121.00', cobradoVigenteDocumental: '0.00', pendienteVigenteDocumental: '121.00', cantidadAnuladas: 0 }, series: [], distribuciones: { estado: grupo, cliente: grupo }, detalle: { pagina: 1, tamanioPagina: 20, total: 1, items: [{ id: 7, numeroComprobante: '0001-00000007', fecha: '2026-10-10', clienteId: 1, clienteRazonSocial: 'Cliente histórico', estado: 'pendiente', neto: '100.00', iva: '21.00', total: '121.00', fechaCobro: null }] } }
const cobros: AnalisisCobros = { contexto: { ...contexto, criterioTemporal: 'fechaCobro' }, resumen: { cantidadCobros: 1, importeCobrado: '90071992547409.91' }, series: [], distribuciones: { cliente: grupo }, detalle: { pagina: 1, tamanioPagina: 20, total: 1, items: [{ facturaId: 5, numeroComprobante: '0001-00000005', clienteId: 1, clienteRazonSocial: 'Cliente de septiembre', fechaFactura: '2026-09-10', fechaCobro: '2026-10-05', importeCobrado: '90071992547409.91' }] } }
function montar(permisos = ['reportes.consultar', 'facturacion.consultar', 'reportes.emitir']) { vi.mocked(consultarAnalisis).mockImplementation(async recurso => recurso === 'facturacion' ? documentos : cobros); render(<MemoryRouter initialEntries={['/reportes/facturacion?desde=2026-10-01&hasta=2026-10-31']}><FacturacionReporte sesionClave="ana" permisos={permisos} /></MemoryRouter>) }
afterEach(() => vi.clearAllMocks())
it('documentos no restan cobro anterior y flujo identifica ambas fechas e importe exacto', async () => {
  montar(); await screen.findByRole('link', { name: '0001-00000007' })
  expect(screen.getByRole('columnheader', { name: 'IVA' })).toBeInTheDocument()
  expect(screen.getByRole('cell', { name: 'Sin cobro' })).toBeInTheDocument()
  await userEvent.setup().click(screen.getByRole('button', { name: 'Cobros' }))
  expect(await screen.findByRole('link', { name: '0001-00000005' })).toBeInTheDocument()
  expect(screen.getByRole('cell', { name: '10/09/2026' })).toBeInTheDocument(); expect(screen.getByRole('cell', { name: '05/10/2026' })).toBeInTheDocument()
  expect(screen.getAllByText(/90\.071\.992\.547\.409,91/)).toHaveLength(2)
  expect(screen.queryByLabelText('Estado documental')).not.toBeInTheDocument()
})
it('conserva filtros comunes y excluye estado documental del flujo; exporta perspectiva', async () => {
  vi.mocked(generarReporte).mockResolvedValue({ caso: 'entregado', entrega: 'descargado' } as never); montar(); await screen.findByRole('link', { name: '0001-00000007' })
  fireEvent.change(screen.getByLabelText('Número de factura'), { target: { value: '0001' } })
  await userEvent.setup().selectOptions(screen.getByLabelText('Estado documental'), 'anulada'); await userEvent.setup().click(screen.getByRole('button', { name: 'Aplicar filtros' }))
  await waitFor(() => expect(consultarAnalisis).toHaveBeenLastCalledWith('facturacion', expect.objectContaining({ estado: 'anulada', numero: '0001' })))
  await userEvent.setup().click(screen.getByRole('button', { name: 'Cobros' })); await screen.findByRole('link', { name: '0001-00000005' })
  expect(screen.getByLabelText('Número de factura')).toHaveValue('0001'); expect(vi.mocked(consultarAnalisis).mock.lastCall?.[1]).not.toHaveProperty('estado')
  await userEvent.setup().click(screen.getByRole('button', { name: 'Generar reporte' })); await userEvent.setup().click(screen.getByRole('button', { name: 'Excel' }))
  expect(generarReporte).toHaveBeenCalledWith('analisis-cobros', 'excel', expect.objectContaining({ numero: '0001' }))
})
it('exporta documentos y base sin fuente no entrega datos', async () => {
  vi.mocked(generarReporte).mockResolvedValue({ caso: 'entregado', entrega: 'descargado' } as never); montar(); await screen.findByRole('link', { name: '0001-00000007' })
  await userEvent.setup().click(screen.getByRole('button', { name: 'Generar reporte' })); await userEvent.setup().click(screen.getByRole('button', { name: 'PDF' }))
  expect(generarReporte).toHaveBeenCalledWith('analisis-facturacion', 'pdf', expect.objectContaining({ desde: '2026-10-01' }))
})
it('sin fuente no consulta', () => { montar(['reportes.consultar']); expect(consultarAnalisis).not.toHaveBeenCalled(); expect(screen.getByText(/No tenés permiso/)).toBeInTheDocument() })
it('cambio de perspectiva por flecha conserva foco en su control', async () => {
  montar(); await screen.findByRole('link', { name: '0001-00000007' })
  screen.getByRole('button', { name: 'Documentos' }).focus()
  await userEvent.setup().keyboard('{ArrowRight}')
  expect(await screen.findByRole('button', { name: 'Cobros', pressed: true })).toHaveFocus()
  expect(await screen.findByRole('link', { name: '0001-00000005' })).toBeInTheDocument()
})
