import { act,render,screen,fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter,Route,Routes,useLocation,useNavigate } from 'react-router-dom'
import { beforeEach,afterEach,expect,it,vi } from 'vitest'
import { CargarFacturaProveedor } from './CargarFacturaProveedor'
import { DetalleFacturaProveedor } from './DetalleFacturaProveedor'
import { ResultadoFacturaProvider } from '../componentes/ResultadoFacturaContexto'
import * as api from '../servicios/facturasProveedor'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import type { DetalleFacturaProveedor as Detalle,ImporteDecimal } from '../servicios/tipos'

vi.mock('../servicios/facturasProveedor',()=>({buscarProveedores:vi.fn(),cargarFacturaProveedor:vi.fn(),obtenerFacturaProveedor:vi.fn()}))
const p={id:1,razonSocial:'Acopio',nombreComercial:null,cuit:'30712345671',tipo:'proveedorNormal',estado:'activo'}
const detalle:Detalle={id:12,proveedorId:1,proveedorRazonSocial:'Acopio',proveedorCuit:p.cuit,tipoProveedorAlRegistrar:'proveedorNormal',numero:'001',fecha:'2026-10-16',detalle:'Servicio',neto:'100.00' as ImporteDecimal,iva:'21.00' as ImporteDecimal,importeTotal:'121.00' as ImporteDecimal,cae:'CAE',fechaVencimientoCae:'2026-10-16',estado:'pendiente',archivo:null,liquidacion:null}
function EstadoRuta(){const l=useLocation();const navegar=useNavigate();return <><output data-testid="ruta">{JSON.stringify(l.state)}</output><button onClick={()=>navegar('/proveedores/facturas/13')}>Abrir otra</button></>}
function abrir(ruta='/proveedores/facturas/nueva'){
  return render(<MemoryRouter initialEntries={[ruta]}><ResultadoFacturaProvider><Routes>
    <Route path="/proveedores/facturas/nueva" element={<CargarFacturaProveedor />} />
    <Route path="/proveedores/facturas/:id" element={<DetalleFacturaProveedor puedeConsultar={false} />} />
  </Routes><EstadoRuta /></ResultadoFacturaProvider></MemoryRouter>)
}
beforeEach(()=>{
  vi.resetAllMocks();vi.mocked(api.buscarProveedores).mockResolvedValue({items:[p as never],total:1,pagina:1,tamanioPagina:20})
  vi.mocked(api.cargarFacturaProveedor).mockResolvedValue(detalle)
  vi.mocked(api.obtenerFacturaProveedor).mockRejectedValue(new ErrorHttp(403,{codigo:'sin_permiso',mensaje:'Sin permiso'}))
})
afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
})
async function llenar(u = userEvent.setup()) {
  const proveedor = screen.getByRole('combobox', { name: 'Proveedor' })
  await u.click(proveedor)
  await u.paste('Acopio')
  await u.click(await screen.findByRole('option', { name: /Acopio/ }))

  fireEvent.change(screen.getByLabelText('Número de factura'), { target: { value: '001' } })
  fireEvent.change(screen.getByLabelText('Detalle'), { target: { value: 'Servicio' } })
  fireEvent.change(screen.getByLabelText('Neto'), { target: { value: '100' } })
  fireEvent.change(screen.getByLabelText('CAE'), { target: { value: 'CAE' } })
  fireEvent.change(screen.getByLabelText('Vencimiento del CAE'), { target: { value: '2026-10-16' } })
  fireEvent.change(screen.getByLabelText('Fecha de factura'), { target: { value: '2026-10-16' } })
  return u
}
it('requiere selección y neto positivo, conserva valores ante error y navega sin GET ni estado del router',async()=>{
  const storage=vi.spyOn(Storage.prototype,'setItem');abrir();const u=userEvent.setup()
  await u.click(screen.getByRole('button',{name:'Guardar factura'}));expect(api.cargarFacturaProveedor).not.toHaveBeenCalled()
  await llenar(u)
  vi.mocked(api.cargarFacturaProveedor).mockRejectedValueOnce(new ErrorHttp(400,{codigo:'numero_duplicado',mensaje:'Número repetido',campo:'numero'}))
  await u.click(screen.getByRole('button',{name:'Guardar factura'}));expect(await screen.findByText('Número repetido')).toBeInTheDocument()
  expect(screen.getByLabelText('Detalle')).toHaveValue('Servicio')
  await u.click(screen.getByRole('button',{name:'Guardar factura'}))
  expect(await screen.findByText('Factura registrada.')).toBeInTheDocument()
  expect(api.cargarFacturaProveedor).toHaveBeenLastCalledWith(expect.objectContaining({archivo:undefined}))
  expect(screen.getByText('$ 121,00')).toBeInTheDocument();expect(api.obtenerFacturaProveedor).not.toHaveBeenCalled()
  expect(screen.getByTestId('ruta')).toHaveTextContent('null');expect(storage).not.toHaveBeenCalled()
})
it('un runtime nuevo sin resultado ejecuta GET y muestra falta de permiso',async()=>{
  abrir('/proveedores/facturas/12')
  expect(await screen.findByText(/Pedile acceso a un administrador/)).toBeInTheDocument()
  expect(api.obtenerFacturaProveedor).toHaveBeenCalledWith(12)
})
it('perder el runtime después de un POST obliga a GET y un Id diferente tampoco reutiliza el DTO',async()=>{
  const primera=abrir();const u=await llenar();await u.click(screen.getByRole('button',{name:'Guardar factura'}))
  await screen.findByText('Factura registrada.');expect(api.obtenerFacturaProveedor).not.toHaveBeenCalled()
  await u.click(screen.getByRole('button',{name:'Abrir otra'}))
  await screen.findByRole('alert')
  expect(vi.mocked(api.obtenerFacturaProveedor).mock.calls).toEqual([[13]])
  expect(screen.getByRole('alert')).toHaveTextContent('Pedile acceso a un administrador')

  primera.unmount()
  abrir('/proveedores/facturas/12')
  await screen.findByRole('alert')
  expect(vi.mocked(api.obtenerFacturaProveedor).mock.calls).toEqual([[13], [12]])
  expect(screen.getByRole('alert')).toHaveTextContent('Pedile acceso a un administrador')
})
it('rechaza neto cero y fechas inválidas antes del POST',async()=>{
  abrir();const u=await llenar();await u.clear(screen.getByLabelText('Neto'));await u.type(screen.getByLabelText('Neto'),'0')
  fireEvent.change(screen.getByLabelText('Fecha de factura'),{target:{value:'2999-01-01'}})
  await u.click(screen.getByRole('button',{name:'Guardar factura'}))
  expect(screen.getByText(/Ingresá un neto mayor a cero/)).toBeInTheDocument();expect(api.cargarFacturaProveedor).not.toHaveBeenCalled()
  expect(screen.getByText(/La fecha no puede ser posterior/)).toBeInTheDocument()
})
it('rechaza un neto negativo sin convertirlo silenciosamente a positivo',async()=>{
  abrir();const u=await llenar();fireEvent.change(screen.getByLabelText('Neto'),{target:{value:'-100'}})
  await u.click(screen.getByRole('button',{name:'Guardar factura'}))
  expect(api.cargarFacturaProveedor).not.toHaveBeenCalled()
  expect(screen.getByText(/Ingresá un neto mayor a cero/)).toBeInTheDocument()
})
it('separa el estado vacío y ofrece una sola parada de teclado para abrir el selector nativo', async () => {
  abrir()
  const u = userEvent.setup()
  const entrada = screen.getByLabelText('Comprobante de la factura (opcional)')
  const elegir = screen.getByRole('button', { name: /^Elegir PDF/ })
  const abrirSelector = vi.spyOn(entrada, 'click')
  expect(entrada).toHaveAttribute('type', 'file')
  expect(entrada).toHaveAttribute('accept', 'application/pdf,.pdf')
  expect(entrada).not.toBeVisible()
  expect(elegir).toHaveAccessibleName('Elegir PDF: Comprobante de la factura (opcional)')
  expect(elegir).toHaveAccessibleDescription('Formato PDF, hasta 10 MB. Sin archivo adjunto')
  expect(entrada).toHaveAccessibleDescription('Formato PDF, hasta 10 MB. Sin archivo adjunto')
  expect(screen.getByText('Sin archivo adjunto')).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Quitar PDF' })).not.toBeInTheDocument()

  screen.getByLabelText('Neto').focus()
  await u.tab()
  expect(elegir).toHaveFocus()
  await u.keyboard('{Enter}')
  expect(abrirSelector).toHaveBeenCalledTimes(1)
  await u.tab()
  expect(screen.getByRole('link', { name: 'Cancelar' })).toHaveFocus()
  expect(api.cargarFacturaProveedor).not.toHaveBeenCalled()
})
it('muestra el nombre real, permite reemplazar el PDF y envía el último archivo sólo al guardar', async () => {
  abrir()
  const u = await llenar()
  const entrada = screen.getByLabelText('Comprobante de la factura (opcional)')
  const original = new File(['%PDF-1.4'], 'Factura_Repuestos_Ruta_2_noviembre_2026_comprobante_original.pdf', { type: 'application/pdf' })
  const reemplazo = new File(['%PDF-1.7'], 'factura_corregida.pdf', { type: 'application/pdf' })
  await u.upload(entrada, original)
  expect(screen.getAllByText(original.name)).toHaveLength(1)
  expect(screen.queryByText('Sin archivo adjunto')).not.toBeInTheDocument()
  const cambiar = screen.getByRole('button', { name: /^Cambiar PDF/ })
  const abrirSelector = vi.spyOn(entrada, 'click')
  await u.click(cambiar)
  expect(abrirSelector).toHaveBeenCalledTimes(1)
  await u.upload(entrada, reemplazo)
  expect(screen.getAllByText(reemplazo.name)).toHaveLength(1)
  expect(screen.queryByText(original.name)).not.toBeInTheDocument()
  expect(api.cargarFacturaProveedor).not.toHaveBeenCalled()

  await u.click(screen.getByRole('button', { name: 'Guardar factura' }))
  expect(vi.mocked(api.cargarFacturaProveedor).mock.calls[0][0].archivo).toBe(reemplazo)
})
it('conserva el PDF anterior al cancelar el diálogo o recibir una selección vacía', async () => {
  abrir()
  const u = await llenar()
  const entrada = screen.getByLabelText('Comprobante de la factura (opcional)')
  const archivo = new File(['%PDF-1.4'], 'factura.pdf', { type: 'application/pdf' })
  await u.upload(entrada, archivo)
  await u.click(screen.getByRole('button', { name: /^Cambiar PDF/ }))
  fireEvent(entrada, new Event('cancel', { bubbles: true }))
  expect(screen.getByText(archivo.name)).toBeVisible()
  fireEvent.change(entrada, { target: { files: [] } })
  expect(screen.getByText(archivo.name)).toBeVisible()
  expect(api.cargarFacturaProveedor).not.toHaveBeenCalled()

  await u.click(screen.getByRole('button', { name: 'Guardar factura' }))
  expect(vi.mocked(api.cargarFacturaProveedor).mock.calls[0][0].archivo).toBe(archivo)
})
it('permite quitar el PDF opcional, limpia el input y guarda sin adjunto', async () => {
  abrir()
  const u = await llenar()
  const entrada = screen.getByLabelText('Comprobante de la factura (opcional)') as HTMLInputElement
  const archivo = new File(['%PDF-1.4'], 'factura.pdf', { type: 'application/pdf' })
  await u.upload(entrada, archivo)
  await u.click(screen.getByRole('button', { name: 'Quitar PDF' }))
  expect(entrada.value).toBe('')
  expect(entrada.files).toHaveLength(0)
  expect(screen.queryByText(archivo.name)).not.toBeInTheDocument()
  expect(screen.getByText('Sin archivo adjunto')).toBeVisible()
  expect(screen.getByRole('button', { name: /^Elegir PDF/ })).toHaveFocus()
  expect(api.cargarFacturaProveedor).not.toHaveBeenCalled()
  await u.click(screen.getByRole('button', { name: 'Guardar factura' }))
  expect(api.cargarFacturaProveedor).toHaveBeenCalledWith(expect.objectContaining({ archivo: undefined }))
})
it('permite volver a elegir el mismo PDF después de quitarlo', async () => {
  abrir()
  const u = await llenar()
  const entrada = screen.getByLabelText('Comprobante de la factura (opcional)')
  const archivo = new File(['%PDF-1.4'], 'factura.pdf', { type: 'application/pdf' })
  await u.upload(entrada, archivo)
  await u.click(screen.getByRole('button', { name: 'Quitar PDF' }))
  await u.upload(entrada, archivo)
  expect(screen.getByText(archivo.name)).toBeVisible()
  await u.click(screen.getByRole('button', { name: 'Guardar factura' }))
  expect(vi.mocked(api.cargarFacturaProveedor).mock.calls[0][0].archivo).toBe(archivo)
})
it.each([0, 10 * 1024 * 1024 + 1])('conserva la validación del PDF de %i bytes y asocia ayuda y error', async tamanio => {
  abrir()
  const u = await llenar()
  const entrada = screen.getByLabelText('Comprobante de la factura (opcional)')
  await u.upload(entrada, new File([new Uint8Array(tamanio)], 'factura.pdf', { type: 'application/pdf' }))
  await u.click(screen.getByRole('button', { name: 'Guardar factura' }))
  const descripcion = 'Formato PDF, hasta 10 MB. factura.pdf El PDF debe tener contenido y pesar como máximo 10 MB.'
  expect(entrada).toHaveAccessibleDescription(descripcion)
  expect(entrada).toHaveAttribute('aria-invalid', 'true')
  expect(screen.getByRole('button', { name: /^Cambiar PDF/ })).toHaveAccessibleDescription(descripcion)
  expect(api.cargarFacturaProveedor).not.toHaveBeenCalled()
})
it.each([false, true])('bloquea el selector y sus acciones mientras guarda (con adjunto: %s)', async conAdjunto => {
  let terminar!: (resultado: Detalle) => void
  vi.mocked(api.cargarFacturaProveedor).mockReturnValueOnce(new Promise(resolve => { terminar = resolve }))
  abrir()
  const u = await llenar()
  const entrada = screen.getByLabelText('Comprobante de la factura (opcional)')
  if (conAdjunto) await u.upload(entrada, new File(['%PDF-1.4'], 'factura.pdf', { type: 'application/pdf' }))
  await u.click(screen.getByRole('button', { name: 'Guardar factura' }))
  expect(entrada).toBeDisabled()
  expect(screen.getByRole('button', { name: conAdjunto ? /^Cambiar PDF/ : /^Elegir PDF/ })).toBeDisabled()
  if (conAdjunto) expect(screen.getByRole('button', { name: 'Quitar PDF' })).toBeDisabled()
  expect(screen.getByRole('button', { name: 'Guardando…' })).toBeDisabled()
  await act(async () => terminar(detalle))
  expect(await screen.findByText('Factura registrada.')).toBeInTheDocument()
})
it('propone hoy Argentina aun cuando UTC ya es mañana',()=>{
  vi.useFakeTimers({toFake:['Date']});vi.setSystemTime(new Date('2026-10-17T01:30:00Z'));abrir()
  expect(screen.getByLabelText('Fecha de factura')).toHaveValue('2026-10-16')
})
it('impide guardar una factura del día siguiente argentino aunque en UTC ya haya comenzado', async () => {
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-17T01:30:00Z'))
  abrir()
  const usuario = await llenar()
  fireEvent.change(screen.getByLabelText('Vencimiento del CAE'), { target: { value: '2026-10-20' } })
  fireEvent.change(screen.getByLabelText('Fecha de factura'), { target: { value: '2026-10-17' } })
  await usuario.click(screen.getByRole('button', { name: 'Guardar factura' }))
  expect(screen.getByText(/La fecha no puede ser posterior/)).toBeInTheDocument()
  expect(api.cargarFacturaProveedor).not.toHaveBeenCalled()
})
it('no muestra selector de liquidación para normal',async()=>{
  abrir();const u=userEvent.setup();await u.type(screen.getByRole('combobox'),'A');await u.click(await screen.findByRole('option'))
  expect(screen.queryByLabelText('Liquidación')).not.toBeInTheDocument()
})
