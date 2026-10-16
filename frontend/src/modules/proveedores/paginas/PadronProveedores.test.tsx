import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { PadronProveedores } from './PadronProveedores'
import { FormularioProveedor } from './FormularioProveedor'
import { DetalleProveedor } from './DetalleProveedor'
import * as api from '../servicios/proveedores'

vi.mock('../servicios/proveedores', async importar => ({
  ...await importar<typeof import('../servicios/proveedores')>(),
  listarProveedores: vi.fn(), obtenerProveedor: vi.fn(), guardarProveedor: vi.fn(),
  cambiarEstadoProveedor: vi.fn(), listarExternos: vi.fn(),
}))
const normal = { id: 1, razonSocial: 'Acopio', cuit: '30712345671', nombreComercial: null, tipo: 'proveedorNormal' as const, estado: 'activo' as const, telefono: null, email: null, direccion: null, transportistaId: null, version: 1 }
const pagina = <T,>(items: T[], total = items.length, numero = 1) => ({ items, total, pagina: numero, tamanioPagina: 20 })
function abrir(ruta = '/proveedores') {
  return render(<MemoryRouter initialEntries={[ruta]}><Routes>
    <Route path="/proveedores" element={<PadronProveedores />} />
    <Route path="/proveedores/nuevo" element={<FormularioProveedor />} />
    <Route path="/proveedores/:id/editar" element={<FormularioProveedor />} />
    <Route path="/proveedores/:id" element={<DetalleProveedor />} />
  </Routes></MemoryRouter>)
}

describe('Administración de proveedores US1', () => {
  beforeEach(() => {
    vi.resetAllMocks()
    vi.mocked(api.listarProveedores).mockResolvedValue(pagina([normal]))
    vi.mocked(api.obtenerProveedor).mockResolvedValue(normal)
    vi.mocked(api.guardarProveedor).mockResolvedValue(normal)
    vi.mocked(api.cambiarEstadoProveedor).mockResolvedValue({ ...normal, estado: 'inactivo', version: 2 })
    vi.mocked(api.listarExternos).mockResolvedValue(pagina([{ id: 21, razonSocial: 'Externo 21', cuit: '20123456786' }]))
  })

  it('busca y combina filtros, muestra enlace real y distingue vacío', async () => {
    const u = userEvent.setup(); abrir()
    expect(await screen.findByRole('link', { name: 'Acopio' })).toHaveAttribute('href', '/proveedores/1')
    await u.type(screen.getByLabelText('Buscar proveedores'), 'Acopio')
    await u.selectOptions(screen.getByLabelText('Estado'), 'inactivo')
    await u.selectOptions(screen.getByLabelText('Tipo'), 'proveedorNormal')
    vi.mocked(api.listarProveedores).mockResolvedValue(pagina([]))
    await u.click(screen.getByRole('button', { name: 'Buscar' }))
    await waitFor(() => expect(api.listarProveedores).toHaveBeenLastCalledWith(expect.objectContaining({ q: 'Acopio', estado: 'inactivo', tipo: 'proveedorNormal', pagina: 1 })))
    expect(await screen.findByText('No hay proveedores que coincidan con los filtros.')).toBeInTheDocument()
  })

  it('sale del alta normal al detalle y conserva campos ante rechazo', async () => {
    const u = userEvent.setup(); abrir('/proveedores/nuevo')
    await u.type(screen.getByLabelText('Razón social'), 'Acopio')
    await u.type(screen.getByLabelText('CUIT'), '30-71234567-1')
    vi.mocked(api.guardarProveedor).mockRejectedValueOnce(new ErrorHttp(400, { codigo: 'cuit_duplicado', mensaje: 'Ese CUIT ya está registrado.', campo: 'cuit' }))
    await u.click(screen.getByRole('button', { name: 'Guardar proveedor' }))
    expect(await screen.findByText('Ese CUIT ya está registrado.')).toBeInTheDocument()
    expect(screen.getByLabelText('Razón social')).toHaveValue('Acopio')
    await u.click(screen.getByRole('button', { name: 'Guardar proveedor' }))
    expect(await screen.findByRole('heading', { name: 'Acopio' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Guardar proveedor' })).not.toBeInTheDocument()
  })

  it('baja cancelada no escribe, confirmada sí; reactivar es separado', async () => {
    const u = userEvent.setup(); abrir('/proveedores/1')
    await u.click(await screen.findByRole('button', { name: 'Dar de baja' }))
    await u.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Cancelar' }))
    expect(api.cambiarEstadoProveedor).not.toHaveBeenCalled()
    await u.click(screen.getByRole('button', { name: 'Dar de baja' }))
    await u.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Confirmar baja' }))
    await u.click(await screen.findByRole('button', { name: 'Reactivar' }))
    expect(api.cambiarEstadoProveedor).toHaveBeenLastCalledWith(1, 2, true)
  })

  it('fletero no ofrece inputs de identidad/contacto propios y envía sólo vínculo', async () => {
    const u = userEvent.setup(); abrir('/proveedores/nuevo')
    await u.selectOptions(screen.getByLabelText('Tipo'), 'fletero')
    await u.selectOptions(await screen.findByLabelText('Transportista'), '21')
    expect(screen.queryByLabelText('CUIT')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Teléfono')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Email')).not.toBeInTheDocument()
    await u.click(screen.getByRole('button', { name: 'Guardar proveedor' }))
    expect(api.guardarProveedor).toHaveBeenCalledWith(undefined, expect.objectContaining({ tipo: 'fletero', transportistaId: 21 }))
    expect(vi.mocked(api.guardarProveedor).mock.calls[0][1]).not.toHaveProperty('cuit')
  })

  it('permite elegir el transportista 21, volver y buscar vuelve a página 1 limpiando selección', async () => {
    const u = userEvent.setup()
    const veinte = Array.from({ length: 20 }, (_, i) => ({ id: i + 1, razonSocial: `Externo ${i + 1}`, cuit: '20123456786' }))
    vi.mocked(api.listarExternos).mockImplementation(async (_q, p) => pagina(p === 2 ? [{ id: 21, razonSocial: 'Externo 21', cuit: '20123456786' }] : veinte, 21, p))
    abrir('/proveedores/nuevo'); await u.selectOptions(screen.getByLabelText('Tipo'), 'fletero')
    await u.click(await screen.findByRole('button', { name: 'Siguiente' }))
    await screen.findByRole('option', { name: /Externo 21/ })
    await u.selectOptions(screen.getByLabelText('Transportista'), '21')
    expect(screen.getByLabelText('Transportista')).toHaveValue('21')
    await u.click(screen.getByRole('button', { name: 'Anterior' }))
    await waitFor(() => expect(screen.getByLabelText('Transportista')).toHaveValue(''))
    await u.type(screen.getByLabelText('Buscar transportista'), 'Otro')
    await waitFor(() => expect(api.listarExternos).toHaveBeenLastCalledWith('Otro', 1))
    expect(screen.getAllByRole('option').length).toBeLessThanOrEqual(23)
  })

  it('descarta respuestas tardías y anuncia carga/vacío/error recuperable', async () => {
    const u = userEvent.setup()
    let resolver!: (valor: ReturnType<typeof pagina<{ id: number; razonSocial: string; cuit: string }>>) => void
    vi.mocked(api.listarExternos).mockImplementationOnce(() => new Promise(r => { resolver = r }))
    abrir('/proveedores/nuevo'); await u.selectOptions(screen.getByLabelText('Tipo'), 'fletero')
    expect(screen.getByText('Cargando transportistas…')).toHaveAttribute('role', 'status')
    vi.mocked(api.listarExternos).mockResolvedValue(pagina([]))
    await u.type(screen.getByLabelText('Buscar transportista'), 'z')
    expect(await screen.findByText('No hay transportistas externos que coincidan.')).toHaveAttribute('role', 'status')
    resolver(pagina([{ id: 99, razonSocial: 'Respuesta vieja', cuit: '20123456786' }]))
    await waitFor(() => expect(screen.queryByRole('option', { name: /Respuesta vieja/ })).not.toBeInTheDocument())
    vi.mocked(api.listarExternos).mockRejectedValue(new Error('Sin conexión'))
    await u.type(screen.getByLabelText('Buscar transportista'), 'x')
    expect(await screen.findByText('Sin conexión')).toHaveAttribute('role', 'alert')
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Guardar proveedor' })).toBeDisabled()
  })

  it('edición fletero inicia búsqueda por CUIT y muestra contacto del origen sin edición', async () => {
    vi.mocked(api.obtenerProveedor).mockResolvedValue({ ...normal, tipo: 'fletero', transportistaId: 21, cuit: '20123456786', telefono: '341', email: 'origen@example.com' })
    abrir('/proveedores/1/editar')
    expect(await screen.findByText('origen@example.com')).toBeInTheDocument()
    await waitFor(() => expect(api.listarExternos).toHaveBeenCalledWith('20123456786', 1))
    expect(screen.queryByLabelText('Email')).not.toBeInTheDocument()
  })

  it('403 explica a quién solicitar permiso', async () => {
    vi.mocked(api.listarProveedores).mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso' }))
    abrir()
    expect(await screen.findByText(/Pedile acceso a un administrador/)).toHaveAttribute('role', 'alert')
  })
})
