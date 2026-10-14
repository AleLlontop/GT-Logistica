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

    const boton = screen.getByRole('button', { name: /Comprobante/i })
    expect(boton).toHaveAttribute('aria-expanded', 'true')

    // Click colapsa
    fireEvent.click(boton)

    expect(screen.queryByText('Contenido detallado del comprobante')).not.toBeInTheDocument()
    expect(boton).toHaveAttribute('aria-expanded', 'false')

    // Click expande de nuevo
    fireEvent.click(boton)

    expect(screen.getByText('Contenido detallado del comprobante')).toBeInTheDocument()
    expect(boton).toHaveAttribute('aria-expanded', 'true')
  })
})
