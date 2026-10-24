import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import App from '../../../App'
import * as sesiones from '../../autenticacion/servicios/sesion'
import * as api from '../servicios/facturasProveedor'
import * as padron from '../servicios/proveedores'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { agruparEnSecciones } from '../../../compartido/seccionesDeMenu'
import type { DetalleFacturaProveedor, ImporteDecimal } from '../servicios/tipos'

vi.mock('../../autenticacion/servicios/sesion', async original => ({ ...await original<typeof import('../../autenticacion/servicios/sesion')>(), obtenerSesion: vi.fn(), cerrarSesion: vi.fn(), iniciarSesion: vi.fn() }))
vi.mock('../servicios/facturasProveedor', () => ({ buscarProveedores: vi.fn(), cargarFacturaProveedor: vi.fn(), obtenerFacturaProveedor: vi.fn(), consultarFacturasProveedor: vi.fn(), consultarLiquidacionesDisponibles: vi.fn() }))
vi.mock('../servicios/proveedores', async original => ({ ...await original<typeof import('../servicios/proveedores')>(), listarProveedores: vi.fn(), obtenerProveedor: vi.fn() }))
const gestionar = 'proveedores.gestionar', cargar = 'proveedores.facturas.cargar', consultar = 'proveedores.facturas.consultar'
const opciones = [
  { permiso: gestionar, codigo: 'proveedores', etiqueta: 'Proveedores', ruta: '/proveedores' },
  { permiso: cargar, codigo: 'cargar-factura-proveedor', etiqueta: 'Cargar factura de proveedor', ruta: '/proveedores/facturas/nueva' },
  { permiso: consultar, codigo: 'consultar-facturas-proveedores', etiqueta: 'Consultar facturas de proveedores', ruta: '/proveedores/facturas' },
]
function sesion(permisos: string[]) {
  // El nombre del rol deliberadamente contradice los permisos: App debe usar permisos/opciones del servidor.
  return { username: 'aislado', roles: [{ codigo: 'administrador_sistema', nombre: 'Administrador' }], permisos,
    opcionesMenu: opciones.filter(o => permisos.includes(o.permiso)).map(({ codigo, etiqueta, ruta }) => ({ codigo, etiqueta, ruta })) }
}
const normal = { id: 5, razonSocial: 'Proveedor elegido', cuit: '30712345671', nombreComercial: null, tipo: 'proveedorNormal' as const, estado: 'activo' as const }
const detalle: DetalleFacturaProveedor = { id: 12, proveedorId: normal.id, proveedorRazonSocial: normal.razonSocial, proveedorCuit: normal.cuit, numero: '001', fecha: '2026-10-17', tipoProveedorAlRegistrar: 'proveedorNormal', detalle: 'Servicio', neto: '100.00' as ImporteDecimal, iva: '21.00' as ImporteDecimal, importeTotal: '121.00' as ImporteDecimal, cae: 'CAE', fechaVencimientoCae: '2026-10-17', estado: 'pendiente', archivo: { nombre: 'factura.pdf', url: '/api/proveedores/facturas/12/archivo' }, liquidacion: null }
beforeEach(() => {
  vi.resetAllMocks(); window.history.replaceState(null, '', '/')
  vi.mocked(api.buscarProveedores).mockResolvedValue({ items: [normal], total: 1, pagina: 1, tamanioPagina: 20 })
  vi.mocked(api.cargarFacturaProveedor).mockResolvedValue(detalle)
  vi.mocked(api.obtenerFacturaProveedor).mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso' }))
  vi.mocked(api.consultarFacturasProveedor).mockResolvedValue({ items: [], total: 0, pagina: 1, tamanioPagina: 20 })
  vi.mocked(padron.listarProveedores).mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso' }))
  vi.mocked(sesiones.cerrarSesion).mockResolvedValue(undefined)
})
afterEach(() => vi.restoreAllMocks())
function abrir(permisos: string[], ruta = '/') { vi.mocked(sesiones.obtenerSesion).mockResolvedValue(sesion(permisos)); window.history.replaceState(null, '', ruta); return render(<App />) }
it.each([gestionar, cargar, consultar])('el menú recibe sólo la entrada del permiso %s', async permiso => {
  abrir([permiso]); const menu = await screen.findByRole('navigation', { name: /principal/i })
  await userEvent.setup().click(within(menu).getByRole('button', { name: 'Proveedores' }))
  for (const opcion of opciones) {
    const enlace = within(menu).queryByRole('link', { name: opcion.etiqueta })
    if (permiso === opcion.permiso) expect(enlace).toBeInTheDocument()
    else expect(enlace).not.toBeInTheDocument()
  }
})
it('agrupa padrón y facturas sin inventar opciones y conserva fallback desconocido', () => {
  const recibidas = opciones.map(({ codigo, etiqueta, ruta }) => ({ codigo, etiqueta, ruta }))
  const secciones = agruparEnSecciones([...recibidas, { codigo: 'futuro', etiqueta: 'Futuro', ruta: '/futuro' }])
  expect(secciones.find(s => s.nombre === 'Proveedores')?.opciones.map(o => o.codigo)).toEqual(['proveedores', 'cargar-factura-proveedor', 'consultar-facturas-proveedores'])
  expect(secciones.at(-1)?.opciones.map(o => o.codigo)).toContain('futuro')
})
it('la URI directa sin sesión exige ingreso', async () => {
  vi.mocked(sesiones.obtenerSesion).mockResolvedValue(null); window.history.replaceState(null, '', '/proveedores/facturas'); render(<App />)
  expect(await screen.findByLabelText('Nombre de usuario')).toBeInTheDocument(); expect(api.consultarFacturasProveedor).not.toHaveBeenCalled()
})
it('consultar o permisos ajenos no habilitan el formulario de carga', async () => {
  abrir([consultar, 'choferes.gestionar', 'liquidaciones.gestionar'], '/proveedores/facturas/nueva')
  expect(await screen.findByText(/No tenés permiso para cargar facturas.*administrador/)).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Guardar factura' })).not.toBeInTheDocument(); expect(api.buscarProveedores).not.toHaveBeenCalled()
})
it('sólo cargar no ofrece ABM al entrar al padrón y el 403 no invita a reintentar', async () => {
  abrir([cargar], '/proveedores'); expect(await screen.findByText(/No tenés permiso para administrar proveedores/)).toBeInTheDocument()
  expect(screen.queryByRole('link', { name: 'Nuevo proveedor' })).not.toBeInTheDocument(); expect(screen.queryByRole('button', { name: /Reintentar/ })).not.toBeInTheDocument()
})
it('GET directo conserva el 403 del servidor aunque el rol se llame administrador', async () => {
  vi.mocked(api.consultarFacturasProveedor).mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso' }))
  abrir([gestionar], '/proveedores/facturas'); expect(await screen.findByText(/No tenés permiso para consultar facturas.*administrador/)).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Reintentar' })).not.toBeInTheDocument()
})
it('seleccionar Fletero sólo consulta disponibles y no ofrece documentos ni ficha de otro módulo', async () => {
  vi.mocked(api.buscarProveedores).mockResolvedValue({ items: [{ ...normal, tipo: 'fletero' }], total: 1, pagina: 1, tamanioPagina: 20 })
  vi.mocked(api.consultarLiquidacionesDisponibles).mockResolvedValue([{ id: 8, numero: 'LQ-8', periodo: '10/2026', importeTotal: '121.00' as ImporteDecimal }])
  abrir([cargar], '/proveedores/facturas/nueva'); const u = userEvent.setup(); await u.type(await screen.findByLabelText('Proveedor'), 'Fletero')
  await u.click(await screen.findByRole('option', { name: /Proveedor elegido/ })); await screen.findByRole('option', { name: /LQ-8/ })
  expect(api.consultarLiquidacionesDisponibles).toHaveBeenCalledWith(5)
  expect(screen.queryByRole('link', { name: /Transportistas|Choferes|documentos|documentación|Ver liquidación/i })).not.toBeInTheDocument()
  expect(padron.obtenerProveedor).not.toHaveBeenCalled()
})
it('sólo cargar completa POST, usa memoria sin GET/PDF/router/storage y la recarga pierde el resultado', async () => {
  const persistir = vi.spyOn(Storage.prototype, 'setItem'); const vista = abrir([cargar], '/proveedores/facturas/nueva')
  const u = userEvent.setup(); await u.type(await screen.findByLabelText('Proveedor'), 'Proveedor'); await u.click(await screen.findByRole('option', { name: /Proveedor elegido/ }))
  await u.type(screen.getByLabelText('Número de factura'), '001'); await u.type(screen.getByLabelText('Detalle'), 'Servicio'); await u.type(screen.getByLabelText('Neto'), '100'); await u.type(screen.getByLabelText('CAE'), 'CAE')
  for (const etiqueta of ['Fecha de factura', 'Vencimiento del CAE']) fireEvent.change(screen.getByLabelText(etiqueta), { target: { value: '2026-10-17' } })
  await u.click(screen.getByRole('button', { name: 'Guardar factura' })); expect(await screen.findByText('Factura registrada.')).toBeInTheDocument()
  expect(api.obtenerFacturaProveedor).not.toHaveBeenCalled(); expect(screen.queryByRole('link', { name: /Abrir PDF|Ver proveedor actual/ })).not.toBeInTheDocument()
  expect(window.history.state?.usr).toBeFalsy(); expect(persistir).not.toHaveBeenCalled()
  vista.unmount(); abrir([cargar], '/proveedores/facturas/12'); expect(await screen.findByText(/No tenés permiso para consultar esta factura/)).toBeInTheDocument(); expect(api.obtenerFacturaProveedor).toHaveBeenCalledWith(12)
})
it('cerrar sesión y volver con otro usuario destruye el resultado del POST', async () => {
  abrir([cargar], '/proveedores/facturas/nueva'); const u = userEvent.setup()
  await u.type(await screen.findByLabelText('Proveedor'), 'Proveedor'); await u.click(await screen.findByRole('option', { name: /Proveedor elegido/ }))
  for (const [etiqueta, valor] of [['Número de factura', '001'], ['Detalle', 'Servicio'], ['Neto', '100'], ['CAE', 'CAE']]) await u.type(screen.getByLabelText(etiqueta), valor)
  for (const etiqueta of ['Fecha de factura', 'Vencimiento del CAE']) fireEvent.change(screen.getByLabelText(etiqueta), { target: { value: '2026-10-17' } })
  await u.click(screen.getByRole('button', { name: 'Guardar factura' })); await screen.findByText('Factura registrada.')
  await u.click(screen.getByRole('button', { name: 'Cerrar sesión' })); await screen.findByLabelText('Nombre de usuario')
  vi.mocked(sesiones.iniciarSesion).mockResolvedValue({ ...sesion([cargar]), username: 'otro' })
  await u.type(screen.getByLabelText('Nombre de usuario'), 'otro'); await u.type(screen.getByLabelText('Contraseña'), 'Temporal.123')
  await u.click(screen.getByRole('button', { name: 'Ingresar' }))
  await screen.findByRole('button', { name: 'Cerrar sesión' })
  act(() => { window.history.pushState(null, '', '/proveedores/facturas/12'); window.dispatchEvent(new PopStateEvent('popstate')) })
  expect(await screen.findByText(/No tenés permiso para consultar esta factura/)).toBeInTheDocument(); await waitFor(() => expect(api.obtenerFacturaProveedor).toHaveBeenCalledWith(12))
})
