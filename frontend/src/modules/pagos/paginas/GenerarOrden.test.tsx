import { beforeEach, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { ResultadoPagoProvider } from '../componentes/ResultadoPagoContexto'
import { ResultadoOrden } from '../componentes/ResultadoOrden'
import { GenerarOrden } from './GenerarOrden'
import * as api from '../servicios/pagos'
import type { DetalleOrden, ImporteDecimal } from '../servicios/tipos'
vi.mock('../servicios/pagos', async () => ({ ...await vi.importActual('../servicios/pagos'),
  referencias: vi.fn(), proveedoresElegibles: vi.fn(), facturasElegibles: vi.fn(), seleccionarFacturas: vi.fn(), generarOrden: vi.fn(), obtenerOrden: vi.fn() }))
const importe = '121.00' as ImporteDecimal
const factura = { id: 1, numero: 'F-1', fecha: '2026-10-17', importeTotal: importe, proveedorId: 3, tipoProveedorAlRegistrar: 'proveedorNormal' as const, liquidacion: null }
const proveedor = { id: 3, razonSocial: 'Proveedor Prueba', cuit: '30712345671', tipo: 'proveedorNormal' as const, activo: false }
const resultado: DetalleOrden = { id: 10, numero: 'OPP-10', fecha: '2026-10-18', importeTotal: importe, estado: 'pendiente', detalle: null,
  proveedor: { ...proveedor, activoAlGenerar: false, activoActual: false }, registradaPor: { id: 1, nombre: 'Administración' }, registradaEn: '2026-10-18T12:00:00Z', facturas: [], medios: [], caja: null, anulacion: null }
function EstadoRouter() { return <output data-testid="estado-router">{JSON.stringify(useLocation().state)}</output> }
function montar() {
  return render(<MemoryRouter initialEntries={['/pagos/nueva']}><ResultadoPagoProvider><Routes>
    <Route path="/pagos/nueva" element={<GenerarOrden puedeGenerar />} /><Route path="/pagos/ordenes/:id" element={<ResultadoOrden />} />
    <Route path="/" element={<h1>Inicio</h1>} />
  </Routes><EstadoRouter /></ResultadoPagoProvider></MemoryRouter>)
}
beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(api.referencias).mockResolvedValue({ hoyArgentina: '2026-10-18', cajaPropia: { id: 5, estado: 'abierta', saldoDisponible: importe }, bancosActivos: [{ id: 1, nombre: 'Banco', activo: true }] })
  vi.mocked(api.proveedoresElegibles).mockResolvedValue({ items: [proveedor], total: 1, pagina: 1, tamanioPagina: 20 })
  vi.mocked(api.facturasElegibles).mockResolvedValue({ items: [factura], total: 1, pagina: 1, tamanioPagina: 20 })
  vi.mocked(api.seleccionarFacturas).mockResolvedValue({ items: [factura], noDisponibles: [], importeTotal: importe })
  vi.mocked(api.generarOrden).mockResolvedValue(resultado)
  vi.mocked(api.obtenerOrden).mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso' }))
})
async function preparar(usuario: ReturnType<typeof userEvent.setup>) {
  await usuario.type(await screen.findByLabelText('Proveedor'), 'Prueba')
  await usuario.click(await screen.findByRole('option', { name: /Proveedor Prueba.*Inactivo/ }))
  await usuario.click(await screen.findByRole('checkbox', { name: 'Seleccionar factura F-1' }))
  await usuario.selectOptions(screen.getByLabelText('Tipo de medio'), 'efectivo')
  await usuario.click(screen.getByRole('button', { name: 'Agregar medio' }))
  const dialogo = screen.getByRole('dialog', { name: 'Agregar efectivo' })
  await usuario.type(within(dialogo).getByLabelText('Importe'), '121,00')
  await usuario.click(within(dialogo).getByRole('button', { name: 'Agregar medio' }))
}
it('armar es temporal y Generar envía una intención completa una sola vez, retirando el formulario', async () => {
  montar(); const usuario = userEvent.setup()
  expect(await screen.findByLabelText('Fecha')).toHaveValue('2026-10-18')
  await preparar(usuario)
  expect(api.generarOrden).not.toHaveBeenCalled()
  await usuario.click(screen.getByRole('button', { name: 'Generar' }))
  expect(await screen.findByRole('heading', { name: 'OPP-10' })).toBeVisible()
  expect(api.generarOrden).toHaveBeenCalledOnce()
  expect(api.generarOrden).toHaveBeenCalledWith(expect.objectContaining({ proveedorId: 3, fecha: '2026-10-18', detalle: null,
    facturaIds: [1], importeTotalConfirmado: '121.00', confirmado: true, medios: [expect.objectContaining({ tipo: 'efectivo', importe: '121.00' })] }))
  expect(screen.queryByRole('button', { name: 'Generar' })).not.toBeInTheDocument()
})
it('respuesta incierta conserva objeto, clave y medios y bloquea una nueva intención involuntaria', async () => {
  vi.mocked(api.generarOrden).mockRejectedValueOnce(new ErrorHttp(0, { codigo: 'sin_conexion', mensaje: 'Conexión perdida.' })).mockResolvedValue(resultado)
  montar(); const usuario = userEvent.setup(); await preparar(usuario)
  await usuario.click(screen.getByRole('button', { name: 'Generar' }))
  expect(await screen.findByText('Conexión perdida.')).toBeVisible()
  expect(screen.getByLabelText('Fecha')).toBeDisabled()
  expect(screen.getByRole('button', { name: 'Agregar medio' })).toBeDisabled()
  const primera = vi.mocked(api.generarOrden).mock.calls[0][0]
  await usuario.click(screen.getByRole('button', { name: 'Reintentar generación' }))
  expect(await screen.findByRole('heading', { name: 'OPP-10' })).toBeVisible()
  expect(vi.mocked(api.generarOrden).mock.calls[1][0]).toBe(primera)
})
it('cancelar después de armar no genera escrituras', async () => {
  montar(); const usuario = userEvent.setup(); await preparar(usuario)
  await usuario.click(screen.getByRole('button', { name: 'Cancelar' }))
  expect(await screen.findByRole('heading', { name: 'Inicio' })).toBeVisible()
  expect(api.generarOrden).not.toHaveBeenCalled()
})

it('selección entre páginas conserva facturas; editar/quitar medios sólo cambia formulario', async () => {
  const segunda = { ...factura, id: 2, numero: 'F-2' }
  vi.mocked(api.facturasElegibles).mockImplementation(async (_id, pagina) => ({ items: pagina === 1 ? [factura] : [segunda], total: 21, pagina, tamanioPagina: 20 }))
  vi.mocked(api.seleccionarFacturas).mockImplementation(async ids => ({ items: ids.map(id => id === 1 ? factura : segunda), noDisponibles: [], importeTotal: (ids.length === 2 ? '242.00' : '121.00') as ImporteDecimal }))
  montar(); const usuario = userEvent.setup(); await preparar(usuario)
  await usuario.click(screen.getByRole('button', { name: 'Siguiente' }))
  await usuario.click(await screen.findByRole('checkbox', { name: 'Seleccionar factura F-2' }))
  await usuario.click(screen.getByRole('button', { name: 'Anterior' }))
  expect(await screen.findByRole('checkbox', { name: 'Seleccionar factura F-1' })).toBeChecked()
  expect(screen.getByRole('list', { name: 'Facturas seleccionadas' })).toHaveTextContent('F-2')
  await usuario.click(screen.getByRole('button', { name: 'Editar medio 1' }))
  const dialogo = screen.getByRole('dialog', { name: 'Editar efectivo' })
  await usuario.clear(within(dialogo).getByLabelText('Importe')); await usuario.type(within(dialogo).getByLabelText('Importe'), '242,00')
  await usuario.click(within(dialogo).getByRole('button', { name: 'Guardar medio' }))
  expect(api.generarOrden).not.toHaveBeenCalled()
  await usuario.click(screen.getByRole('button', { name: 'Quitar medio 1' }))
  expect(screen.queryByRole('button', { name: 'Editar medio 1' })).not.toBeInTheDocument()
  expect(api.generarOrden).not.toHaveBeenCalled()
})
it('un centavo de diferencia se rechaza antes de enviar', async () => {
  montar(); const usuario = userEvent.setup(); await preparar(usuario)
  await usuario.click(screen.getByRole('button', { name: 'Editar medio 1' }))
  const dialogo = screen.getByRole('dialog', { name: 'Editar efectivo' })
  await usuario.clear(within(dialogo).getByLabelText('Importe')); await usuario.type(within(dialogo).getByLabelText('Importe'), '120,99')
  await usuario.click(within(dialogo).getByRole('button', { name: 'Guardar medio' }))
  await usuario.click(screen.getByRole('button', { name: 'Generar' }))
  expect(screen.getByText('Los medios deben cubrir exactamente el total de las facturas.')).toBeVisible()
  expect(api.generarOrden).not.toHaveBeenCalled()
})
it('máximo decimal conserva el último centavo desde selección, editor, preview y comando', async () => {
  const maximo = '9999999999999999.99' as ImporteDecimal
  vi.mocked(api.facturasElegibles).mockResolvedValue({ items: [{ ...factura, importeTotal: maximo }], total: 1, pagina: 1, tamanioPagina: 20 })
  vi.mocked(api.seleccionarFacturas).mockResolvedValue({ items: [{ ...factura, importeTotal: maximo }], noDisponibles: [], importeTotal: maximo })
  montar(); const usuario = userEvent.setup()
  await usuario.type(await screen.findByLabelText('Proveedor'), 'Prueba')
  await usuario.click(await screen.findByRole('option', { name: /Proveedor Prueba/ }))
  await usuario.click(await screen.findByRole('checkbox', { name: 'Seleccionar factura F-1' }))
  await usuario.click(screen.getByRole('button', { name: 'Agregar medio' }))
  const dialogo = screen.getByRole('dialog', { name: 'Agregar cheque' })
  await usuario.selectOptions(within(dialogo).getByLabelText('Banco'), '1')
  await usuario.type(within(dialogo).getByLabelText('Número de cheque'), '000-123')
  fireEvent.change(within(dialogo).getByLabelText('Vencimiento'), { target: { value: '2026-10-18' } })
  await usuario.type(within(dialogo).getByLabelText('Importe'), '9999999999999999,99')
  await usuario.click(within(dialogo).getByRole('button', { name: 'Agregar medio' }))
  expect(screen.getAllByText('$ 9.999.999.999.999.999,99').length).toBeGreaterThan(1)
  await usuario.click(screen.getByRole('button', { name: 'Generar' }))
  expect(await screen.findByRole('heading', { name: 'OPP-10' })).toBeVisible()
  expect(api.generarOrden).toHaveBeenCalledWith(expect.objectContaining({ importeTotalConfirmado: maximo, medios: [expect.objectContaining({ importe: maximo, numeroCheque: '000-123' })] }))
})
it('Fletero Inactivo sólo ofrece cheque diferido y no pide reactivar ni confirmar aviso', async () => {
  vi.mocked(api.proveedoresElegibles).mockResolvedValue({ items: [{ ...proveedor, tipo: 'fletero' }], total: 1, pagina: 1, tamanioPagina: 20 })
  montar(); const usuario = userEvent.setup()
  await usuario.type(await screen.findByLabelText('Proveedor'), 'Prueba')
  await usuario.click(await screen.findByRole('option', { name: /Inactivo/ }))
  await usuario.click(await screen.findByRole('checkbox', { name: 'Seleccionar factura F-1' }))
  expect(within(screen.getByLabelText('Tipo de medio')).getAllByRole('option')).toHaveLength(1)
  expect(screen.queryByRole('button', { name: /Reactivar/ })).not.toBeInTheDocument()
  await usuario.click(screen.getByRole('button', { name: 'Agregar medio' }))
  const dialogo = screen.getByRole('dialog', { name: 'Agregar cheque' })
  await usuario.selectOptions(within(dialogo).getByLabelText('Banco'), '1')
  await usuario.type(within(dialogo).getByLabelText('Número de cheque'), '000-123')
  fireEvent.change(within(dialogo).getByLabelText('Vencimiento'), { target: { value: '2026-10-18' } })
  await usuario.type(within(dialogo).getByLabelText('Importe'), '121,00')
  await usuario.click(within(dialogo).getByRole('button', { name: 'Agregar medio' }))
  expect(within(dialogo).getByText('El cheque del Fletero debe ser diferido.')).toBeVisible()
  fireEvent.change(within(dialogo).getByLabelText('Vencimiento'), { target: { value: '2026-10-19' } })
  await usuario.click(within(dialogo).getByRole('button', { name: 'Agregar medio' }))
  await usuario.click(screen.getByRole('button', { name: 'Generar' }))
  expect(await screen.findByRole('heading', { name: 'OPP-10' })).toBeVisible()
  expect(api.generarOrden).toHaveBeenCalledOnce()
})
it.each([409, 403])('rechazo HTTP %s no provoca handshake ni un segundo POST automático', async estado => {
  vi.mocked(api.generarOrden).mockRejectedValue(new ErrorHttp(estado, { codigo: 'datos_invalidos', mensaje: 'La factura dejó de estar disponible.' }))
  montar(); const usuario = userEvent.setup(); await preparar(usuario)
  await usuario.click(screen.getByRole('button', { name: 'Generar' }))
  expect(await screen.findByRole('alert')).toHaveTextContent(estado === 403 ? api.SIN_PERMISO_PAGOS : 'La factura dejó de estar disponible.')
  expect(api.generarOrden).toHaveBeenCalledOnce()
  if (estado === 403) expect(screen.queryByRole('button', { name: 'Generar' })).not.toBeInTheDocument()
  else await waitFor(() => expect(screen.getByRole('button', { name: 'Generar' })).toBeEnabled())
})
it('resultado propio no se copia a storage/history y runtime nuevo no lo rehidrata', async () => {
  const escribir = vi.spyOn(Storage.prototype, 'setItem'); const leer = vi.spyOn(Storage.prototype, 'getItem')
  const vista = montar(); const usuario = userEvent.setup(); await preparar(usuario)
  await usuario.click(screen.getByRole('button', { name: 'Generar' }))
  expect(await screen.findByRole('heading', { name: 'OPP-10' })).toBeVisible()
  expect(screen.getByTestId('estado-router')).toHaveTextContent('null')
  expect(escribir).not.toHaveBeenCalled(); expect(leer).not.toHaveBeenCalled()
  vista.unmount()
  render(<MemoryRouter initialEntries={[{ pathname: '/pagos/ordenes/10', state: resultado }]}><ResultadoPagoProvider><Routes>
    <Route path="/pagos/ordenes/:id" element={<ResultadoOrden />} />
  </Routes></ResultadoPagoProvider></MemoryRouter>)
  expect(await screen.findByRole('alert')).toHaveTextContent(api.SIN_PERMISO_PAGOS)
  expect(api.obtenerOrden).toHaveBeenCalledWith(10)
  expect(screen.queryByRole('heading', { name: 'OPP-10' })).not.toBeInTheDocument()
  escribir.mockRestore(); leer.mockRestore()
})
it('fecha default viene del servidor argentino aunque UTC ya sea mañana', async () => {
  vi.useFakeTimers({ toFake: ['Date'] }); vi.setSystemTime(new Date('2026-10-19T01:15:00Z'))
  try { montar(); expect(await screen.findByLabelText('Fecha')).toHaveValue('2026-10-18') }
  finally { vi.useRealTimers() }
})

it('saldo manual negativo se informa y el rechazo económico sigue siendo del servidor', async () => {
  vi.mocked(api.referencias).mockResolvedValue({ hoyArgentina: '2026-10-18', cajaPropia: { id: 5, estado: 'abierta', saldoDisponible: '-1.00' }, bancosActivos: [] })
  vi.mocked(api.generarOrden).mockRejectedValue(new ErrorHttp(409, { codigo: 'datos_invalidos', mensaje: 'El saldo actual no alcanza.' }))
  montar(); const usuario = userEvent.setup(); await preparar(usuario)
  expect(screen.getByText(/Saldo disponible de tu caja: -\$ 1,00/)).toBeVisible()
  await usuario.click(screen.getByRole('button', { name: 'Generar' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('El saldo actual no alcanza.')
})

it('un conflicto refresca caja y bancos sin reemplazar la fecha elegida ni los medios', async () => {
  vi.mocked(api.referencias).mockResolvedValueOnce({ hoyArgentina: '2026-10-18', cajaPropia: { id: 5, estado: 'abierta', saldoDisponible: '121.00' }, bancosActivos: [{ id: 1, nombre: 'Banco', activo: true }] })
    .mockResolvedValue({ hoyArgentina: '2026-10-19', cajaPropia: { id: 5, estado: 'abierta', saldoDisponible: '50.00' }, bancosActivos: [] })
  vi.mocked(api.generarOrden).mockRejectedValue(new ErrorHttp(409, { codigo: 'caja_no_disponible', mensaje: 'La caja cambió.' }))
  montar(); const usuario = userEvent.setup(); await preparar(usuario)
  fireEvent.change(screen.getByLabelText('Fecha'), { target: { value: '2026-10-17' } })
  await usuario.click(screen.getByRole('button', { name: 'Generar' })); expect(await screen.findByRole('alert')).toHaveTextContent('La caja cambió.')
  await waitFor(() => expect(api.referencias).toHaveBeenCalledTimes(2)); expect(screen.getByLabelText('Fecha')).toHaveValue('2026-10-17')
  expect(await screen.findByText(/Saldo disponible de tu caja: \$ 50,00/)).toBeVisible(); expect(screen.getByRole('button', { name: 'Editar medio 1' })).toBeVisible()
  await usuario.selectOptions(screen.getByLabelText('Tipo de medio'), 'cheque'); await usuario.click(screen.getByRole('button', { name: 'Agregar medio' }))
  expect(within(screen.getByRole('dialog')).getAllByRole('option')).toHaveLength(1)
})
