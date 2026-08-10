import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { TiposDocumentacion } from './TiposDocumentacion'
import type { TipoDocumentacion } from './servicioTipos'

const listarTipos = vi.fn()
const crearTipo = vi.fn()
const modificarTipo = vi.fn()

vi.mock('./servicioTipos', async () => {
  const real = await vi.importActual<typeof import('./servicioTipos')>('./servicioTipos')

  return {
    ...real,
    listarTipos: (...args: unknown[]) => listarTipos(...args),
    crearTipo: (...args: unknown[]) => crearTipo(...args),
    modificarTipo: (...args: unknown[]) => modificarTipo(...args),
  }
})

function tipo(
  id: number,
  nombre: string,
  ambito: TipoDocumentacion['ambito'] = 'chofer',
  documentosAsociados = 0,
): TipoDocumentacion {
  return { id, nombre, diasAvisoVencimiento: 30, ambito, activo: true, documentosAsociados }
}

describe('TiposDocumentacion — el ámbito del Módulo 4', () => {
  beforeEach(() => {
    listarTipos.mockReset()
    listarTipos.mockResolvedValue([tipo(1, 'Licencia de conducir')])
    crearTipo.mockReset()
    crearTipo.mockResolvedValue(tipo(2, 'VTV', 'vehiculo'))
    modificarTipo.mockReset()
    modificarTipo.mockResolvedValue(tipo(1, 'Licencia de conducir'))
  })

  /** FR-017: el ámbito es un campo obligatorio del formulario, con las dos opciones del contrato. */
  it('el formulario tiene el campo Ámbito con sus dos opciones (FR-017)', async () => {
    render(<TiposDocumentacion />)

    const ambito = await screen.findByLabelText('Ámbito')

    expect(ambito).toBeRequired()
    expect(within(ambito).getByRole('option', { name: 'Chofer' })).toBeInTheDocument()
    expect(within(ambito).getByRole('option', { name: 'Vehículo' })).toBeInTheDocument()
  })

  it('manda el ámbito elegido al crear un tipo (FR-017)', async () => {
    const usuario = userEvent.setup()
    render(<TiposDocumentacion />)

    await screen.findByLabelText('Ámbito')

    await usuario.type(screen.getByLabelText('Nombre'), 'VTV')
    await usuario.selectOptions(screen.getByLabelText('Ámbito'), 'vehiculo')
    await usuario.click(screen.getByRole('button', { name: 'Cargar tipo' }))

    expect(crearTipo).toHaveBeenCalledWith(
      expect.objectContaining({ nombre: 'VTV', ambito: 'vehiculo' }),
    )
  })

  /** El listado muestra el ámbito de cada tipo, con su etiqueta en castellano. */
  it('el listado muestra el ámbito de cada tipo', async () => {
    listarTipos.mockResolvedValue([
      tipo(1, 'Licencia de conducir', 'chofer'),
      tipo(2, 'VTV', 'vehiculo'),
    ])

    render(<TiposDocumentacion />)

    const tabla = await screen.findByRole('table')

    expect(within(tabla).getByText('Chofer')).toBeInTheDocument()
    expect(within(tabla).getByText('Vehículo')).toBeInTheDocument()
  })

  /** El filtro por ámbito dice qué está mostrando: ninguna fila queda oculta en silencio. */
  it('el filtro por ámbito dice qué está filtrando', async () => {
    listarTipos.mockResolvedValue([
      tipo(1, 'Licencia de conducir', 'chofer'),
      tipo(2, 'VTV', 'vehiculo'),
    ])

    const usuario = userEvent.setup()
    render(<TiposDocumentacion />)

    await screen.findByRole('table')

    expect(screen.getByText('Mostrando los tipos de los dos ámbitos.')).toBeInTheDocument()

    await usuario.selectOptions(screen.getByLabelText('Filtrar por ámbito'), 'vehiculo')

    expect(
      await screen.findByText('Mostrando sólo los de ámbito Vehículo.'),
    ).toBeInTheDocument()

    const tabla = screen.getByRole('table')
    expect(within(tabla).queryByText('Licencia de conducir')).not.toBeInTheDocument()
    expect(within(tabla).getByText('VTV')).toBeInTheDocument()
  })

  /**
   * FR-017d: cambiar el ámbito de un tipo que ya tiene documentos se rechaza, y el mensaje dice
   * cuántos son. Si el cambio pasara, esos documentos quedarían colgando de un tipo que su propio
   * módulo ya no ofrece.
   */
  it('muestra el rechazo cuando el ámbito no se puede cambiar (FR-017d)', async () => {
    listarTipos.mockResolvedValue([tipo(1, 'Seguro', 'vehiculo', 3)])

    modificarTipo.mockRejectedValue(
      new ErrorHttp(409, {
        codigo: 'ambito_no_modificable',
        mensaje: 'No se puede cambiar el ámbito: 3 documento(s) ya usan este tipo.',
        campo: 'ambito',
      }),
    )

    const usuario = userEvent.setup()
    render(<TiposDocumentacion />)

    await screen.findByRole('table')

    await usuario.click(screen.getByRole('button', { name: 'Editar' }))
    await usuario.selectOptions(screen.getByLabelText('Ámbito'), 'chofer')
    await usuario.click(screen.getByRole('button', { name: 'Guardar cambios' }))

    expect(
      await screen.findByText('No se puede cambiar el ámbito: 3 documento(s) ya usan este tipo.'),
    ).toBeInTheDocument()

    expect(screen.getByLabelText('Ámbito')).toHaveAttribute('aria-invalid', 'true')
  })

  /** Al editar, el formulario se puebla con el ámbito que el tipo ya tiene. */
  it('al editar, el ámbito se puebla con el valor actual', async () => {
    listarTipos.mockResolvedValue([tipo(1, 'VTV', 'vehiculo')])

    const usuario = userEvent.setup()
    render(<TiposDocumentacion />)

    await screen.findByRole('table')

    await usuario.click(screen.getByRole('button', { name: 'Editar' }))

    expect(screen.getByLabelText('Ámbito')).toHaveValue('vehiculo')
  })
})
