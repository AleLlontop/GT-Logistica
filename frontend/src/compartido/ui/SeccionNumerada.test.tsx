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

    const boton = screen.getByRole('button', { name: /Datos del comprobante/i })
    expect(boton).toHaveAttribute('aria-expanded', 'true')

    // Click colapsa
    fireEvent.click(boton)

    expect(screen.queryByText('Campos del formulario')).not.toBeInTheDocument()
    expect(boton).toHaveAttribute('aria-expanded', 'false')

    // Click expande de nuevo
    fireEvent.click(boton)

    expect(screen.getByText('Campos del formulario')).toBeInTheDocument()
    expect(boton).toHaveAttribute('aria-expanded', 'true')
  })
})
