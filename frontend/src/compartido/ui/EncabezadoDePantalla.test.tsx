import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { EncabezadoDePantalla } from './EncabezadoDePantalla'

/**
 * El título de la pestaña es lo único de esta feature que no se ve operando la aplicación desde
 * adentro: hay que mirar la solapa del navegador. Por eso lleva test (FR-008).
 */
describe('EncabezadoDePantalla', () => {
  function montar(titulo: string) {
    return render(
      <MemoryRouter>
        <EncabezadoDePantalla titulo={titulo} />
      </MemoryRouter>,
    )
  }

  it('fija el título de la pestaña con el nombre del sistema', () => {
    montar('Facturas')

    expect(document.title).toBe('Facturas · Sistema Integral de Gestión')
  })

  it('cambia el título de la pestaña al navegar a otra pantalla', () => {
    const { rerender } = montar('Facturas')

    rerender(
      <MemoryRouter>
        <EncabezadoDePantalla titulo="Viajes" />
      </MemoryRouter>,
    )

    expect(document.title).toBe('Viajes · Sistema Integral de Gestión')
  })

  it('muestra el título de la pantalla como encabezado de nivel 1', () => {
    montar('Panel de vencimientos')

    expect(screen.getByRole('heading', { level: 1, name: 'Panel de vencimientos' })).toBeVisible()
  })

  /**
   * Módulo 12: la acción secundaria tiene su propio espacio, a la izquierda de la principal. Va así y
   * no pasándola por `accionPrincipal`, porque un slot llamado *acción principal* que recibe una
   * secundaria es lo que la convención [007] señala.
   */
  it('dibuja la acción secundaria a la izquierda de la principal', () => {
    render(
      <MemoryRouter>
        <EncabezadoDePantalla
          titulo="Viajes"
          accionSecundaria={<button type="button">Generar reporte</button>}
          accionPrincipal={<button type="button">Nuevo viaje</button>}
        />
      </MemoryRouter>,
    )

    const secundaria = screen.getByRole('button', { name: 'Generar reporte' })
    const principal = screen.getByRole('button', { name: 'Nuevo viaje' })

    expect(secundaria).toBeVisible()
    expect(principal).toBeVisible()
    expect(secundaria.compareDocumentPosition(principal)).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    )
  })

  /** Es aditiva: una pantalla de sólo lectura la recibe y **sigue sin acción primaria**. */
  it('la acción secundaria sola no inventa una primaria', () => {
    render(
      <MemoryRouter>
        <EncabezadoDePantalla
          titulo="Vencimientos"
          accionSecundaria={<button type="button">Generar reporte</button>}
        />
      </MemoryRouter>,
    )

    expect(screen.getAllByRole('button')).toHaveLength(1)
  })
})
