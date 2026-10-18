import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { beforeEach, expect, it, vi } from 'vitest'
import { DetalleFacturaProveedor } from './DetalleFacturaProveedor'
import { ResultadoFacturaProvider } from '../componentes/ResultadoFacturaContexto'
import { useResultadoFactura } from '../componentes/resultadoFactura'
import * as api from '../servicios/facturasProveedor'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import type { DetalleFacturaProveedor as Detalle, ImporteDecimal } from '../servicios/tipos'

vi.mock('../servicios/facturasProveedor', () => ({ obtenerFacturaProveedor: vi.fn() }))
const d: Detalle = { id: 12, numero: '0001-12', fecha: '2026-10-16', proveedorId: 5, proveedorRazonSocial: 'Razón histórica', proveedorCuit: '30712345671', tipoProveedorAlRegistrar: 'proveedorNormal', neto: '100000000000000.01' as ImporteDecimal, iva: '21000000000000.00' as ImporteDecimal, importeTotal: '121000000000000.01' as ImporteDecimal, detalle: 'Servicio registrado', cae: 'CAE', fechaVencimientoCae: '2026-10-17', estado: 'pendiente', archivo: null, liquidacion: null }
beforeEach(() => { vi.resetAllMocks(); vi.mocked(api.obtenerFacturaProveedor).mockResolvedValue(d) })
function ResultadoDePost({ datos }: { datos: Detalle }) { const { guardar } = useResultadoFactura(); return <button onClick={() => guardar(datos)}>Recibir POST</button> }
function RouterState() { return <output data-testid="router-state">{JSON.stringify(useLocation().state)}</output> }
function abrir({ id = 12, puedeConsultar = true, puedeGestionarProveedores = false, puedeConsultarLiquidaciones = false, state = null as unknown, resultadoPost = d } = {}) {
  return render(<MemoryRouter initialEntries={[{ pathname: `/proveedores/facturas/${id}`, state }]}><ResultadoFacturaProvider><ResultadoDePost datos={resultadoPost} /><Routes>
    <Route path="/proveedores/facturas/:id" element={<DetalleFacturaProveedor {...{ puedeConsultar, puedeGestionarProveedores, puedeConsultarLiquidaciones }} />} />
  </Routes><RouterState /></ResultadoFacturaProvider></MemoryRouter>)
}
it('la URL directa obtiene detalle con snapshot, total destacado y ausencia de PDF/liquidación', async () => {
  abrir(); expect(await screen.findByText('Razón histórica', { selector: 'dd' })).toBeInTheDocument(); expect(api.obtenerFacturaProveedor).toHaveBeenCalledWith(12)
  expect(screen.getByText('$ 121.000.000.000.000,01')).toBeInTheDocument(); expect(screen.getByText('No corresponde liquidación.')).toBeInTheDocument(); expect(screen.getByText('Sin PDF adjunto.')).toBeInTheDocument()
  expect(screen.getByText('Proveedor normal', { selector: 'dd' })).toBeInTheDocument()
  expect(screen.queryByRole('link', { name: /Ver proveedor actual/ })).not.toBeInTheDocument()
})
it('ofrece referencias de origen sólo con los permisos propios y PDF sólo con consultar', async () => {
  vi.mocked(api.obtenerFacturaProveedor).mockResolvedValue({ ...d, tipoProveedorAlRegistrar: 'fletero', archivo: { nombre: 'original.pdf', url: '/api/proveedores/facturas/12/archivo' }, liquidacion: { id: 8, numero: 'LQ-8', periodo: '10/2026', importeAlRegistrar: '100000000000000.01' as ImporteDecimal, importeActual: '100000000000000.02' as ImporteDecimal, estadoActual: 'anulada' } })
  const vista = abrir(); await screen.findByText('LQ-8 · 10/2026')
  expect(screen.getByText('Fletero', { selector: 'dd' })).toBeInTheDocument()
  expect(screen.getAllByText('$ 100.000.000.000.000,01', { selector: 'dd' })).toHaveLength(2); expect(screen.getByText('$ 100.000.000.000.000,02')).toBeInTheDocument(); expect(screen.getByText('Anulada')).toBeInTheDocument()
  expect(screen.queryByRole('link', { name: /Ver liquidación/ })).not.toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'Abrir PDF: original.pdf' })).toHaveAttribute('href', '/api/proveedores/facturas/12/archivo')
  vista.unmount(); abrir({ puedeGestionarProveedores: true, puedeConsultarLiquidaciones: true }); await screen.findByText('LQ-8 · 10/2026')
  expect(screen.getByRole('link', { name: 'Ver proveedor actual' })).toHaveAttribute('href', '/proveedores/5'); expect(screen.getByRole('link', { name: 'Ver liquidación LQ-8' })).toHaveAttribute('href', '/liquidaciones/8')
})
it('runtime nuevo e Id diferente hacen GET; router/storage no rehidratan un DTO ni evitan 403', async () => {
  vi.mocked(api.obtenerFacturaProveedor).mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso' }))
  const storage = vi.spyOn(Storage.prototype, 'setItem'); const obtenerStorage = vi.spyOn(Storage.prototype, 'getItem'); const indexar = vi.fn(); vi.stubGlobal('indexedDB', { open: indexar })
  const uno = abrir({ puedeConsultar: false, state: d }); expect(await screen.findByText(/Pedile acceso a un administrador/)).toBeInTheDocument()
  const u = userEvent.setup(); await u.click(screen.getByRole('button', { name: 'Recibir POST' })); expect(await screen.findByText('Factura registrada.')).toBeInTheDocument()
  expect(api.obtenerFacturaProveedor).toHaveBeenCalledTimes(1); expect(storage).not.toHaveBeenCalled(); expect(obtenerStorage).not.toHaveBeenCalled(); expect(indexar).not.toHaveBeenCalled()
  uno.unmount(); vi.mocked(api.obtenerFacturaProveedor).mockClear(); const dos = abrir({ puedeConsultar: false }); await screen.findByText(/Pedile acceso a un administrador/); expect(api.obtenerFacturaProveedor).toHaveBeenCalledWith(12); dos.unmount()
  vi.mocked(api.obtenerFacturaProveedor).mockClear(); abrir({ id: 13, puedeConsultar: false }); await u.click(screen.getByRole('button', { name: 'Recibir POST' })); await waitFor(() => expect(api.obtenerFacturaProveedor).toHaveBeenCalledWith(13)); expect(screen.queryByText('Factura registrada.')).not.toBeInTheDocument()
  storage.mockRestore(); obtenerStorage.mockRestore(); vi.unstubAllGlobals()
})
it('con sólo cargar muestra el DTO recibido pero no ofrece abrir PDF ni referencias ajenas', async () => {
  vi.mocked(api.obtenerFacturaProveedor).mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso' }))
  abrir({ puedeConsultar: false, resultadoPost: { ...d, archivo: { nombre: 'recibida.pdf', url: '/api/proveedores/facturas/12/archivo' } } })
  await screen.findByText(/Pedile acceso a un administrador/)
  const u = userEvent.setup(); await u.click(screen.getByRole('button', { name: 'Recibir POST' }))
  expect(await screen.findByText('El PDF requiere permiso de consulta.')).toBeInTheDocument()
  expect(screen.queryByRole('link', { name: /Abrir PDF|Ver proveedor actual|Ver liquidación/ })).not.toBeInTheDocument()
  expect(api.obtenerFacturaProveedor).toHaveBeenCalledTimes(1); expect(screen.getByTestId('router-state')).toHaveTextContent('null')
})
