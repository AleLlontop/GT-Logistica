import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { SeccionNumerada } from './SeccionNumerada'

describe('SeccionNumerada colapsable', () => {
  it('se expande y colapsa al hacer clic en el encabezado', () => {
    render(
      <SeccionNumerada numero={1} titulo="Datos del comprobante">
        <p>Campos del formulario</p>
      </SeccionNumerada>,
    )

    // Por defecto inicia abierto
    expect(screen.getByText('Campos del formulario')).toBeInTheDocument()

    const encabezado = screen.getByRole('heading', { name: 'Datos del comprobante' })
    expect(encabezado).toBeInTheDocument()

    // Click colapsa
    fireEvent.click(encabezado)

    expect(screen.queryByText('Campos del formulario')).not.toBeInTheDocument()

    // Click expande de nuevo
    fireEvent.click(encabezado)

    expect(screen.getByText('Campos del formulario')).toBeInTheDocument()
  })
})
