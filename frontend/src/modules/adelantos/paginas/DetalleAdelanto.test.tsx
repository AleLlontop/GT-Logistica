import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { DetalleAdelanto } from './DetalleAdelanto'
import type { AdelantoDetalle } from '../servicios/servicioAdelantos'

vi.mock('react-router-dom', async () => {
  const real = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')

  return { ...real, useParams: () => ({ id: '7' }) }
})

const obtenerAdelanto = vi.fn()
const aprobarAdelanto = vi.fn()
const rechazarAdelanto = vi.fn()
const anularAdelanto = vi.fn()

vi.mock('../servicios/servicioAdelantos', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioAdelantos')>(
    '../servicios/servicioAdelantos',
  )

  return {
    ...real,
    obtenerAdelanto: (...args: unknown[]) => obtenerAdelanto(...args),
    aprobarAdelanto: (...args: unknown[]) => aprobarAdelanto(...args),
    rechazarAdelanto: (...args: unknown[]) => rechazarAdelanto(...args),
    anularAdelanto: (...args: unknown[]) => anularAdelanto(...args),
  }
})

const REGISTRO = { operacion: 'registro' as const, usuario: 'admin.empresa', ocurridoEn: '2026-09-14T13:14:00Z', motivo: null }
const APROBACION = { operacion: 'aprobacion' as const, usuario: 'admin.empresa', ocurridoEn: '2026-09-14T14:00:00Z', motivo: null }

function detalle(parcial: Partial<AdelantoDetalle> = {}): AdelantoDetalle {
  return {
    id: 7,
    fecha: '2026-09-14',
    persona: { id: 5, apellido: 'Pérez', nombre: 'Juan', dni: '30123456' },
    tipo: 'chofer',
    motivo: 'Gastos médicos',
    importe: 150_000,
    estado: 'pendiente',
    motivoRechazo: null,
    motivoAnulacion: null,
    historial: [REGISTRO],
    puedeResolverse: true,
    puedeAnularse: false,
    ...parcial,
  }
}

const APROBADO = detalle({ estado: 'aprobado', historial: [REGISTRO, APROBACION], puedeResolverse: false, puedeAnularse: true })

function renderizar(puedeGestionar = true, aviso?: string) {
  render(
    <MemoryRouter initialEntries={[{ pathname: '/adelantos/7', state: aviso ? { aviso } : null }]}>
      <DetalleAdelanto puedeGestionar={puedeGestionar} />
    </MemoryRouter>,
  )
}

describe('DetalleAdelanto', () => {
  beforeEach(() => {
    obtenerAdelanto.mockReset().mockResolvedValue(detalle())
    aprobarAdelanto.mockReset().mockResolvedValue(APROBADO)
    rechazarAdelanto.mockReset()
    anularAdelanto.mockReset()
  })

  // ── User Story 3 ──────────────────────────────────────────────────────────────────────────────

  /** FR-019, US3 esc. 1. */
  it('muestra la persona con su DNI, el tipo y los datos del adelanto', async () => {
    renderizar()

    expect(await screen.findByText('Pérez, Juan · Chofer · 14/09/2026')).toBeInTheDocument()

    const datos = screen.getByRole('region', { name: 'Datos del adelanto' })
    expect(within(datos).getByText('Pérez, Juan')).toBeInTheDocument()
    expect(within(datos).getByText('30123456')).toBeInTheDocument()
    expect(within(datos).getByText('Chofer')).toBeInTheDocument()
    expect(within(datos).getByText('Gastos médicos')).toBeInTheDocument()
    expect(screen.getByText('$ 150.000,00')).toBeInTheDocument()
  })

  /** FR-036, US3 esc. 2 y 3: el historial con cada usuario, y el motivo de la anulación. */
  it('muestra el historial con el motivo de la anulación', async () => {
    obtenerAdelanto.mockResolvedValue(
      detalle({
        estado: 'anulado',
        motivoAnulacion: 'Cargado sobre la persona equivocada.',
        puedeResolverse: false,
        historial: [
          REGISTRO,
          APROBACION,
          { operacion: 'anulacion', usuario: 'admin', ocurridoEn: '2026-09-15T13:00:00Z', motivo: 'Cargado sobre la persona equivocada.' },
        ],
      }),
    )

    renderizar()

    expect(await screen.findByText('Registrado por admin.empresa')).toBeInTheDocument()
    expect(screen.getByText('Aprobado por admin.empresa')).toBeInTheDocument()
    expect(screen.getByText('Anulado por admin')).toBeInTheDocument()
    expect(screen.getByText('Motivo: Cargado sobre la persona equivocada.')).toBeInTheDocument()
  })

  it('muestra el callout y el aside de cada estado', async () => {
    renderizar()
    expect(await screen.findByText('Pendiente de aprobación.')).toBeInTheDocument()
    expect(screen.getByText('Todavía no cuenta como adelantado.')).toBeInTheDocument()
  })

  it('muestra el rechazado con su motivo y que no cuenta', async () => {
    obtenerAdelanto.mockResolvedValue(
      detalle({ estado: 'rechazado', motivoRechazo: 'Ya tiene otro', puedeResolverse: false }),
    )

    renderizar()

    expect(await screen.findByText('Adelanto rechazado — cerrado.')).toBeInTheDocument()
    expect(screen.getByText(/Motivo: Ya tiene otro\. No se corrige ni se vuelve a presentar: si hace falta/)).toBeInTheDocument()
    expect(screen.getByText('No cuenta en el total adelantado.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /adelanto/ })).not.toBeInTheDocument()
  })

  it('muestra el aprobado sin callout y contando en el total', async () => {
    obtenerAdelanto.mockResolvedValue(APROBADO)

    renderizar()

    expect(await screen.findByText('Cuenta en el total adelantado.')).toBeInTheDocument()
    expect(screen.queryByText(/— cerrado\./)).not.toBeInTheDocument()
  })

  /** US2 esc. 11: quien sólo consulta no ve acciones ni la instrucción que no puede seguir. */
  it('quien sólo consulta no ve acciones ni la oración de instrucción', async () => {
    renderizar(false)

    expect(await screen.findByText('Pendiente de aprobación.')).toBeInTheDocument()
    expect(screen.queryByText(/Aprobalo, o rechazalo/)).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Aprobar adelanto' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Rechazar adelanto' })).not.toBeInTheDocument()
  })

  it('un pendiente ofrece aprobar y rechazar, y un aprobado sólo anular', async () => {
    renderizar()

    expect(await screen.findByRole('button', { name: 'Aprobar adelanto' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Rechazar adelanto' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Anular adelanto' })).not.toBeInTheDocument()
  })

  it('un aprobado ofrece sólo anular', async () => {
    obtenerAdelanto.mockResolvedValue(APROBADO)

    renderizar()

    expect(await screen.findByRole('button', { name: 'Anular adelanto' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Aprobar adelanto' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Rechazar adelanto' })).not.toBeInTheDocument()
  })

  it('anuncia el mensaje que llega por la navegación', async () => {
    renderizar(true, 'Se registró el adelanto de $ 150.000,00 para Pérez, Juan. Queda pendiente de aprobación.')

    expect(
      await screen.findByText('Se registró el adelanto de $ 150.000,00 para Pérez, Juan. Queda pendiente de aprobación.'),
    ).toHaveAttribute('role', 'status')
  })

  /** FR-043, US6 esc. 5. */
  it('un 403 al cargar muestra el aviso sin permiso y no el de inexistente', async () => {
    obtenerAdelanto.mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso.' }))

    renderizar()

    expect(
      await screen.findByText(
        'No tenés permiso para ver este adelanto. Si lo necesitás, pedíselo a quien administra los usuarios del sistema.',
      ),
    ).toHaveAttribute('role', 'alert')
    expect(screen.queryByText('No encontramos ese adelanto.', { exact: false })).not.toBeInTheDocument()
    expect(screen.queryByText(/Volvé a intentar/)).not.toBeInTheDocument()
  })

  // ── User Story 4 ──────────────────────────────────────────────────────────────────────────────

  it('aprobar llama al servicio, anuncia y muestra el estado releído', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Aprobar adelanto' }))

    expect(aprobarAdelanto).toHaveBeenCalledWith(7)
    expect(await screen.findByText('Se aprobó el adelanto de $ 150.000,00 para Pérez, Juan.')).toHaveAttribute(
      'role',
      'status',
    )
    expect(screen.getByText('Aprobado')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Anular adelanto' })).toBeInTheDocument()
  })

  /** FR-026, US4 esc. 7: otro usuario lo resolvió; se dice y se relee. */
  it('un 409 al aprobar muestra su texto y relee el detalle', async () => {
    const mensaje = 'Este adelanto ya está rechazado: no se puede aprobar ni rechazar.'
    aprobarAdelanto.mockRejectedValue(new ErrorHttp(409, { codigo: 'adelanto_no_resoluble', mensaje }))
    obtenerAdelanto
      .mockResolvedValueOnce(detalle())
      .mockResolvedValueOnce(detalle({ estado: 'rechazado', motivoRechazo: 'Ya tiene otro', puedeResolverse: false }))
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Aprobar adelanto' }))

    expect(await screen.findByText(mensaje)).toHaveAttribute('role', 'alert')
    await waitFor(() => expect(obtenerAdelanto).toHaveBeenCalledTimes(2))
    expect(await screen.findByText('Adelanto rechazado — cerrado.')).toBeInTheDocument()
  })

  it('un 403 al aprobar muestra el texto de acción sin permiso', async () => {
    aprobarAdelanto.mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso.' }))
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Aprobar adelanto' }))

    expect(
      await screen.findByText('No tenés permiso para registrar, aprobar, rechazar ni anular adelantos.'),
    ).toHaveAttribute('role', 'alert')
  })

  it('rechaza desde el diálogo y anuncia el resultado', async () => {
    rechazarAdelanto.mockResolvedValue(
      detalle({ estado: 'rechazado', motivoRechazo: 'Ya tiene otro.', puedeResolverse: false }),
    )
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Rechazar adelanto' }))

    const dialogo = await screen.findByRole('dialog')
    await usuario.type(within(dialogo).getByLabelText('Motivo'), 'Ya tiene otro.')
    await usuario.click(within(dialogo).getByRole('button', { name: 'Rechazar adelanto' }))

    await waitFor(() => expect(rechazarAdelanto).toHaveBeenCalledWith(7, 'Ya tiene otro.'))
    expect(await screen.findByText('Se rechazó el adelanto de $ 150.000,00 para Pérez, Juan.')).toHaveAttribute(
      'role',
      'status',
    )
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  // ── User Story 5 ──────────────────────────────────────────────────────────────────────────────

  it('anula desde el diálogo, relee el estado y anuncia el resultado', async () => {
    obtenerAdelanto.mockResolvedValue(APROBADO)
    anularAdelanto.mockResolvedValue(
      detalle({ estado: 'anulado', motivoAnulacion: 'Persona equivocada.', puedeResolverse: false }),
    )
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Anular adelanto' }))

    const dialogo = await screen.findByRole('dialog', { name: 'Anular adelanto' })
    await usuario.type(within(dialogo).getByLabelText('Motivo'), 'Persona equivocada.')
    await usuario.click(within(dialogo).getByRole('button', { name: 'Anular adelanto' }))

    await waitFor(() => expect(anularAdelanto).toHaveBeenCalledWith(7, 'Persona equivocada.'))
    expect(await screen.findByText('Se anuló el adelanto de $ 150.000,00 para Pérez, Juan.')).toHaveAttribute(
      'role',
      'status',
    )
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByText('Anulado')).toBeInTheDocument()
  })

  /** FR-030, US5 esc. 2. */
  it('Volver en la anulación deja el detalle sin cambios', async () => {
    obtenerAdelanto.mockResolvedValue(APROBADO)
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Anular adelanto' }))
    await usuario.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Volver' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(anularAdelanto).not.toHaveBeenCalled()
    expect(screen.getByText('Aprobado')).toBeInTheDocument()
  })
})
