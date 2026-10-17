import { render, screen, fireEvent, act } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { beforeEach, expect, it, vi } from 'vitest'
import { CargarFacturaProveedor } from './CargarFacturaProveedor'
import { DetalleFacturaProveedor } from './DetalleFacturaProveedor'
import { ResultadoFacturaProvider } from '../componentes/ResultadoFacturaContexto'
import * as api from '../servicios/facturasProveedor'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import type { ImporteDecimal } from '../servicios/tipos'

vi.mock('../servicios/facturasProveedor', () => ({ buscarProveedores: vi.fn(), consultarLiquidacionesDisponibles: vi.fn(), cargarFacturaProveedor: vi.fn(), obtenerFacturaProveedor: vi.fn() }))
const p = { id: 1, razonSocial: 'Fletero Díaz', nombreComercial: null, cuit: '20123456786', tipo: 'fletero' as const, estado: 'activo' as const }
const l = { id: 7, numero: 'LQ-37', periodo: '10/2026', importeTotal: '121.00' as ImporteDecimal }
beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(api.buscarProveedores).mockResolvedValue({ items: [p], total: 1, pagina: 1, tamanioPagina: 20 })
  vi.mocked(api.consultarLiquidacionesDisponibles).mockResolvedValue([l])
})
function abrir() { render(<MemoryRouter><ResultadoFacturaProvider><CargarFacturaProveedor /></ResultadoFacturaProvider></MemoryRouter>) }
async function elegir() {
  const u = userEvent.setup(); await u.type(screen.getByLabelText('Proveedor'), 'Díaz'); await u.click(await screen.findByRole('option', { name: /Fletero Díaz/ })); return u
}
async function llenar() {
  const u = await elegir(); await screen.findByRole('option', { name: /LQ-37.*10\/2026.*121,00/ })
  await u.type(screen.getByLabelText('Número de factura'), '001'); await u.type(screen.getByLabelText('Detalle'), 'Servicio')
  await u.type(screen.getByLabelText('Neto'), '100'); await u.type(screen.getByLabelText('CAE'), 'CAE')
  fireEvent.change(screen.getByLabelText('Fecha de factura'), { target: { value: '2026-10-16' } })
  fireEvent.change(screen.getByLabelText('Vencimiento del CAE'), { target: { value: '2026-10-16' } }); return u
}
it('requiere selección nativa y bloquea guardar hasta tener una liquidación válida', async () => {
  abrir(); const u = await llenar(); expect(screen.getByLabelText('Liquidación')).toBeRequired()
  expect(screen.getByRole('button', { name: 'Guardar factura' })).toBeDisabled()
  await u.selectOptions(screen.getByLabelText('Liquidación'), '7')
  vi.mocked(api.cargarFacturaProveedor).mockRejectedValue(new Error('Error transitorio'))
  await u.click(screen.getByRole('button', { name: 'Guardar factura' }))
  expect(api.cargarFacturaProveedor).toHaveBeenCalledWith(expect.objectContaining({ liquidacionId: 7, neto: '100.00' }))
})
it('sin disponibles explica el bloqueo y no permite guardar', async () => {
  vi.mocked(api.consultarLiquidacionesDisponibles).mockResolvedValue([]); abrir(); await elegir()
  expect(await screen.findByText(/No hay liquidaciones Pendientes disponibles/)).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Guardar factura' })).toBeDisabled(); expect(api.cargarFacturaProveedor).not.toHaveBeenCalled()
})
it('cambiar proveedor limpia selección; normal no muestra ni envía liquidación', async () => {
  abrir(); const u = await llenar(); await u.selectOptions(screen.getByLabelText('Liquidación'), '7')
  vi.mocked(api.buscarProveedores).mockResolvedValue({ items: [{ ...p, id: 2, tipo: 'proveedorNormal' }], total: 1, pagina: 1, tamanioPagina: 20 })
  await u.clear(screen.getByLabelText('Proveedor')); expect(screen.queryByLabelText('Liquidación')).not.toBeInTheDocument()
  await u.type(screen.getByLabelText('Proveedor'), 'Normal'); await u.click(await screen.findByRole('option', { name: /Fletero Díaz/ }))
  vi.mocked(api.cargarFacturaProveedor).mockRejectedValue(new Error('Error transitorio'))
  await u.click(screen.getByRole('button', { name: 'Guardar factura' }))
  expect(api.cargarFacturaProveedor).toHaveBeenCalledWith(expect.objectContaining({ proveedorId: 2, liquidacionId: undefined }))
})
it('muestra ambos importes del conflicto exactamente como los recibió y conserva lo ingresado sin confirmar', async () => {
  abrir(); const u = await llenar(); await u.selectOptions(screen.getByLabelText('Liquidación'), '7')
  vi.mocked(api.cargarFacturaProveedor).mockRejectedValue(new ErrorHttp(409, {
    codigo: 'importe_distinto', campo: 'liquidacionId', mensaje: 'Los totales no coinciden.',
    ...{ importeTotalFactura: '100000000000000.01', importeTotalLiquidacion: '100000000000000.02' },
  }))
  await u.click(screen.getByRole('button', { name: 'Guardar factura' }))
  expect(await screen.findByText('$ 100.000.000.000.000,01')).toBeInTheDocument()
  expect(screen.getByText('$ 100.000.000.000.000,02')).toBeInTheDocument()
  expect(screen.getByLabelText('Detalle')).toHaveValue('Servicio'); expect(screen.queryByRole('button', { name: /Confirmar/ })).not.toBeInTheDocument()
})
it('anuncia carga y error de consulta y permite reintentar conservando el formulario', async () => {
  vi.mocked(api.consultarLiquidacionesDisponibles).mockRejectedValueOnce(new Error('Sin conexión')); abrir(); const u = await elegir()
  expect(await screen.findByText('Sin conexión')).toBeInTheDocument(); expect(screen.getByRole('button', { name: 'Guardar factura' })).toBeDisabled()
  await u.click(screen.getByRole('button', { name: 'Reintentar liquidaciones' })); expect(await screen.findByRole('option', { name: /LQ-37/ })).toBeInTheDocument()
})
it('descarta una consulta tardía y exige una nueva selección al cambiar de Fletero', async () => {
  let resolver!: (valor: typeof l[]) => void
  vi.mocked(api.consultarLiquidacionesDisponibles).mockImplementationOnce(() => new Promise(r => { resolver = r }))
  abrir(); const u = await elegir(); expect(screen.getByText('Cargando liquidaciones…')).toBeInTheDocument()
  vi.mocked(api.buscarProveedores).mockResolvedValue({ items: [{ ...p, id: 2 }], total: 1, pagina: 1, tamanioPagina: 20 })
  vi.mocked(api.consultarLiquidacionesDisponibles).mockResolvedValue([{ ...l, id: 8, numero: 'LQ-38', importeTotal: '100000000000000.02' as ImporteDecimal }])
  await u.clear(screen.getByLabelText('Proveedor')); await u.type(screen.getByLabelText('Proveedor'), 'Otro')
  await u.click(await screen.findByRole('option', { name: /Fletero Díaz/ }))
  expect(await screen.findByRole('option', { name: /LQ-38.*100\.000\.000\.000\.000,02/ })).toBeInTheDocument()
  await act(async () => resolver([l]))
  expect(screen.queryByRole('option', { name: /LQ-37/ })).not.toBeInTheDocument()
  expect(screen.getByLabelText('Liquidación')).toHaveValue(''); expect(screen.getByRole('button', { name: 'Guardar factura' })).toBeDisabled()
  await u.selectOptions(screen.getByLabelText('Liquidación'), '8')
  vi.mocked(api.buscarProveedores).mockResolvedValue({ items: [p], total: 1, pagina: 1, tamanioPagina: 20 })
  await u.clear(screen.getByLabelText('Proveedor')); await u.type(screen.getByLabelText('Proveedor'), 'Díaz')
  await u.click(await screen.findByRole('option', { name: /Fletero Díaz/ }))
  expect(await screen.findByLabelText('Liquidación')).toHaveValue('')
})
function EstadoRouter() { return <output data-testid="estado-router">{JSON.stringify(useLocation().state)}</output> }
it('muestra la asociación del POST desde memoria sin GET ni DTO en storage o router', async () => {
  const persistir = vi.spyOn(Storage.prototype, 'setItem')
  vi.mocked(api.cargarFacturaProveedor).mockResolvedValue({
    id: 19, proveedorId: 1, proveedorRazonSocial: p.razonSocial, proveedorCuit: p.cuit, tipoProveedorAlRegistrar: 'fletero', numero: '001',
    fecha: '2026-10-16', detalle: 'Servicio', neto: '100.00' as ImporteDecimal, iva: '21.00' as ImporteDecimal, importeTotal: '121.00' as ImporteDecimal,
    cae: 'CAE', fechaVencimientoCae: '2026-10-16', estado: 'pendiente', archivo: null,
    liquidacion: { id: l.id, numero: l.numero, periodo: l.periodo, importeAlRegistrar: l.importeTotal, importeActual: l.importeTotal, estadoActual: 'pendiente' },
  })
  render(<MemoryRouter initialEntries={['/proveedores/facturas/nueva']}><ResultadoFacturaProvider><Routes>
    <Route path="/proveedores/facturas/nueva" element={<CargarFacturaProveedor />} />
    <Route path="/proveedores/facturas/:id" element={<DetalleFacturaProveedor />} />
  </Routes><EstadoRouter /></ResultadoFacturaProvider></MemoryRouter>)
  const u = await llenar(); await u.selectOptions(screen.getByLabelText('Liquidación'), '7'); await u.click(screen.getByRole('button', { name: 'Guardar factura' }))
  expect(await screen.findByText('Factura registrada.')).toBeInTheDocument()
  expect(screen.getByText('LQ-37 · 10/2026')).toBeInTheDocument()
  expect(screen.getByText('Importe de liquidación al registrar')).toBeInTheDocument()
  expect(api.obtenerFacturaProveedor).not.toHaveBeenCalled(); expect(screen.getByTestId('estado-router')).toHaveTextContent('null')
  expect(persistir).not.toHaveBeenCalled(); persistir.mockRestore()
})
