import { beforeEach, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { ResultadoPagoProvider } from '../componentes/ResultadoPagoContexto'
import { Bancos } from './Bancos'
import { DetalleBanco } from './DetalleBanco'
import { FormularioBanco } from './FormularioBanco'
import * as api from '../servicios/pagos'
import { errorNombreBanco, trimExterior } from '../servicios/normalizacionPagos'
import type { Banco } from '../servicios/tipos'
vi.mock('../servicios/pagos', async () => ({ ...await vi.importActual('../servicios/pagos'),
  listarBancos: vi.fn(), obtenerBanco: vi.fn(), guardarBanco: vi.fn(), activarBanco: vi.fn() }))
const banco: Banco = { id: 7, nombre: 'Banco Histórico', activo: true, version: 0 }
function montar(ruta = '/pagos/bancos', gestionar = false) {
  return render(<MemoryRouter initialEntries={[ruta]}><ResultadoPagoProvider><Routes>
    <Route path="/pagos/bancos" element={<Bancos puedeGestionar={gestionar} />} />
    <Route path="/pagos/bancos/nuevo" element={<FormularioBanco puedeGestionar={gestionar} />} />
    <Route path="/pagos/bancos/:id" element={<DetalleBanco puedeGestionar={gestionar} />} />
    <Route path="/pagos/bancos/:id/editar" element={<FormularioBanco puedeGestionar={gestionar} />} />
  </Routes></ResultadoPagoProvider></MemoryRouter>)
}
beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(api.listarBancos).mockResolvedValue({ items: [banco], total: 1, pagina: 1, tamanioPagina: 20 })
  vi.mocked(api.obtenerBanco).mockResolvedValue(banco)
  vi.mocked(api.guardarBanco).mockResolvedValue(banco)
  vi.mocked(api.activarBanco).mockImplementation(async (id, activo) => ({ ...banco, id, activo }))
})
it('consulta sin gestión muestra activos/inactivos y conserva enlaces nativos sin acciones', async () => {
  vi.mocked(api.listarBancos).mockResolvedValue({ items: [banco, { ...banco, id: 8, nombre: 'Banco Inactivo', activo: false }], total: 2, pagina: 1, tamanioPagina: 20 })
  montar()
  expect(await screen.findByRole('link', { name: banco.nombre })).toBeVisible()
  expect(screen.getByText('Inactivo')).toBeVisible()
  expect(screen.queryByRole('link', { name: 'Nuevo banco' })).not.toBeInTheDocument()
  const usuario = userEvent.setup()
  await usuario.click(screen.getByRole('link', { name: banco.nombre }))
  expect(await screen.findByRole('heading', { name: banco.nombre })).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Desactivar' })).not.toBeInTheDocument()
  expect(screen.queryByRole('link', { name: 'Editar' })).not.toBeInTheDocument()
})
it('alta conserva nombre significativo, muestra resultado y retira formulario sin GET autorizado adicional', async () => {
  montar('/pagos/bancos/nuevo', true)
  const usuario = userEvent.setup()
  expect(screen.queryByText('Escribí el nombre del banco.')).not.toBeInTheDocument()
  await usuario.type(screen.getByLabelText('Nombre'), '  Banco  Nuevo  ')
  await usuario.click(screen.getByRole('button', { name: 'Guardar' }))
  expect(api.guardarBanco).toHaveBeenCalledWith(undefined, 'Banco  Nuevo', 0)
  expect(await screen.findByRole('heading', { name: banco.nombre })).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Guardar' })).not.toBeInTheDocument()
  expect(api.obtenerBanco).not.toHaveBeenCalled()
})
it('nombre blanco/201 se rechaza sin truncado y 200 después de trim se envía', async () => {
  montar('/pagos/bancos/nuevo', true)
  const usuario = userEvent.setup()
  const nombre = screen.getByLabelText('Nombre')
  await usuario.click(screen.getByRole('button', { name: 'Guardar' }))
  expect(nombre).toHaveAttribute('aria-invalid', 'true')
  fireEvent.change(nombre, { target: { value: ' ' + 'A'.repeat(201) + ' ' } })
  fireEvent.blur(nombre)
  expect(screen.getByText(/No puede superar 200/)).toBeVisible()
  expect(nombre).toHaveValue(' ' + 'A'.repeat(201) + ' ')
  expect(nombre).not.toHaveAttribute('maxlength')
  expect(api.guardarBanco).not.toHaveBeenCalled()
  fireEvent.change(nombre, { target: { value: ' ' + 'A'.repeat(200) + ' ' } })
  await usuario.click(screen.getByRole('button', { name: 'Guardar' }))
  expect(api.guardarBanco).toHaveBeenCalledWith(undefined, 'A'.repeat(200), 0)
})
it('duplicado del servidor aparece vinculado al campo y permite corregir', async () => {
  vi.mocked(api.guardarBanco).mockRejectedValueOnce(new ErrorHttp(400, { codigo: 'datos_invalidos', mensaje: 'Ya existe ese nombre.', campo: 'nombre' }))
  montar('/pagos/bancos/nuevo', true)
  const usuario = userEvent.setup()
  await usuario.type(screen.getByLabelText('Nombre'), 'Duplicado')
  await usuario.click(screen.getByRole('button', { name: 'Guardar' }))
  expect(await screen.findByText('Ya existe ese nombre.')).toBeVisible()
  expect(screen.getByLabelText('Nombre')).toHaveAttribute('aria-describedby', 'nombre-banco-error')
  await usuario.clear(screen.getByLabelText('Nombre')); await usuario.type(screen.getByLabelText('Nombre'), 'Otro')
  await usuario.click(screen.getByRole('button', { name: 'Guardar' }))
  expect(await screen.findByRole('heading', { name: banco.nombre })).toBeVisible()
})
it('edición envía versión, recarga explícitamente un conflicto y no cambia Activo por editar', async () => {
  vi.mocked(api.guardarBanco).mockRejectedValueOnce(new ErrorHttp(409, { codigo: 'datos_invalidos', mensaje: 'El banco cambió.' }))
  montar('/pagos/bancos/7/editar', true)
  expect(await screen.findByDisplayValue(banco.nombre)).toBeVisible()
  const usuario = userEvent.setup()
  await usuario.click(screen.getByRole('button', { name: 'Guardar' }))
  expect(await screen.findByText('El banco cambió.')).toBeVisible()
  expect(api.guardarBanco).toHaveBeenCalledWith(7, banco.nombre, 0)
  vi.mocked(api.obtenerBanco).mockResolvedValue({ ...banco, version: 1, activo: false })
  await usuario.click(screen.getByRole('button', { name: 'Volver a cargar banco' }))
  await waitFor(() => expect(screen.getByRole('button', { name: 'Guardar' })).toBeEnabled())
  await usuario.click(screen.getByRole('button', { name: 'Guardar' }))
  expect(api.guardarBanco).toHaveBeenLastCalledWith(7, banco.nombre, 1)
  expect(api.activarBanco).not.toHaveBeenCalled()
})
it('desactivar confirma en diálogo, cancelar/Escape no escribe, después permite activar', async () => {
  montar('/pagos/bancos/7', true)
  const usuario = userEvent.setup()
  const disparador = await screen.findByRole('button', { name: 'Desactivar' })
  await usuario.click(disparador)
  let dialogo = screen.getByRole('dialog', { name: 'Desactivar banco' })
  expect(dialogo).toHaveFocus()
  await usuario.keyboard('{Escape}')
  await waitFor(() => expect(disparador).toHaveFocus())
  expect(api.activarBanco).not.toHaveBeenCalled()
  await usuario.click(disparador)
  dialogo = screen.getByRole('dialog', { name: 'Desactivar banco' })
  await usuario.click(within(dialogo).getByRole('button', { name: 'Cancelar' }))
  expect(api.activarBanco).not.toHaveBeenCalled()
  await usuario.click(disparador)
  await usuario.click(screen.getByRole('button', { name: 'Confirmar desactivación' }))
  expect(await screen.findByRole('button', { name: 'Activar' })).toBeVisible()
  expect(api.activarBanco).toHaveBeenCalledWith(7, false)
  await usuario.click(screen.getByRole('button', { name: 'Activar' }))
  expect(await screen.findByRole('button', { name: 'Desactivar' })).toBeVisible()
  expect(api.activarBanco).toHaveBeenLastCalledWith(7, true)
})
it('recargar el resultado descarta memoria y respeta 403 de consulta', async () => {
  const vista = montar('/pagos/bancos/nuevo', true)
  const usuario = userEvent.setup()
  await usuario.type(screen.getByLabelText('Nombre'), 'Banco Nuevo')
  await usuario.click(screen.getByRole('button', { name: 'Guardar' }))
  expect(await screen.findByRole('heading', { name: banco.nombre })).toBeVisible()
  vista.unmount()
  vi.mocked(api.obtenerBanco).mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin autorización' }))
  montar('/pagos/bancos/7', true)
  expect(await screen.findByRole('alert')).toHaveTextContent(api.SIN_PERMISO_PAGOS)
  expect(screen.queryByRole('heading', { name: banco.nombre })).not.toBeInTheDocument()
})
it('ruta de alta sin gestionar informa autorización sin ofrecer guardar ni escribir', () => {
  montar('/pagos/bancos/nuevo')
  expect(screen.getByRole('alert')).toHaveTextContent(api.SIN_PERMISO_PAGOS)
  expect(screen.queryByRole('button', { name: 'Guardar' })).not.toBeInTheDocument()
  expect(api.guardarBanco).not.toHaveBeenCalled()
})
it('lista tiene vacío/error/403 explícitos y pagina sin traer padrones completos', async () => {
  vi.mocked(api.listarBancos).mockResolvedValueOnce({ items: [banco], total: 21, pagina: 1, tamanioPagina: 20 })
    .mockResolvedValueOnce({ items: [], total: 21, pagina: 2, tamanioPagina: 20 })
  montar('/pagos/bancos', true)
  const usuario = userEvent.setup()
  await usuario.click(await screen.findByRole('button', { name: 'Siguiente' }))
  expect(await screen.findByText('No hay bancos registrados.')).toBeVisible()
  expect(api.listarBancos).toHaveBeenLastCalledWith(2)
})
it('trim Unicode conserva espacios internos y FEFF, con paridad exterior de .NET', () => {
  expect(trimExterior('\u0085 \u00a0Banco  Interno\u3000')).toBe('Banco  Interno')
  expect(trimExterior('\ufeffBanco\ufeff')).toBe('\ufeffBanco\ufeff')
  expect(errorNombreBanco('\u0085' + 'A'.repeat(200) + '\u0085')).toBe('')
  expect(errorNombreBanco('A'.repeat(201))).not.toBe('')
})

it.each([403, 500])('listado anuncia carga y error HTTP %s sin exponer filas', async estado => {
  vi.mocked(api.listarBancos).mockRejectedValue(new ErrorHttp(estado, { codigo: 'sin_permiso', mensaje: 'No pudimos consultar los bancos.' }))
  montar()
  expect(screen.getByRole('status')).toHaveTextContent('Cargando bancos')
  expect(await screen.findByRole('alert')).toHaveTextContent(estado === 403 ? api.SIN_PERMISO_PAGOS : 'No pudimos consultar los bancos.')
  expect(screen.queryByRole('link', { name: banco.nombre })).not.toBeInTheDocument()
})
