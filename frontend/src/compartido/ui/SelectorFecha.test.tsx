import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { SelectorFecha } from './SelectorFecha'

describe('SelectorFecha', () => {
  it('muestra la fecha seleccionada en los tres desplegables', () => {
    render(<SelectorFecha id="fechaPrueba" value="2026-10-14" onChange={vi.fn()} />)

    expect(screen.getByLabelText('Día')).toHaveValue('14')
    expect(screen.getByLabelText('Mes')).toHaveValue('10')
    expect(screen.getByLabelText('Año')).toHaveValue('2026')
  })

  it('notifica la fecha en formato ISO al cambiar el día', async () => {
    const usuario = userEvent.setup()
    const alCambiar = vi.fn()

    render(<SelectorFecha id="fechaPrueba" value="2026-10-14" onChange={alCambiar} />)

    await usuario.selectOptions(screen.getByLabelText('Día'), '20')

    expect(alCambiar).toHaveBeenCalledWith('2026-10-20')
  })

  it('ajusta los días máximos al cambiar a un mes con menos días (ej. 31 de marzo a abril)', async () => {
    const usuario = userEvent.setup()
    const alCambiar = vi.fn()

    render(<SelectorFecha id="fechaPrueba" value="2026-03-31" onChange={alCambiar} />)

    // Cambiar a abril (mes 4, 30 días)
    await usuario.selectOptions(screen.getByLabelText('Mes'), '4')

    expect(alCambiar).toHaveBeenCalledWith('2026-04-30')
  })

  it('permite limpiar la fecha seleccionando una opción vacía', async () => {
    const usuario = userEvent.setup()
    const alCambiar = vi.fn()

    render(<SelectorFecha id="fechaPrueba" value="2026-10-14" onChange={alCambiar} />)

    await usuario.selectOptions(screen.getByLabelText('Día'), '')

    expect(alCambiar).toHaveBeenCalledWith('')
  })
})
