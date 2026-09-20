import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { GenerarReporte } from './GenerarReporte'

const generarReporte = vi.fn()

vi.mock('../servicios/servicioReportes', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioReportes')>(
    '../servicios/servicioReportes',
  )

  return {
    ...real,
    generarReporte: (...args: unknown[]) => generarReporte(...args),
  }
})

/**
 * Todo por rol, etiqueta y texto (convención [007]): lo que este test fija es el comportamiento, no
 * el marcado.
 */
describe('GenerarReporte', () => {
  beforeEach(() => {
    generarReporte.mockReset().mockResolvedValue({ estado: 'entregado', entrega: 'abierto' })
  })

  function montar(props: Partial<Parameters<typeof GenerarReporte>[0]> = {}) {
    return render(
      <GenerarReporte reporte="viajes" cantidadDeFilas={12} puedeEmitir {...props} />,
    )
  }

  // ── El permiso (FR-013) ───────────────────────────────────────────────────────────────────────

  /** **Sin el permiso no se dibuja nada**, ni siquiera un botón deshabilitado. */
  it('sin el permiso no dibuja nada', () => {
    const { container } = montar({ puedeEmitir: false })

    expect(container).toBeEmptyDOMElement()
    expect(screen.queryByRole('button', { name: 'Generar reporte' })).toBeNull()
  })

  it('con el permiso ofrece la acción', () => {
    montar()

    expect(screen.getByRole('button', { name: 'Generar reporte' })).toBeEnabled()
  })

  // ── Cero filas (FR-003) ───────────────────────────────────────────────────────────────────────

  it('con cero filas la acción queda deshabilitada y se explica por qué', () => {
    montar({ cantidadDeFilas: 0 })

    const boton = screen.getByRole('button', { name: 'Generar reporte' })

    expect(boton).toBeDisabled()
    expect(screen.getByText('No hay filas para reportar.')).toBeVisible()

    // La explicación está enlazada al control, no sólo al lado.
    expect(boton).toHaveAccessibleDescription('No hay filas para reportar.')
  })

  // ── El diálogo (FR-002, SC-001) ───────────────────────────────────────────────────────────────

  it('ofrece exactamente dos formatos, en el orden PDF → Excel', async () => {
    const usuario = userEvent.setup()
    montar()

    await usuario.click(screen.getByRole('button', { name: 'Generar reporte' }))

    const dialogo = screen.getByRole('dialog')

    expect(
      within(dialogo)
        .getAllByRole('button')
        .map((boton) => boton.textContent),
    ).toEqual(['PDF', 'Excel', 'Cancelar'])
  })

  /** **Dos clics**: la acción y el formato. El botón del formato ejecuta. */
  it('el botón del formato genera el reporte en dos clics', async () => {
    const usuario = userEvent.setup()
    montar({ filtros: { clienteId: 7 } })

    await usuario.click(screen.getByRole('button', { name: 'Generar reporte' }))
    await usuario.click(screen.getByRole('button', { name: 'PDF' }))

    expect(generarReporte).toHaveBeenCalledWith('viajes', 'pdf', { clienteId: 7 })
  })

  it('el segundo formato pide Excel', async () => {
    const usuario = userEvent.setup()
    montar()

    await usuario.click(screen.getByRole('button', { name: 'Generar reporte' }))
    await usuario.click(screen.getByRole('button', { name: 'Excel' }))

    expect(generarReporte).toHaveBeenCalledWith('viajes', 'excel', {})
  })

  it('cancelar cierra sin generar nada', async () => {
    const usuario = userEvent.setup()
    montar()

    const disparador = screen.getByRole('button', { name: 'Generar reporte' })

    await usuario.click(disparador)
    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(screen.queryByRole('dialog')).toBeNull()
    expect(generarReporte).not.toHaveBeenCalled()
    await waitFor(() => expect(disparador).toHaveFocus())
  })

  it('Escape cierra sin generar nada y devuelve el foco al disparador', async () => {
    const usuario = userEvent.setup()
    montar()

    const disparador = screen.getByRole('button', { name: 'Generar reporte' })

    await usuario.click(disparador)
    await usuario.keyboard('{Escape}')

    expect(screen.queryByRole('dialog')).toBeNull()
    expect(generarReporte).not.toHaveBeenCalled()
    await waitFor(() => expect(disparador).toHaveFocus())
  })

  // ── Mientras genera (FR-004) ──────────────────────────────────────────────────────────────────

  it('mientras genera, la acción queda deshabilitada y la región viva lo anuncia', async () => {
    const usuario = userEvent.setup()

    let resolver: (valor: unknown) => void = () => {}
    generarReporte.mockReturnValue(new Promise((resuelve) => (resolver = resuelve)))

    montar()

    await usuario.click(screen.getByRole('button', { name: 'Generar reporte' }))
    await usuario.click(screen.getByRole('button', { name: 'PDF' }))

    expect(screen.getByRole('button', { name: 'Generar reporte' })).toBeDisabled()
    expect(screen.getByRole('status')).toHaveTextContent('Generando el reporte…')

    resolver({ estado: 'entregado', entrega: 'abierto' })

    await waitFor(() =>
      expect(screen.getByRole('status')).toHaveTextContent(
        'Se abrió el reporte en una pestaña nueva.',
      ),
    )
  })

  it('cuando las emergentes están bloqueadas, anuncia la descarga', async () => {
    const usuario = userEvent.setup()
    generarReporte.mockResolvedValue({ estado: 'entregado', entrega: 'descargado' })

    montar()

    await usuario.click(screen.getByRole('button', { name: 'Generar reporte' }))
    await usuario.click(screen.getByRole('button', { name: 'PDF' }))

    await waitFor(() =>
      expect(screen.getByRole('status')).toHaveTextContent('Se descargó el reporte.'),
    )
  })

  // ── El 409 del tope (FR-016) ──────────────────────────────────────────────────────────────────

  /** **Con el mensaje que llega del servidor**, no con uno compuesto acá. */
  it('el tope superado se muestra con el mensaje del servidor', async () => {
    const usuario = userEvent.setup()

    generarReporte.mockResolvedValue({
      estado: 'tope_superado',
      mensaje:
        'El filtro dejó 7.412 filas y el tope de un reporte es 5.000. Acotá los filtros y volvé a intentar.',
      filas: 7412,
      tope: 5000,
    })

    montar()

    await usuario.click(screen.getByRole('button', { name: 'Generar reporte' }))
    await usuario.click(screen.getByRole('button', { name: 'PDF' }))

    await waitFor(() =>
      expect(screen.getByRole('alert')).toHaveTextContent(
        'El filtro dejó 7.412 filas y el tope de un reporte es 5.000. Acotá los filtros y volvé a intentar.',
      ),
    )
  })

  // ── FR-017: un rechazo no es un callejón sin salida ───────────────────────────────────────────

  /**
   * **El caso de FR-017.** Una generación que falla muestra el mensaje, deja la acción accionable
   * otra vez, y un segundo intento que sí funciona entrega el archivo — **sin que los filtros que el
   * componente recibió hayan cambiado**.
   */
  it('después de un fallo se puede reintentar y el segundo intento entrega el archivo', async () => {
    const usuario = userEvent.setup()
    const filtros = { clienteId: 7, estado: 'enCurso' }

    generarReporte.mockRejectedValueOnce(new Error('se cayó la red'))

    montar({ filtros })

    await usuario.click(screen.getByRole('button', { name: 'Generar reporte' }))
    await usuario.click(screen.getByRole('button', { name: 'PDF' }))

    await waitFor(() =>
      expect(screen.getByRole('alert')).toHaveTextContent(
        'No pudimos generar el reporte. Volvé a intentar en unos minutos.',
      ),
    )

    // La acción vuelve a quedar accionable.
    const disparador = screen.getByRole('button', { name: 'Generar reporte' })
    expect(disparador).toBeEnabled()

    generarReporte.mockResolvedValue({ estado: 'entregado', entrega: 'descargado' })

    await usuario.click(disparador)
    await usuario.click(screen.getByRole('button', { name: 'Excel' }))

    await waitFor(() =>
      expect(screen.getByRole('status')).toHaveTextContent('Se descargó el reporte.'),
    )

    // El rechazo anterior ya no está, y los filtros son los mismos que llegaron por props.
    expect(screen.queryByRole('alert')).toBeNull()
    expect(generarReporte).toHaveBeenLastCalledWith('viajes', 'excel', filtros)
    expect(filtros).toEqual({ clienteId: 7, estado: 'enCurso' })
  })
})
