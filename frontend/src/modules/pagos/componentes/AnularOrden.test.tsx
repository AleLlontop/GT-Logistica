import { beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { AnularOrden } from './AnularOrden'
import * as api from '../servicios/pagos'
import type { DetalleOrden, ImporteDecimal } from '../servicios/tipos'
vi.mock('../servicios/pagos', async () => ({ ...await vi.importActual('../servicios/pagos'), anularOrden: vi.fn() }))
const ordenPrueba: DetalleOrden = { id: 25, numero: 'OPP-25', fecha: '2026-10-18', importeTotal: '121.00' as ImporteDecimal, estado: 'pendiente', detalle: null,
  proveedor: { id: 1, razonSocial: 'Proveedor', cuit: '30712345671', tipo: 'proveedorNormal', activoAlGenerar: true, activoActual: true },
  registradaPor: { id: 1, nombre: 'Administración' }, registradaEn: '2026-10-18T12:00:00Z', facturas: [], medios: [], caja: null, anulacion: null }
beforeEach(() => vi.resetAllMocks())
it('cancelar el diálogo no envía y devuelve foco al disparador', async () => {
  render(<AnularOrden orden={ordenPrueba} alAnular={vi.fn()} alActualizar={vi.fn()} />); const u = userEvent.setup()
  const disparador = screen.getByRole('button', { name: 'Anular OPP' }); await u.click(disparador)
  expect(screen.getByRole('dialog', { name: 'Anular OPP-25' })).toHaveFocus()
  await u.click(screen.getByRole('button', { name: 'Cancelar' })); expect(api.anularOrden).not.toHaveBeenCalled(); await waitFor(() => expect(disparador).toHaveFocus())
})
it('motivo y confirmación explícita antes del único POST, resultado íntegro en callback', async () => {
  const actualizada = { ...ordenPrueba, estado: 'anulada' as const }; vi.mocked(api.anularOrden).mockResolvedValue(actualizada)
  const alAnular = vi.fn(); render(<AnularOrden orden={ordenPrueba} alAnular={alAnular} alActualizar={vi.fn()} />); const u = userEvent.setup()
  await u.click(screen.getByRole('button', { name: 'Anular OPP' })); const dialogo = screen.getByRole('dialog')
  await u.click(within(dialogo).getByRole('button', { name: 'Confirmar anulación' })); expect(api.anularOrden).not.toHaveBeenCalled(); expect(screen.getByText('Ingresá el motivo de anulación.')).toBeVisible()
  await u.type(screen.getByLabelText('Motivo'), '  Corregir importe  '); expect(api.anularOrden).not.toHaveBeenCalled()
  await u.click(screen.getByRole('button', { name: 'Confirmar anulación' })); await waitFor(() => expect(alAnular).toHaveBeenCalledWith(actualizada))
  expect(api.anularOrden).toHaveBeenCalledExactlyOnceWith(25, { claveOperacion: expect.any(String), motivo: 'Corregir importe', confirmado: true })
})

it('respuesta incierta congela motivo y repite exactamente la misma intención', async () => {
  vi.mocked(api.anularOrden).mockRejectedValueOnce(new ErrorHttp(0, { codigo: 'sin_conexion', mensaje: 'Respuesta perdida.' })).mockResolvedValue({ ...ordenPrueba, estado: 'anulada' })
  const alAnular = vi.fn(); render(<AnularOrden orden={ordenPrueba} alAnular={alAnular} alActualizar={vi.fn()} />); const u = userEvent.setup()
  await u.click(screen.getByRole('button', { name: 'Anular OPP' })); await u.type(screen.getByLabelText('Motivo'), 'Corregir')
  await u.click(screen.getByRole('button', { name: 'Confirmar anulación' })); expect(await screen.findByRole('alert')).toHaveTextContent('Respuesta perdida.')
  expect(screen.getByLabelText('Motivo')).toBeDisabled(); expect(screen.getByRole('button', { name: 'Cancelar' })).toBeDisabled()
  const entrada = vi.mocked(api.anularOrden).mock.calls[0][1]
  await u.click(screen.getByRole('button', { name: 'Reintentar anulación' })); await waitFor(() => expect(alAnular).toHaveBeenCalledOnce())
  expect(vi.mocked(api.anularOrden).mock.calls[1][1]).toBe(entrada)
})
it('conflicto real obliga actualizar y403 informa falta de permiso sin proponer reintento', async () => {
  vi.mocked(api.anularOrden).mockRejectedValueOnce(new ErrorHttp(409, { codigo: 'estado_cheque_modificado', mensaje: 'El cheque cambió.' }))
  const actualizar = vi.fn(); render(<AnularOrden orden={ordenPrueba} alAnular={vi.fn()} alActualizar={actualizar} />); const u = userEvent.setup()
  await u.click(screen.getByRole('button', { name: 'Anular OPP' })); await u.type(screen.getByLabelText('Motivo'), 'Corregir')
  await u.click(screen.getByRole('button', { name: 'Confirmar anulación' })); expect(await screen.findByRole('alert')).toHaveTextContent('El cheque cambió.')
  expect(screen.getByRole('button', { name: 'Confirmar anulación' })).toBeDisabled()
  await u.click(screen.getByRole('button', { name: 'Actualizar información' })); expect(actualizar).toHaveBeenCalledOnce()
  vi.mocked(api.anularOrden).mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso' }))
  await u.click(screen.getByRole('button', { name: 'Anular OPP' })); await u.type(screen.getByLabelText('Motivo'), 'Corregir')
  await u.click(screen.getByRole('button', { name: 'Confirmar anulación' })); expect(await screen.findByRole('alert')).toHaveTextContent(api.SIN_PERMISO_PAGOS)
  expect(screen.getByRole('button', { name: 'Confirmar anulación' })).toBeDisabled(); expect(screen.queryByRole('button', { name: 'Reintentar anulación' })).not.toBeInTheDocument()
})
