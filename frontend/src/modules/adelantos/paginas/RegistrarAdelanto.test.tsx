import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { RegistrarAdelanto } from './RegistrarAdelanto'
import type { AdelantoDetalle, Beneficiarios, PersonaResumen } from '../servicios/servicioAdelantos'

const navegar = vi.fn()

vi.mock('react-router-dom', async () => {
  const real = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')

  return { ...real, useNavigate: () => navegar }
})

const listarBeneficiarios = vi.fn()
const registrarAdelanto = vi.fn()

vi.mock('../servicios/servicioAdelantos', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioAdelantos')>(
    '../servicios/servicioAdelantos',
  )

  return {
    ...real,
    listarBeneficiarios: (...args: unknown[]) => listarBeneficiarios(...args),
    registrarAdelanto: (...args: unknown[]) => registrarAdelanto(...args),
  }
})

const JUAN: PersonaResumen = { id: 5, apellido: 'Pérez', nombre: 'Juan', dni: '30123456' }
const CARLOS: PersonaResumen = { id: 6, apellido: 'Gómez', nombre: 'Carlos', dni: '28765432' }
const ANA: PersonaResumen = { id: 9, apellido: 'Torres', nombre: 'Ana', dni: '33222111' }

function beneficiarios(personas: PersonaResumen[], empresaEmisoraConfigurada = true): Beneficiarios {
  return { empresaEmisoraConfigurada, personas }
}

const CREADO: AdelantoDetalle = {
  id: 31,
  fecha: '2026-09-14',
  persona: JUAN,
  tipo: 'chofer',
  motivo: 'gastos médicos',
  importe: 150_000,
  estado: 'pendiente',
  motivoRechazo: null,
  motivoAnulacion: null,
  historial: [],
  puedeResolverse: true,
  puedeAnularse: false,
}

function renderizar(puedeGestionar = true) {
  render(
    <MemoryRouter>
      <RegistrarAdelanto puedeGestionar={puedeGestionar} />
    </MemoryRouter>,
  )
}

async function completar(usuario: ReturnType<typeof userEvent.setup>) {
  await usuario.selectOptions(screen.getByLabelText('Tipo de persona'), 'chofer')
  await screen.findByRole('option', { name: 'Pérez, Juan — 30123456' })
  await usuario.selectOptions(screen.getByLabelText('Persona'), '5')
  await usuario.type(screen.getByLabelText('Motivo'), 'gastos médicos')
  await usuario.type(screen.getByLabelText('Importe'), '150000')
}

describe('RegistrarAdelanto', () => {
  beforeEach(() => {
    // Sólo `Date`: los temporizadores de `userEvent` siguen reales.
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(2026, 8, 14, 12, 0))

    navegar.mockReset()
    listarBeneficiarios
      .mockReset()
      .mockImplementation((tipo: string) =>
        Promise.resolve(tipo === 'chofer' ? beneficiarios([JUAN, CARLOS]) : beneficiarios([ANA])),
      )
    registrarAdelanto.mockReset().mockResolvedValue(CREADO)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  /** FR-005, US1 esc. 5: la fecha viene con hoy y el piso sale de hoy, nunca escrito. */
  it('propone hoy como fecha, con el rango admitido en la ayuda', () => {
    renderizar()

    const fecha = screen.getByLabelText('Fecha')
    expect(fecha).toHaveValue('2026-09-14')
    expect(fecha).toHaveAttribute('min', '2026-08-01')
    expect(fecha).toHaveAttribute('max', '2026-09-14')
    expect(screen.getByText('Desde el 01/08/2026 hasta hoy.')).toBeInTheDocument()
  })

  /** FR-002, US1 esc. 3: sin tipo, *Persona* sólo tiene su texto vacío, y no está deshabilitada. */
  it('sin tipo, Persona no ofrece a nadie', () => {
    renderizar()

    const persona = screen.getByLabelText('Persona')
    const opciones = within(persona).getAllByRole('option')

    expect(persona).not.toBeDisabled()
    expect(opciones).toHaveLength(1)
    expect(opciones[0]).toHaveTextContent('Elegí primero el tipo de persona')
    expect(listarBeneficiarios).not.toHaveBeenCalled()
  })

  /** FR-040: el desplegable que se vuelve a llenar se anuncia. */
  it('al elegir Chofer consulta las personas y anuncia cuántas hay', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.selectOptions(screen.getByLabelText('Tipo de persona'), 'chofer')

    expect(listarBeneficiarios).toHaveBeenCalledWith('chofer')
    expect(await screen.findByText('Hay 2 choferes de G&T Logística para elegir.')).toHaveAttribute('role', 'status')
    expect(screen.getByRole('option', { name: 'Gómez, Carlos — 28765432' })).toBeInTheDocument()
  })

  /** FR-003, US1 esc. 4. */
  it('cambiar el tipo vacía la persona y conserva la fecha, el motivo y el importe', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.selectOptions(screen.getByLabelText('Tipo de persona'), 'empleado')

    expect(await screen.findByText('Hay 1 empleado para elegir.')).toBeInTheDocument()
    expect(screen.getByLabelText('Persona')).toHaveValue('')
    expect(screen.getByLabelText('Fecha')).toHaveValue('2026-09-14')
    expect(screen.getByLabelText('Motivo')).toHaveValue('gastos médicos')
    expect(screen.getByLabelText('Importe')).toHaveValue('150.000')
  })

  /** FR-002a, US1 esc. 12. */
  it('con la empresa emisora sin configurar dice dónde se configura', async () => {
    listarBeneficiarios.mockResolvedValue(beneficiarios([], false))
    const usuario = userEvent.setup()
    renderizar()

    await usuario.selectOptions(screen.getByLabelText('Tipo de persona'), 'chofer')

    expect(await screen.findByText('No se pueden elegir choferes todavía.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Empresa emisora' })).toHaveAttribute('href', '/facturacion/empresa')
  })

  /** FR-004. */
  it('sin personas del tipo lo informa', async () => {
    listarBeneficiarios.mockResolvedValue(beneficiarios([]))
    const usuario = userEvent.setup()
    renderizar()

    await usuario.selectOptions(screen.getByLabelText('Tipo de persona'), 'empleado')

    expect(
      await screen.findByText('No hay empleados activos para elegir. Las personas se cargan desde Personas.'),
    ).toBeInTheDocument()
  })

  /** FR-008, US1 esc. 6. */
  it('guardar vacío marca los cinco campos y no registra', async () => {
    const usuario = userEvent.setup()
    renderizar()

    fireEvent.change(screen.getByLabelText('Fecha'), { target: { value: '' } })
    await usuario.click(screen.getByRole('button', { name: 'Guardar adelanto' }))

    expect(screen.getByText('Elegí si el adelanto es para un chofer o un empleado.')).toBeInTheDocument()
    expect(screen.getByText('Elegí primero el tipo de persona.')).toBeInTheDocument()
    expect(screen.getByText('Elegí la fecha en que se otorgó el adelanto.')).toBeInTheDocument()
    expect(screen.getByText('Escribí el motivo del adelanto. Ejemplo: gastos médicos')).toBeInTheDocument()
    expect(screen.getByText('Escribí un importe mayor que cero. Ejemplo: 150000,00')).toBeInTheDocument()
    expect(registrarAdelanto).not.toHaveBeenCalled()
  })

  /** FR-007, US1 esc. 7. */
  it('marca un importe en cero', async () => {
    const usuario = userEvent.setup()
    renderizar()

    const importe = screen.getByLabelText('Importe')

    await usuario.type(importe, '0')
    await usuario.tab()
    expect(screen.getByText('Escribí un importe mayor que cero. Ejemplo: 150000,00')).toBeInTheDocument()
  })

  /** FR-005, US1 esc. 8. */
  it('marca una fecha anterior al piso con el rango', () => {
    renderizar()

    const fecha = screen.getByLabelText('Fecha')
    fireEvent.change(fecha, { target: { value: '2026-07-31' } })
    fireEvent.blur(fecha)

    expect(screen.getByText('La fecha tiene que estar entre el 01/08/2026 y hoy.')).toBeInTheDocument()
  })

  /** FR-009, US1 esc. 10: el rechazo del servidor no hace perder lo cargado. */
  it('muestra el rechazo por persona no elegible y conserva lo cargado', async () => {
    const mensaje = 'Pérez, Juan se dio de baja y ya no puede recibir adelantos. Elegí otra persona.'
    registrarAdelanto.mockRejectedValue(
      new ErrorHttp(400, { codigo: 'beneficiario_no_elegible', mensaje }),
    )
    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Guardar adelanto' }))

    expect(await screen.findByText(mensaje)).toHaveAttribute('role', 'alert')
    expect(screen.getByLabelText('Motivo')).toHaveValue('gastos médicos')
    expect(screen.getByLabelText('Importe')).toHaveValue('150.000')
    expect(navegar).not.toHaveBeenCalled()
  })

  it('un 403 al guardar muestra el texto de acción sin permiso', async () => {
    registrarAdelanto.mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso.' }))
    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Guardar adelanto' }))

    expect(
      await screen.findByText('No tenés permiso para registrar, aprobar, rechazar ni anular adelantos.'),
    ).toHaveAttribute('role', 'alert')
  })

  /** FR-043, US6 esc. 5. */
  it('sin permiso de gestión muestra el aviso y ningún campo', () => {
    renderizar(false)

    expect(
      screen.getByText(
        'No tenés permiso para registrar adelantos. Si lo necesitás, pedíselo a quien administra los usuarios del sistema.',
      ),
    ).toHaveAttribute('role', 'alert')
    expect(screen.queryByLabelText('Tipo de persona')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Guardar adelanto' })).not.toBeInTheDocument()
  })

  /** FR-011, US1 esc. 9: al guardar se navega al detalle con la confirmación. */
  it('al guardar navega al detalle con la confirmación', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Guardar adelanto' }))

    await waitFor(() =>
      expect(navegar).toHaveBeenCalledWith('/adelantos/31', {
        state: {
          aviso: 'Se registró el adelanto de $ 150.000,00 para Pérez, Juan. Queda pendiente de aprobación.',
        },
      }),
    )
    expect(registrarAdelanto).toHaveBeenCalledWith({
      tipo: 'chofer',
      personaId: 5,
      fecha: '2026-09-14',
      motivo: 'gastos médicos',
      importe: 150_000,
    })
  })
})
