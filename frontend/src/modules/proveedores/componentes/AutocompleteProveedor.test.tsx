import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, expect, it, vi } from 'vitest'
import { AutocompleteProveedor } from './AutocompleteProveedor'
import * as api from '../servicios/facturasProveedor'

vi.mock('../servicios/facturasProveedor',()=>({ buscarProveedores:vi.fn() }))
const p={ id:1,razonSocial:'Acopio',nombreComercial:'Comercial',cuit:'30712345671',tipo:'proveedorNormal' as const,estado:'activo' as const }
const pagina=(items=[p],n=1,total=items.length)=>({items,total,pagina:n,tamanioPagina:20})
beforeEach(()=>{vi.resetAllMocks();vi.mocked(api.buscarProveedores).mockResolvedValue(pagina())})

it('busca sin mínimo, sólo activos, anuncia opciones y permite selección y limpieza con teclado',async()=>{
  const elegir=vi.fn();const u=userEvent.setup();render(<AutocompleteProveedor soloActivos onSeleccionar={elegir} />)
  expect(api.buscarProveedores).not.toHaveBeenCalled()
  await u.type(screen.getByRole('combobox'),'A')
  await screen.findByRole('option',{name:/Acopio.*Comercial/})
  expect(api.buscarProveedores).toHaveBeenLastCalledWith('A',true,1)
  await u.keyboard('{ArrowDown}{Enter}')
  expect(elegir).toHaveBeenLastCalledWith(p)
  await u.type(screen.getByRole('combobox'),'x')
  expect(elegir).toHaveBeenLastCalledWith(null)
  await u.clear(screen.getByRole('combobox'));await u.keyboard('{Escape}{Tab}')
  expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
})

it('pagina resultados y conserva búsqueda sin precargar el padrón',async()=>{
  vi.mocked(api.buscarProveedores).mockImplementation(async(_q,_a,n)=>pagina([{...p,id:n,razonSocial:`Página ${n}`}],n,21))
  const u=userEvent.setup();render(<AutocompleteProveedor soloActivos={false} onSeleccionar={vi.fn()} />)
  await u.type(screen.getByRole('combobox'),'30')
  await screen.findByRole('option',{name:/Página 1/});await u.click(screen.getByRole('button',{name:'Siguientes resultados'}))
  expect(await screen.findByRole('option',{name:/Página 2/})).toBeInTheDocument()
  expect(api.buscarProveedores).toHaveBeenLastCalledWith('30',false,2)
})

it('flecha arriba desde ninguna opción activa selecciona la última',async()=>{
  vi.mocked(api.buscarProveedores).mockResolvedValue(pagina([p,{...p,id:2,razonSocial:'Último'}]))
  const elegir=vi.fn();const u=userEvent.setup();render(<AutocompleteProveedor soloActivos onSeleccionar={elegir} />)
  await u.type(screen.getByRole('combobox'),'A');await screen.findByRole('option',{name:/Último/})
  await u.keyboard('{ArrowUp}{Enter}');expect(elegir).toHaveBeenLastCalledWith(expect.objectContaining({id:2}))
})

it('descarta respuesta tardía y anuncia vacío y error',async()=>{
  let resolver!:(r:ReturnType<typeof pagina>)=>void
  vi.mocked(api.buscarProveedores).mockImplementationOnce(()=>new Promise(r=>{resolver=r}))
  const u=userEvent.setup();render(<AutocompleteProveedor soloActivos onSeleccionar={vi.fn()} />)
  await u.type(screen.getByRole('combobox'),'A');await waitFor(()=>expect(api.buscarProveedores).toHaveBeenCalled())
  vi.mocked(api.buscarProveedores).mockResolvedValue(pagina([]))
  await u.type(screen.getByRole('combobox'),'B')
  expect(await screen.findByText('No hay proveedores que coincidan.')).toHaveAttribute('role','status')
  resolver(pagina());await waitFor(()=>expect(screen.queryByRole('option')).not.toBeInTheDocument())
  vi.mocked(api.buscarProveedores).mockRejectedValue(new Error('Sin conexión'))
  await u.type(screen.getByRole('combobox'),'C')
  expect(await screen.findByText('Sin conexión')).toHaveAttribute('role','alert')
})
