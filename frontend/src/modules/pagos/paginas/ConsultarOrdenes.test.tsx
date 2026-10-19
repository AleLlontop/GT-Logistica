import { beforeEach, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { ConsultarOrdenes } from './ConsultarOrdenes'
import * as api from '../servicios/pagos'
vi.mock('../servicios/pagos', async () => ({ ...await vi.importActual('../servicios/pagos'), consultarOrdenes: vi.fn(), proveedoresHistoricos: vi.fn() }))
beforeEach(() => { vi.resetAllMocks(); vi.mocked(api.consultarOrdenes).mockResolvedValue({ items: [], total: 0, pagina: 1, tamanioPagina: 20 }) })
it('filtra por fecha de negocio y estado, conserva filtros al paginar y limpia', async () => {
  vi.mocked(api.consultarOrdenes).mockImplementation(async f => ({ items: [], total: 21, pagina: f.pagina, tamanioPagina: 20 }))
  render(<MemoryRouter><ConsultarOrdenes /></MemoryRouter>); const u = userEvent.setup()
  await screen.findByText('No hay órdenes que coincidan con los filtros.')
  fireEvent.change(screen.getByLabelText('Desde'), { target: { value: '2026-10-01' } })
  fireEvent.change(screen.getByLabelText('Hasta'), { target: { value: '2026-10-18' } })
  await u.selectOptions(screen.getByLabelText('Estado'), 'anulada'); await u.click(screen.getByRole('button', { name: 'Filtrar' }))
  await waitFor(() => expect(api.consultarOrdenes).toHaveBeenLastCalledWith(expect.objectContaining({ desde: '2026-10-01', hasta: '2026-10-18', estado: 'anulada', pagina: 1 })))
  await u.click(screen.getByRole('button', { name: 'Siguiente' }))
  await waitFor(() => expect(api.consultarOrdenes).toHaveBeenLastCalledWith(expect.objectContaining({ desde: '2026-10-01', estado: 'anulada', pagina: 2 })))
  await u.click(screen.getByRole('button', { name: 'Limpiar filtros' }))
  await waitFor(() => expect(api.consultarOrdenes).toHaveBeenLastCalledWith({ pagina: 1 }))
})
it('rango invertido queda en campo sin enviar consulta inválida', async () => {
  render(<MemoryRouter><ConsultarOrdenes /></MemoryRouter>); const u = userEvent.setup()
  await screen.findByText('No hay órdenes que coincidan con los filtros.')
  fireEvent.change(screen.getByLabelText('Desde'), { target: { value: '2026-10-19' } }); fireEvent.change(screen.getByLabelText('Hasta'), { target: { value: '2026-10-18' } })
  await u.click(screen.getByRole('button', { name: 'Filtrar' }))
  expect(screen.getByText('Desde no puede ser posterior a Hasta.')).toBeVisible(); expect(api.consultarOrdenes).toHaveBeenCalledOnce()
})
