import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { EmpresaEmisora } from './EmpresaEmisora'
import type { EmpresaEmisora as Configuracion } from '../servicios/servicioEmpresaEmisora'

const obtenerEmpresaEmisora = vi.fn()
const guardarEmpresaEmisora = vi.fn()
const subirLogo = vi.fn()
const quitarLogo = vi.fn()

vi.mock('../servicios/servicioEmpresaEmisora', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioEmpresaEmisora')>(
    '../servicios/servicioEmpresaEmisora',
  )

  return {
    ...real,
    obtenerEmpresaEmisora: () => obtenerEmpresaEmisora(),
    guardarEmpresaEmisora: (...args: unknown[]) => guardarEmpresaEmisora(...args),
    subirLogo: (...args: unknown[]) => subirLogo(...args),
    quitarLogo: () => quitarLogo(),
  }
})

const SIN_CONFIGURAR: Configuracion = {
  configurada: false,
  faltantes: ['razón social', 'CUIT', 'domicilio', 'condición de IVA'],
  razonSocial: null,
  cuit: null,
  domicilio: null,
  condicionIva: null,
  ingresosBrutos: null,
  inicioActividades: null,
  puntoDeVenta: null,
  cbu: null,
  telefono: null,
  email: null,
  logo: null,
}

const CONFIGURADA: Configuracion = {
  ...SIN_CONFIGURAR,
  configurada: true,
  faltantes: [],
  razonSocial: 'G&T Logística S.R.L.',
  cuit: '30712345671',
  domicilio: 'Av. Pellegrini 1234, Rosario',
  condicionIva: 'IVA Responsable Inscripto',
}

async function completar(usuario: ReturnType<typeof userEvent.setup>) {
  await usuario.type(screen.getByLabelText('Razón social'), 'G&T Logística S.R.L.')
  await usuario.type(screen.getByLabelText('CUIT (con o sin guiones)'), '30-71234567-1')
  await usuario.type(screen.getByLabelText('Domicilio'), 'Av. Pellegrini 1234, Rosario')
  await usuario.type(screen.getByLabelText('Condición de IVA'), 'IVA Responsable Inscripto')
}

describe('EmpresaEmisora', () => {
  beforeEach(() => {
    obtenerEmpresaEmisora.mockReset().mockResolvedValue(SIN_CONFIGURAR)
    guardarEmpresaEmisora.mockReset().mockResolvedValue(CONFIGURADA)
    subirLogo.mockReset()
    quitarLogo.mockReset().mockResolvedValue(undefined)
  })

  /** US1 esc. 1: con la configuración vacía la pantalla dice qué falta, no queda en blanco. */
  it('avisa que todavía no está configurada con el texto del contrato', async () => {
    render(<EmpresaEmisora />)

    expect(
      await screen.findByText(
        'La empresa emisora todavía no está configurada. Completá al menos la razón social, el CUIT, ' +
          'el domicilio y la condición de IVA para poder emitir facturas.',
      ),
    ).toBeInTheDocument()
  })

  it('no repite el aviso cuando ya está configurada', async () => {
    obtenerEmpresaEmisora.mockResolvedValue(CONFIGURADA)

    render(<EmpresaEmisora />)

    await screen.findByDisplayValue('G&T Logística S.R.L.')
    expect(screen.queryByText(/todavía no está configurada/)).not.toBeInTheDocument()
  })

  /**
   * El guardado **no cambia de pantalla** y se anuncia con `role="status"`: es un resultado que
   * aparece sin que la pantalla cambie, y quien usa lector de pantalla tiene que enterarse
   * (convención [003]).
   */
  it('anuncia el guardado sin cambiar de pantalla', async () => {
    const usuario = userEvent.setup()
    render(<EmpresaEmisora />)

    await screen.findByLabelText('Razón social')
    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }))

    const aviso = await screen.findByText('Los datos de la empresa emisora quedaron guardados.')

    expect(aviso).toBeInTheDocument()
    expect(aviso).toHaveAttribute('role', 'status')

    // Sigue en la misma pantalla, con el formulario a la vista y los datos devueltos por el servidor.
    expect(screen.getByRole('heading', { name: 'Empresa emisora' })).toBeInTheDocument()
    expect(screen.getByLabelText('Domicilio')).toHaveValue('Av. Pellegrini 1234, Rosario')
  })

  /** El CUIT viaja como se tipeó: normalizarlo es responsabilidad del backend (FR-002). */
  it('manda el CUIT tal como se escribió y los opcionales vacíos como nulos', async () => {
    const usuario = userEvent.setup()
    render(<EmpresaEmisora />)

    await screen.findByLabelText('Razón social')
    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(guardarEmpresaEmisora).toHaveBeenCalledWith(
      expect.objectContaining({
        cuit: '30-71234567-1',
        cbu: null,
        telefono: null,
        email: null,
        puntoDeVenta: null,
        ingresosBrutos: null,
        inicioActividades: null,
      }),
    )
  })

  /** FR-002: el rechazo marca el campo puntual, no el formulario entero. */
  it('marca el CUIT inválido con su mensaje propio', async () => {
    guardarEmpresaEmisora.mockRejectedValue(
      new ErrorHttp(400, {
        codigo: 'cuit_invalido',
        mensaje: 'El CUIT tiene que tener once dígitos y un dígito verificador válido.',
        campo: 'cuit',
      }),
    )

    const usuario = userEvent.setup()
    render(<EmpresaEmisora />)

    await screen.findByLabelText('Razón social')
    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(
      await screen.findByText(
        'El CUIT tiene que tener once dígitos y un dígito verificador válido.',
      ),
    ).toBeInTheDocument()

    expect(screen.getByLabelText('CUIT (con o sin guiones)')).toHaveAttribute(
      'aria-invalid',
      'true',
    )
  })

  /** El obligatorio vacío se nombra: son cuatro de un formulario de diez (contracts/README). */
  it('marca el obligatorio que el backend nombra', async () => {
    guardarEmpresaEmisora.mockRejectedValue(
      new ErrorHttp(400, {
        codigo: 'datos_invalidos',
        mensaje: 'Completá domicilio para poder guardar.',
        campo: 'domicilio',
      }),
    )

    const usuario = userEvent.setup()
    render(<EmpresaEmisora />)

    await screen.findByLabelText('Razón social')
    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('Completá domicilio para poder guardar.')).toBeInTheDocument()
    expect(screen.getByLabelText('Domicilio')).toHaveAttribute('aria-invalid', 'true')
  })
})

describe('CargaDeLogo', () => {
  beforeEach(() => {
    obtenerEmpresaEmisora.mockReset().mockResolvedValue(CONFIGURADA)
    guardarEmpresaEmisora.mockReset()
    subirLogo.mockReset()
    quitarLogo.mockReset().mockResolvedValue(undefined)
  })

  /** FR-004: es opcional, y la pantalla lo dice con esas palabras. */
  it('dice que el logo es opcional cuando no hay ninguno', async () => {
    render(<EmpresaEmisora />)

    expect(
      await screen.findByText(
        'Todavía no hay un logo cargado. Es opcional: las facturas se emiten igual.',
      ),
    ).toBeInTheDocument()

    expect(screen.getByText('JPG o PNG, hasta 10 MB.')).toBeInTheDocument()
  })

  /**
   * FR-003: el rechazo por formato deja la configuración sin cambios, y el mensaje lo dice. La
   * validación es del servidor —por la firma del archivo—, así que la pantalla sólo muestra lo que
   * llega.
   */
  it('muestra el rechazo del archivo no admitido sin tocar la configuración', async () => {
    subirLogo.mockRejectedValue(
      new ErrorHttp(400, {
        codigo: 'archivo_no_admitido',
        mensaje: 'Ese archivo no es una imagen JPG ni PNG. La configuración quedó sin cambios.',
        campo: 'archivo',
      }),
    )

    const usuario = userEvent.setup()
    render(<EmpresaEmisora />)

    const entrada = await screen.findByLabelText('Cargar logo')
    await usuario.upload(
      entrada,
      new File(['%PDF-1.7'], 'logo.png', { type: 'image/png' }),
    )

    expect(
      await screen.findByText(
        'Ese archivo no es una imagen JPG ni PNG. La configuración quedó sin cambios.',
      ),
    ).toBeInTheDocument()

    expect(
      screen.getByText('Todavía no hay un logo cargado. Es opcional: las facturas se emiten igual.'),
    ).toBeInTheDocument()
  })

  /**
   * Quitar **no pide confirmación aparte**: no destruye nada que no se pueda volver a subir
   * (precedente [004]). El botón dispara la operación directo.
   */
  it('quitar el logo no abre ningún diálogo de confirmación', async () => {
    obtenerEmpresaEmisora.mockResolvedValue({
      ...CONFIGURADA,
      logo: { nombre: 'logo-gt.png', url: '/api/facturacion/empresa-emisora/logo' },
    })

    const usuario = userEvent.setup()
    render(<EmpresaEmisora />)

    await usuario.click(await screen.findByRole('button', { name: 'Quitar' }))

    await waitFor(() => expect(quitarLogo).toHaveBeenCalledOnce())

    // Ningún diálogo intermedio, y el resultado se anuncia con `role="status"`.
    expect(screen.queryByRole('button', { name: 'Confirmar' })).not.toBeInTheDocument()

    const aviso = await screen.findByText('El logo quedó quitado. Las facturas se siguen emitiendo.')
    expect(aviso).toHaveAttribute('role', 'status')
  })

  /** Sin la configuración guardada no hay dónde colgar el logo, y la pantalla lo explica. */
  it('deshabilita la carga mientras la configuración no esté guardada', async () => {
    obtenerEmpresaEmisora.mockResolvedValue(SIN_CONFIGURAR)

    render(<EmpresaEmisora />)

    expect(await screen.findByLabelText('Cargar logo')).toBeDisabled()
    expect(
      screen.getByText('Guardá primero los datos de la empresa emisora para poder cargar el logo.'),
    ).toBeInTheDocument()
  })
})
