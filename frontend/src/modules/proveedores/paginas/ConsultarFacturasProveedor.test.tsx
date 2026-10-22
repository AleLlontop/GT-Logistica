import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, expect, it, vi } from 'vitest'
import { ConsultarFacturasProveedor } from './ConsultarFacturasProveedor'
import * as api from '../servicios/facturasProveedor'
import { ErrorHttp } from '../../../compartido/clienteHttp'

vi.mock('../servicios/facturasProveedor', () => ({ consultarFacturasProveedor: vi.fn(), buscarProveedores: vi.fn() }))
const fila = { id: 1, numero: '0001-12', fecha: '2026-10-16', proveedorId: 5, proveedorRazonSocial: 'Razón histórica', proveedorCuit: '30712345671', importeTotal: '100000000000000.01', estado: 'pendiente' }
const p = { id: 5, razonSocial: 'Razón actual', cuit: '30712345671', nombreComercial: 'Comercial', tipo: 'proveedorNormal', estado: 'inactivo' }
beforeEach(() => {
  vi.resetAllMocks(); vi.mocked(api.consultarFacturasProveedor).mockResolvedValue({ items: [fila] as never, total: 1, pagina: 1, tamanioPagina: 20 })
  vi.mocked(api.buscarProveedores).mockResolvedValue({ items: [p] as never, total: 1, pagina: 1, tamanioPagina: 20 })
})
function abrir() { return render(<MemoryRouter><ConsultarFacturasProveedor /></MemoryRouter>) }
it('muestra snapshot, fecha local y dinero exacto con enlace real y sin primaria artificial', async () => {
  abrir(); expect(await screen.findByRole('link', { name: '0001-12' })).toHaveAttribute('href', '/proveedores/facturas/1')
  expect(screen.getByText('Razón histórica')).toBeInTheDocument(); expect(screen.getByText('16/10/2026')).toBeInTheDocument()
  expect(screen.getByText('$ 100.000.000.000.000,01')).toBeInTheDocument(); expect(screen.queryByText(/Exportar|Nueva factura/)).not.toBeInTheDocument()
})
it('combina filtros por Id con inactivos y limpiar elimina también el texto del autocomplete', async () => {
  abrir(); await screen.findByText('Razón histórica'); const u = userEvent.setup()
  await u.type(screen.getByLabelText('Proveedor'), 'Comercial'); await u.click(await screen.findByRole('option', { name: /Razón actual/ }))
  expect(api.buscarProveedores).toHaveBeenCalledWith('Comercial', false, 1)
  fireEvent.change(screen.getByLabelText('Desde'), { target: { value: '2026-10-01' } }); fireEvent.change(screen.getByLabelText('Hasta'), { target: { value: '2026-10-16' } })
  await u.selectOptions(screen.getByLabelText('Estado'), 'pendiente'); await u.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
  await waitFor(() => expect(api.consultarFacturasProveedor).toHaveBeenLastCalledWith({ desde: '2026-10-01', hasta: '2026-10-16', proveedorId: 5, estado: 'pendiente' }, 1))
  expect(screen.getByText(/Proveedor: Razón actual/)).toBeInTheDocument()
  await u.click(screen.getByRole('button', { name: 'Limpiar filtros' })); expect(screen.getByLabelText('Proveedor')).toHaveValue('')
  await waitFor(() => expect(api.consultarFacturasProveedor).toHaveBeenLastCalledWith({}, 1))
})
it('informa rango invertido y no aplica texto sin selección como filtro de proveedor', async () => {
  abrir(); await screen.findByText('Razón histórica'); const u = userEvent.setup()
  fireEvent.change(screen.getByLabelText('Desde'), { target: { value: '2026-10-17' } }); fireEvent.change(screen.getByLabelText('Hasta'), { target: { value: '2026-10-01' } })
  await u.click(screen.getByRole('button', { name: 'Aplicar filtros' })); expect(await screen.findByText(/La fecha desde no puede ser posterior/)).toBeInTheDocument(); expect(api.consultarFacturasProveedor).toHaveBeenCalledTimes(1)
  fireEvent.change(screen.getByLabelText('Desde'), { target: { value: '' } }); fireEvent.change(screen.getByLabelText('Hasta'), { target: { value: '' } })
  await u.type(screen.getByLabelText('Proveedor'), 'Sin seleccionar'); await u.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
  await waitFor(() => expect(api.consultarFacturasProveedor).toHaveBeenLastCalledWith({}, 1))
})
it('distingue vacío inicial, sin coincidencias y falta de permiso real del backend', async () => {
  vi.mocked(api.consultarFacturasProveedor).mockResolvedValue({ items: [], total: 0, pagina: 1, tamanioPagina: 20 }); abrir()
  expect(await screen.findByText('Todavía no hay facturas de proveedores registradas.')).toHaveAttribute('role', 'status')
  const u = userEvent.setup(); await u.selectOptions(screen.getByLabelText('Estado'), 'pendiente'); await u.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
  expect(await screen.findByText(/Ninguna factura coincide con los filtros aplicados/)).toHaveAttribute('role', 'status')
  vi.mocked(api.consultarFacturasProveedor).mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso' }))
  await u.click(screen.getByRole('button', { name: 'Limpiar filtros' })); expect(await screen.findByText(/Pedile acceso a un administrador/)).toBeInTheDocument()
  expect(screen.queryByText('Todavía no hay facturas de proveedores registradas.')).not.toBeInTheDocument()
})
it('pagina de a 20, conserva filtros y presenta centavos grandes distintos', async () => {
  vi.mocked(api.consultarFacturasProveedor).mockResolvedValue({ items: Array.from({ length: 20 }, (_, i) => ({ ...fila, id: i + 1, numero: `F-${i}`, importeTotal: i === 1 ? '100000000000000.02' : fila.importeTotal })) as never, total: 21, pagina: 1, tamanioPagina: 20 })
  abrir(); await screen.findByText('F-0'); expect(screen.getAllByRole('row')).toHaveLength(21); expect(screen.getByText('$ 100.000.000.000.000,02')).toBeInTheDocument()
  const u = userEvent.setup(); await u.click(screen.getByRole('button', { name: 'Siguiente' })); await waitFor(() => expect(api.consultarFacturasProveedor).toHaveBeenLastCalledWith({}, 2))
})
it('un error de carga permite reintentar sin mostrar el resultado anterior', async () => {
  vi.mocked(api.consultarFacturasProveedor).mockRejectedValueOnce(new Error('Sin conexión')); abrir()
  expect(await screen.findByText('Sin conexión')).toBeInTheDocument(); const u = userEvent.setup(); await u.click(screen.getByRole('button', { name: 'Reintentar' }))
  expect(await screen.findByText('Razón histórica')).toBeInTheDocument()
})

it('[014] consulta Pagadas con filtro y estado real sin cambiar dinero histórico', async () => {
  vi.mocked(api.consultarFacturasProveedor).mockResolvedValue({ items: [{ ...fila, estado: 'pagada' }] as never, total: 1, pagina: 1, tamanioPagina: 20 })
  abrir(); await screen.findByText('Razón histórica')
  const u = userEvent.setup()
  await u.selectOptions(screen.getByLabelText('Estado'), 'pagada')
  await u.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
  await waitFor(() => expect(api.consultarFacturasProveedor).toHaveBeenLastCalledWith({ estado: 'pagada' }, 1))
  expect(screen.getByText(/Estado: Pagada/)).toBeVisible()
  expect(screen.getByText('$ 100.000.000.000.000,01')).toBeVisible()
})
