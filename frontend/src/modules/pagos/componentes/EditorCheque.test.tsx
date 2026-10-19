import { expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { EditorCheque } from './EditorCheque'
const bancos = [{ id: 1, nombre: 'Banco Activo', activo: true }, { id: 2, nombre: 'Banco Inactivo', activo: false }]
it.each(['proveedorNormal', 'fletero'] as const)('conserva identidad textual, sólo bancos activos y vencimiento de %s', async tipoProveedor => {
  const guardar = vi.fn(); const usuario = userEvent.setup()
  render(<MemoryRouter><EditorCheque hoy="2026-10-18" tipoProveedor={tipoProveedor} bancos={bancos} onGuardar={guardar} onCancelar={vi.fn()} /></MemoryRouter>)
  expect(screen.queryByRole('option', { name: 'Banco Inactivo' })).not.toBeInTheDocument()
  await usuario.selectOptions(screen.getByLabelText('Banco'), '1')
  await usuario.type(screen.getByLabelText('Número de cheque'), '  001-00 / A  ')
  fireEvent.change(screen.getByLabelText('Vencimiento'), { target: { value: '2026-10-18' } })
  await usuario.type(screen.getByLabelText('Importe'), '121,00')
  await usuario.click(screen.getByRole('button', { name: 'Agregar medio' }))
  if (tipoProveedor === 'fletero') {
    expect(guardar).not.toHaveBeenCalled()
    expect(screen.getByText('El cheque del Fletero debe ser diferido.')).toBeVisible()
    fireEvent.change(screen.getByLabelText('Vencimiento'), { target: { value: '2026-10-19' } })
    await usuario.click(screen.getByRole('button', { name: 'Agregar medio' }))
  }
  expect(guardar).toHaveBeenCalledWith(expect.objectContaining({ tipo: 'cheque', bancoId: 1, numeroCheque: '001-00 / A', importe: '121.00' }))
})
it('número de 201 no se trunca y cancelar no guarda', async () => {
  const guardar = vi.fn(); const cancelar = vi.fn(); const usuario = userEvent.setup()
  render(<EditorCheque hoy="2026-10-18" tipoProveedor="proveedorNormal" bancos={bancos} onGuardar={guardar} onCancelar={cancelar} />)
  fireEvent.change(screen.getByLabelText('Número de cheque'), { target: { value: 'X'.repeat(201) } })
  fireEvent.blur(screen.getByLabelText('Número de cheque'))
  expect(screen.getByLabelText('Número de cheque')).toHaveValue('X'.repeat(201))
  expect(screen.getByText(/No puede superar 200/)).toBeVisible()
  await usuario.click(screen.getByRole('button', { name: 'Cancelar' }))
  expect(cancelar).toHaveBeenCalledOnce(); expect(guardar).not.toHaveBeenCalled()
})
