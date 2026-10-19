import { beforeEach, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, within, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { ChequesPorVencer } from './ChequesPorVencer'
import { DetalleCheque } from './DetalleCheque'
import { ResultadoPagoProvider } from '../componentes/ResultadoPagoContexto'
import type { DetalleCheque as Cheque, ImporteDecimal } from '../servicios/tipos'
import * as api from '../servicios/pagos'
vi.mock('../servicios/pagos', async () => ({ ...await vi.importActual('../servicios/pagos'), obtenerCheque: vi.fn(), cambiarEstadoCheque: vi.fn(), consultarCheques: vi.fn(), bancosHistoricos: vi.fn() }))
const cheque: Cheque = { id: 2, numeroCheque: '000-01', banco: { id: 1, nombre: 'Banco', activo: false }, fechaEmision: '2026-10-17', fechaVencimiento: '2026-10-18', importe: '121.00' as ImporteDecimal, estado: 'pendiente', situacionVencimiento: 'vencido', ordenPagoProveedor: { id: 25, numero: 'OPP-25' }, detalle: null, historial: [] }
beforeEach(() => { vi.resetAllMocks(); vi.mocked(api.obtenerCheque).mockResolvedValue(cheque); vi.mocked(api.bancosHistoricos).mockResolvedValue({ items: [cheque.banco] }); vi.mocked(api.consultarCheques).mockResolvedValue({ items: [cheque], total: 1, pagina: 1, tamanioPagina: 20 }) })
function detalle(cambiar = false, consultar = false) { render(<MemoryRouter initialEntries={['/pagos/cheques/2']}><ResultadoPagoProvider><Routes><Route path="/pagos/cheques/:id" element={<DetalleCheque puedeCambiar={cambiar} puedeConsultarOrden={consultar} />} /></Routes></ResultadoPagoProvider></MemoryRouter>) }
it('muestra estado y situación separados, banco histórico y seis estados en select nativo', async () => {
  render(<MemoryRouter><ChequesPorVencer /></MemoryRouter>)
  expect(await screen.findByRole('link', { name: '000-01' })).toBeVisible()
  expect(screen.getByRole('cell', { name: 'Pendiente' })).toBeVisible(); expect(screen.getByRole('cell', { name: 'Vencido' })).toBeVisible()
  expect(await screen.findByRole('option', { name: 'Banco — Inactivo' })).toBeVisible()
  expect(within(screen.getByLabelText('Estado')).getAllByRole('option')).toHaveLength(7)
})
it('consulta sola no muestra cambio ni enlace a OPP', async () => {
  detalle(); expect(await screen.findByRole('heading', { name: 'Cheque 000-01' })).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Cambiar estado' })).not.toBeInTheDocument(); expect(screen.queryByRole('link', { name: 'OPP-25' })).not.toBeInTheDocument()
})
it('diálogo guarda sólo tras acción explícita y muestra historial releído', async () => {
  vi.mocked(api.cambiarEstadoCheque).mockResolvedValue({ ...cheque, estado: 'entregado', historial: [{ id: 1, estadoAnterior: 'pendiente', estadoNuevo: 'entregado', usuario: { id: 1, nombre: 'Administración' }, ocurridoEn: '2026-10-18T12:00:00Z' }] })
  detalle(true, true); const u = userEvent.setup(); await u.click(await screen.findByRole('button', { name: 'Cambiar estado' }))
  const dialogo = screen.getByRole('dialog', { name: 'Cambiar estado del cheque' }); expect(dialogo).toHaveFocus(); expect(api.cambiarEstadoCheque).not.toHaveBeenCalled()
  await u.selectOptions(within(dialogo).getByLabelText('Estado nuevo'), 'entregado'); await u.click(within(dialogo).getByRole('button', { name: 'Guardar cambio' }))
  expect(await screen.findByText('Estado del cheque actualizado.')).toBeVisible(); expect(api.cambiarEstadoCheque).toHaveBeenCalledExactlyOnceWith(2, 'pendiente', 'entregado')
  expect(screen.getByText(/Pendiente → Entregado/)).toBeVisible(); expect(screen.getByRole('link', { name: 'OPP-25' })).toHaveAttribute('href', '/pagos/ordenes/25')
})

it('cancelar devuelve foco y no envía cambios', async () => {
  detalle(true); const u = userEvent.setup(); const accion = await screen.findByRole('button', { name: 'Cambiar estado' })
  await u.click(accion); await u.click(screen.getByRole('button', { name: 'Cancelar' }))
  expect(api.cambiarEstadoCheque).not.toHaveBeenCalled(); await waitFor(() => expect(accion).toHaveFocus())
})
it('conflicto real bloquea reenvío y pide actualizar antes de una nueva transición', async () => {
  vi.mocked(api.cambiarEstadoCheque).mockRejectedValue(new ErrorHttp(409, { codigo: 'estado_cheque_modificado', mensaje: 'El estado cambió.' }))
  detalle(true); const u = userEvent.setup(); await u.click(await screen.findByRole('button', { name: 'Cambiar estado' }))
  await u.selectOptions(screen.getByLabelText('Estado nuevo'), 'entregado'); await u.click(screen.getByRole('button', { name: 'Guardar cambio' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('El estado cambió.')
  expect(screen.getByRole('button', { name: 'Guardar cambio' })).toBeDisabled()
  await u.click(screen.getByRole('button', { name: 'Actualizar información' })); await waitFor(() => expect(api.obtenerCheque).toHaveBeenCalledTimes(2))
})
it('estado final no ofrece cambios y403 de detalle no expone datos', async () => {
  vi.mocked(api.obtenerCheque).mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso' }))
  detalle(true); expect(await screen.findByRole('alert')).toHaveTextContent(api.SIN_PERMISO_PAGOS)
  expect(screen.queryByText('000-01')).not.toBeInTheDocument(); expect(screen.queryByRole('button', { name: 'Cambiar estado' })).not.toBeInTheDocument()
})
it('filtros conservados entre páginas, rango inválido no sale y limpiar restaura universo base', async () => {
  vi.mocked(api.consultarCheques).mockImplementation(async f => ({ items: [], total: 21, pagina: f.pagina, tamanioPagina: 20 }))
  render(<MemoryRouter><ChequesPorVencer /></MemoryRouter>); const u = userEvent.setup()
  await screen.findByText('No hay cheques que coincidan con los filtros.')
  await u.selectOptions(screen.getByLabelText('Estado'), 'anulado')
  fireEvent.change(screen.getByLabelText('Vencimiento desde'), { target: { value: '2026-10-19' } })
  fireEvent.change(screen.getByLabelText('Vencimiento hasta'), { target: { value: '2026-10-18' } })
  await u.click(screen.getByRole('button', { name: 'Filtrar' })); expect(api.consultarCheques).toHaveBeenCalledOnce()
  expect(screen.getByText('Desde no puede ser posterior a Hasta.')).toBeVisible()
  fireEvent.change(screen.getByLabelText('Vencimiento hasta'), { target: { value: '2026-12-31' } })
  await u.click(screen.getByRole('button', { name: 'Filtrar' })); await waitFor(() => expect(api.consultarCheques).toHaveBeenLastCalledWith(expect.objectContaining({ estado: 'anulado', hastaVencimiento: '2026-12-31', pagina: 1 })))
  await u.click(screen.getByRole('button', { name: 'Siguiente' })); await waitFor(() => expect(api.consultarCheques).toHaveBeenLastCalledWith(expect.objectContaining({ estado: 'anulado', pagina: 2 })))
  await u.click(screen.getByRole('button', { name: 'Limpiar filtros' })); await waitFor(() => expect(api.consultarCheques).toHaveBeenLastCalledWith({ pagina: 1 }))
})
it('estado final conserva consulta e historial sin ofrecer una salida', async () => {
  vi.mocked(api.obtenerCheque).mockResolvedValue({ ...cheque, estado: 'rechazado' })
  detalle(true); expect(await screen.findByText('Rechazado')).toBeVisible(); expect(screen.queryByRole('button', { name: 'Cambiar estado' })).not.toBeInTheDocument()
})
