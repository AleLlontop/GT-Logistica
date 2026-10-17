import { render,screen,fireEvent } from '@testing-library/react'
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
afterEach(()=>vi.useRealTimers())
async function llenar(){
  const u=userEvent.setup();await u.type(screen.getByRole('combobox'),'Acopio');await u.click(await screen.findByRole('option',{name:/Acopio/}))
  await u.type(screen.getByLabelText('Número de factura'),'001');await u.type(screen.getByLabelText('Detalle'),'Servicio')
  await u.type(screen.getByLabelText('Neto'),'100');await u.type(screen.getByLabelText('CAE'),'CAE')
  fireEvent.change(screen.getByLabelText('Vencimiento del CAE'),{target:{value:'2026-10-16'}})
  fireEvent.change(screen.getByLabelText('Fecha de factura'),{target:{value:'2026-10-16'}})
  return u
}
it('requiere selección y neto positivo, conserva valores ante error y navega sin GET ni estado del router',async()=>{
  const storage=vi.spyOn(Storage.prototype,'setItem');abrir();const u=userEvent.setup()
  await u.click(screen.getByRole('button',{name:'Guardar factura'}));expect(api.cargarFacturaProveedor).not.toHaveBeenCalled()
  await llenar()
  vi.mocked(api.cargarFacturaProveedor).mockRejectedValueOnce(new ErrorHttp(400,{codigo:'numero_duplicado',mensaje:'Número repetido',campo:'numero'}))
  await u.click(screen.getByRole('button',{name:'Guardar factura'}));expect(await screen.findByText('Número repetido')).toBeInTheDocument()
  expect(screen.getByLabelText('Detalle')).toHaveValue('Servicio')
  await u.click(screen.getByRole('button',{name:'Guardar factura'}))
  expect(await screen.findByText('Factura registrada.')).toBeInTheDocument()
  expect(screen.getByText('$ 121,00')).toBeInTheDocument();expect(api.obtenerFacturaProveedor).not.toHaveBeenCalled()
  expect(screen.getByTestId('ruta')).toHaveTextContent('null');expect(storage).not.toHaveBeenCalled();storage.mockRestore()
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
  await screen.findByText(/Pedile acceso a un administrador/);expect(api.obtenerFacturaProveedor).toHaveBeenCalledWith(13)
  primera.unmount();vi.mocked(api.obtenerFacturaProveedor).mockClear();abrir('/proveedores/facturas/12')
  await screen.findByText(/Pedile acceso a un administrador/);expect(api.obtenerFacturaProveedor).toHaveBeenCalledWith(12)
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
it('permite quitar el PDF opcional antes de guardar',async()=>{
  abrir();const u=await llenar();await u.upload(screen.getByLabelText('PDF'),new File(['%PDF-1.4'],'factura.pdf',{type:'application/pdf'}))
  await u.click(screen.getByRole('button',{name:'Quitar PDF'}));await u.click(screen.getByRole('button',{name:'Guardar factura'}))
  expect(api.cargarFacturaProveedor).toHaveBeenCalledWith(expect.objectContaining({archivo:undefined}))
})
it('propone hoy Argentina aun cuando UTC ya es mañana',()=>{
  vi.useFakeTimers({toFake:['Date']});vi.setSystemTime(new Date('2026-10-17T01:30:00Z'));abrir()
  expect(screen.getByLabelText('Fecha de factura')).toHaveValue('2026-10-16')
})
it('no muestra selector de liquidación para normal',async()=>{
  abrir();const u=userEvent.setup();await u.type(screen.getByRole('combobox'),'A');await u.click(await screen.findByRole('option'))
  expect(screen.queryByLabelText('Liquidación')).not.toBeInTheDocument()
})
