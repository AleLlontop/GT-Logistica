import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { FormularioDocumento } from './FormularioDocumento'
import type { TipoDocumentacion } from './servicioTipos'

const listarTipos = vi.fn()
const cargarDocumento = vi.fn()

vi.mock('./servicioTipos', async () => {
  const real = await vi.importActual<typeof import('./servicioTipos')>('./servicioTipos')
  return { ...real, listarTipos: (...args: unknown[]) => listarTipos(...args) }
})

vi.mock('./servicioDocumentacion', async () => {
  const real = await vi.importActual<
    typeof import('./servicioDocumentacion')
  >('./servicioDocumentacion')
  return { ...real, cargarDocumento: (...args: unknown[]) => cargarDocumento(...args) }
})

const licencia: TipoDocumentacion = {
  id: 1,
  nombre: 'Licencia de conducir',
  diasAvisoVencimiento: 30,
  activo: true,
  documentosAsociados: 0,
}

function renderizar() {
  return render(
    <MemoryRouter>
      <FormularioDocumento
        choferId={7}
        documentosDelChofer={[]}
        onGuardado={() => {}}
        onCancelar={() => {}}
      />
    </MemoryRouter>,
  )
}

async function completar() {
  const usuario = userEvent.setup()

  await screen.findByLabelText('Número')

  await usuario.type(screen.getByLabelText('Número'), 'LIC-123')
  await usuario.type(screen.getByLabelText('Fecha de emisión'), '2026-01-01')
  await usuario.type(screen.getByLabelText('Fecha de vencimiento'), '2027-01-01')

  return usuario
}

describe('FormularioDocumento', () => {
  beforeEach(() => {
    listarTipos.mockReset()
    listarTipos.mockResolvedValue([licencia])
    cargarDocumento.mockReset()
    cargarDocumento.mockResolvedValue({ id: 1 })
  })

  /**
   * FR-018 y SC-004: el estado no es editable por ninguna vía. Este test es el que lo fija: si
   * alguien agrega un control de estado "para poder corregir un caso raro", falla acá.
   */
  it('no expone ningún control para elegir el estado del documento (FR-018, SC-004)', async () => {
    renderizar()

    await screen.findByLabelText('Número')

    // Ni por etiqueta…
    expect(screen.queryByLabelText(/estado/i)).not.toBeInTheDocument()

    // …ni como opción escondida en algún desplegable.
    for (const opcion of screen.queryAllByRole('option')) {
      expect(opcion.textContent).not.toMatch(/al día|próxima a vencer|vencida|vigente/i)
    }

    // El único desplegable es el de tipo de documentación.
    const desplegables = screen.getAllByRole('combobox')
    expect(desplegables).toHaveLength(1)
    expect(desplegables[0]).toHaveAccessibleName('Tipo de documentación')
  })

  it('informa los formatos y el tamaño admitidos antes de elegir un archivo (FR-015a)', async () => {
    renderizar()

    await screen.findByLabelText(/Archivo adjunto/)

    expect(screen.getByText('PDF, JPG o PNG, hasta 10 MB')).toBeInTheDocument()
  })

  it('avisa que no hay tipos activos, con enlace a esa pantalla', async () => {
    listarTipos.mockResolvedValue([])

    renderizar()

    expect(
      await screen.findByText(
        'No hay tipos de documentación activos. Cargá uno desde la pantalla Tipos de documentación.',
      ),
    ).toBeInTheDocument()

    expect(screen.getByRole('link', { name: 'Ir a Tipos de documentación' })).toHaveAttribute(
      'href',
      '/tipos-documentacion',
    )
  })

  it('rechaza un vencimiento anterior a la emisión sin llamar al backend (FR-016)', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await screen.findByLabelText('Número')
    await usuario.type(screen.getByLabelText('Número'), 'LIC-123')
    await usuario.type(screen.getByLabelText('Fecha de emisión'), '2027-01-01')
    await usuario.type(screen.getByLabelText('Fecha de vencimiento'), '2026-01-01')

    await usuario.click(screen.getByRole('button', { name: 'Cargar documento' }))

    expect(
      await screen.findByText('La fecha de vencimiento tiene que ser posterior a la de emisión.'),
    ).toBeInTheDocument()
    expect(cargarDocumento).not.toHaveBeenCalled()
  })

  /**
   * FR-015e: si el archivo no se guarda, el documento no se crea y lo tipeado **sigue en pantalla**
   * para poder reintentar sin volver a completarlo.
   */
  it('conserva lo tipeado cuando el archivo no se pudo guardar (FR-015e)', async () => {
    cargarDocumento.mockRejectedValue(
      new ErrorHttp(500, {
        codigo: 'archivo_no_guardado',
        mensaje:
          'No pudimos guardar el archivo, así que no se guardó nada. Volvé a intentar; los datos que cargaste se conservan.',
      }),
    )

    renderizar()

    const usuario = await completar()
    await usuario.click(screen.getByRole('button', { name: 'Cargar documento' }))

    expect(
      await screen.findByText(
        'No pudimos guardar el archivo, así que no se guardó nada. Volvé a intentar; los datos que cargaste se conservan.',
      ),
    ).toBeInTheDocument()

    // Lo importante: nada se limpió.
    expect(screen.getByLabelText('Número')).toHaveValue('LIC-123')
    expect(screen.getByLabelText('Fecha de emisión')).toHaveValue('2026-01-01')
    expect(screen.getByLabelText('Fecha de vencimiento')).toHaveValue('2027-01-01')
  })

  it('avisa que la carga es una renovación cuando ya hay uno de ese tipo (FR-020a)', async () => {
    render(
      <MemoryRouter>
        <FormularioDocumento
          choferId={7}
          documentosDelChofer={[
            {
              id: 9,
              tipo: { id: 1, nombre: 'Licencia de conducir' },
              numero: 'LIC-VIEJA',
              fechaEmision: '2020-01-01',
              fechaVencimiento: '2025-01-01',
              estado: 'vencida',
              esVigenteDelTipo: true,
              diasHastaVencimiento: -100,
              tieneArchivo: false,
              archivoNombre: null,
            },
          ]}
          onGuardado={() => {}}
          onCancelar={() => {}}
        />
      </MemoryRouter>,
    )

    expect(
      await screen.findByText(/El anterior va a quedar como historial y este pasa a ser el vigente/),
    ).toBeInTheDocument()
  })
})
