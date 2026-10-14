import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { FichaSeccion } from './Ficha'

describe('FichaSeccion colapsable', () => {
  it('se expande y colapsa al hacer clic en el encabezado', () => {
    render(
      <FichaSeccion titulo="Comprobante" id="titulo-comprobante">
        <p>Contenido detallado del comprobante</p>
      </FichaSeccion>,
    )

    // Por defecto inicia abierto
    expect(screen.getByText('Contenido detallado del comprobante')).toBeInTheDocument()

    const encabezado = screen.getByRole('heading', { name: 'Comprobante' })
    expect(encabezado).toHaveAttribute('aria-expanded', 'true')

    // Click colapsa
    fireEvent.click(encabezado)

    expect(screen.queryByText('Contenido detallado del comprobante')).not.toBeInTheDocument()
    expect(encabezado).toHaveAttribute('aria-expanded', 'false')

    // Click expande de nuevo
    fireEvent.click(encabezado)

    expect(screen.getByText('Contenido detallado del comprobante')).toBeInTheDocument()
    expect(encabezado).toHaveAttribute('aria-expanded', 'true')
  })
})
