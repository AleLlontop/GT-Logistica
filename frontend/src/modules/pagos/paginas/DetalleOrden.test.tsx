import { beforeEach, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import type { ImporteDecimal } from '../servicios/tipos'
import { DetalleOrden } from './DetalleOrden'
import { useResultadoPago } from '../componentes/resultadoPago'
import { useResultadoFactura } from '../../proveedores/componentes/resultadoFactura'
import { ResultadoFacturaProvider } from '../../proveedores/componentes/ResultadoFacturaContexto'
import type { DetalleFacturaProveedor } from '../../proveedores/servicios/tipos'
import type { DetalleOrden as Orden } from '../servicios/tipos'
import { ResultadoPagoProvider } from '../componentes/ResultadoPagoContexto'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import * as api from '../servicios/pagos'
vi.mock('../servicios/pagos', async () => ({ ...await vi.importActual('../servicios/pagos'), obtenerOrden: vi.fn(), anularOrden: vi.fn() }))
beforeEach(() => vi.resetAllMocks())
function montar() { render(<MemoryRouter initialEntries={['/pagos/ordenes/25']}><ResultadoPagoProvider><Routes><Route path="/pagos/ordenes/:id" element={<DetalleOrden puedeConsultar />} /></Routes></ResultadoPagoProvider></MemoryRouter>) }
it('sin DTO runtime hace GET autorizado y muestra403 sin proponer reintento', async () => {
  vi.mocked(api.obtenerOrden).mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso' }))
  montar(); expect(await screen.findByRole('alert')).toHaveTextContent(api.SIN_PERMISO_PAGOS); expect(api.obtenerOrden).toHaveBeenCalledWith(25)
  expect(screen.queryByText(/volvé a intentar/i)).not.toBeInTheDocument()
})
it('consulta detalle completo distinguiendo fecha e instante sin inventar permisos externos', async () => {
  vi.mocked(api.obtenerOrden).mockResolvedValue({ id: 25, numero: 'OPP-25', fecha: '2026-10-17', importeTotal: '121.00' as ImporteDecimal, estado: 'pendiente', detalle: null,
    proveedor: { id: 3, razonSocial: 'Proveedor', cuit: '30712345671', tipo: 'proveedorNormal', activoAlGenerar: false, activoActual: false },
    registradaPor: { id: 1, nombre: 'Admin' }, registradaEn: '2026-10-18T12:00:00Z', facturas: [], medios: [], caja: null, anulacion: null })
  montar(); expect(await screen.findByRole('heading', { name: 'OPP-25' })).toBeVisible(); expect(screen.getByText('17/10/2026')).toBeVisible(); expect(screen.getByText(/Inactivo al generar/)).toBeVisible()
  expect(screen.queryByRole('link', { name: 'Ver caja' })).not.toBeInTheDocument()
})

it('guardar el pago descarta el resultado de factura que quedó desactualizado', () => {
  function Prueba() {
    const factura = useResultadoFactura(); const pago = useResultadoPago()
    return <><button onClick={() => factura.guardar({ id: 8 } as DetalleFacturaProveedor)}>Factura</button>
      <button onClick={() => pago.guardarOrden({ id: 25, facturas: [{ id: 8 }] } as Orden)}>Pago</button>
      <output>{factura.resultado ? 'Factura en memoria' : 'Requiere consulta autorizada'}</output></>
  }
  render(<ResultadoFacturaProvider><ResultadoPagoProvider><Prueba /></ResultadoPagoProvider></ResultadoFacturaProvider>)
  fireEvent.click(screen.getByRole('button', { name: 'Factura' })); expect(screen.getByText('Factura en memoria')).toBeVisible()
  fireEvent.click(screen.getByRole('button', { name: 'Pago' })); expect(screen.getByText('Requiere consulta autorizada')).toBeVisible()
})

const baseAnulacion: Orden = { id: 25, numero: 'OPP-25', fecha: '2026-10-18', importeTotal: '121.00' as ImporteDecimal, estado: 'pendiente', detalle: null,
  proveedor: { id: 3, razonSocial: 'Proveedor', cuit: '30712345671', tipo: 'proveedorNormal', activoAlGenerar: true, activoActual: true },
  registradaPor: { id: 1, nombre: 'Administración' }, registradaEn: '2026-10-18T12:00:00Z', facturas: [{ id: 8, numero: 'F-8', fecha: '2026-10-17', importeTotalAlPagar: '121.00' as ImporteDecimal, estadoActual: 'pagada', vinculoVigente: true, proveedorRazonSocial: 'Proveedor', proveedorCuit: '30712345671', liquidacion: null }],
  medios: [{ id: 1, tipo: 'efectivo', renglonId: 'r-1', fecha: '2026-10-18', importe: '121.00' as ImporteDecimal }], caja: null, anulacion: null }
function montarAnulacion(consultar: boolean, anular: boolean) { render(<MemoryRouter initialEntries={['/pagos/ordenes/25']}><ResultadoPagoProvider><Routes><Route path="/pagos/ordenes/:id" element={<DetalleOrden puedeConsultar={consultar} puedeAnular={anular} />} /></Routes></ResultadoPagoProvider></MemoryRouter>) }
it.each([[true, false], [false, true]])('consulta y anulación se requieren separadamente en UI: %s/%s', async (consultar, anular) => {
  vi.mocked(api.obtenerOrden).mockResolvedValue(baseAnulacion); montarAnulacion(consultar, anular)
  await screen.findByRole('heading', { name: 'OPP-25' }); expect(screen.queryByRole('button', { name: 'Anular OPP' })).not.toBeInTheDocument()
})
it('éxito muestra Anulada, motivo/autor/historia y medios originales sin ofrecer nueva anulación', async () => {
  vi.mocked(api.obtenerOrden).mockResolvedValue(baseAnulacion)
  vi.mocked(api.anularOrden).mockResolvedValue({ ...baseAnulacion, estado: 'anulada', facturas: baseAnulacion.facturas.map(f => ({ ...f, estadoActual: 'pendiente', vinculoVigente: false })),
    anulacion: { motivo: 'Corregir', usuario: { id: 2, nombre: 'Anulador' }, ocurridaEn: '2026-10-18T13:00:00Z', efectos: { facturasRestituidas: [8], chequesAnulados: [], pagosRevertidos: [], movimientoCompensatorio: null } } })
  montarAnulacion(true, true); const u = userEvent.setup()
  await u.click(await screen.findByRole('button', { name: 'Anular OPP' })); await u.type(screen.getByLabelText('Motivo'), 'Corregir')
  await u.click(screen.getByRole('button', { name: 'Confirmar anulación' })); expect(await screen.findByText('Orden anulada correctamente.')).toBeVisible()
  expect(screen.getByText('Anulada')).toBeVisible(); expect(screen.getByText('Corregir')).toBeVisible(); expect(screen.getByText('Anulador')).toBeVisible()
  expect(screen.getByText(/F-8.*Pendiente/)).toBeVisible(); expect(screen.getByText(/Efectivo.*121/)).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Anular OPP' })).not.toBeInTheDocument(); expect(api.anularOrden).toHaveBeenCalledOnce()
})
