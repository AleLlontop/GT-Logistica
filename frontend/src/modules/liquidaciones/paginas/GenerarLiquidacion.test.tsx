import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { GenerarLiquidacion } from './GenerarLiquidacion'
import type { ViajeDisponible } from '../servicios/servicioLiquidaciones'

const navegar = vi.fn()

vi.mock('react-router-dom', async () => {
  const real = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')

  return { ...real, useNavigate: () => navegar }
})

const listarTransportistas = vi.fn()
const listarDisponibles = vi.fn()
const generarLiquidacion = vi.fn()

vi.mock('../servicios/servicioLiquidaciones', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioLiquidaciones')>(
    '../servicios/servicioLiquidaciones',
  )

  return {
    ...real,
    listarTransportistas: (...args: unknown[]) => listarTransportistas(...args),
    listarDisponibles: (...args: unknown[]) => listarDisponibles(...args),
    generarLiquidacion: (...args: unknown[]) => generarLiquidacion(...args),
  }
})

function viaje(id: number, numero: number, importe: number, estado: 'rendido' | 'facturado' = 'rendido'): ViajeDisponible {
  return { id, numero, fecha: '2026-07-05', origen: 'Rosario', destino: 'Córdoba', estado, importe }
}

const VIAJES = [viaje(1, 13, 120_000), viaje(2, 14, 95_000), viaje(3, 15, 140_000, 'facturado')]

function renderizar() {
  render(
    <MemoryRouter>
      <GenerarLiquidacion />
    </MemoryRouter>,
  )
}

async function elegirYBuscar(usuario: ReturnType<typeof userEvent.setup>) {
  await usuario.selectOptions(await screen.findByLabelText('Transportista'), '3')
  await usuario.selectOptions(screen.getByLabelText('Mes'), '7')
  await usuario.selectOptions(screen.getByLabelText('Año'), '2026')
  await usuario.click(screen.getByRole('button', { name: 'Buscar viajes' }))
}

describe('GenerarLiquidacion', () => {
  beforeEach(() => {
    navegar.mockReset()
    listarTransportistas.mockReset().mockResolvedValue({
      empresaEmisoraConfigurada: true,
      transportistas: [{ id: 3, razonSocial: 'Transportes Díaz', cuit: '20123456786', activo: true }],
    })
    listarDisponibles.mockReset().mockResolvedValue(VIAJES)
    generarLiquidacion.mockReset()
  })

  /** FR-001a: sin empresa emisora no hay formulario, y el aviso dice dónde se configura. */
  it('bloquea la generación sin empresa emisora y ofrece la salida', async () => {
    listarTransportistas.mockResolvedValue({ empresaEmisoraConfigurada: false, transportistas: [] })

    renderizar()

    expect(await screen.findByText('No se puede generar todavía.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Empresa emisora' })).toHaveAttribute('href', '/facturacion/empresa')
    expect(screen.queryByLabelText('Transportista')).not.toBeInTheDocument()
  })

  /** FR-003: con campos vacíos los marca y no busca. El año ya viene propuesto, así que no se marca. */
  it('buscar con campos vacíos los marca y no llama al servicio', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Buscar viajes' }))

    expect(screen.getByText('Elegí el transportista al que le vas a liquidar.')).toBeInTheDocument()
    expect(screen.getByText('Elegí el mes del período.')).toBeInTheDocument()
    expect(screen.queryByText('Elegí el año del período.')).not.toBeInTheDocument()
    expect(listarDisponibles).not.toHaveBeenCalled()
  })

  it('propone el año en curso', async () => {
    renderizar()

    expect(await screen.findByLabelText('Año')).toHaveValue(String(new Date().getFullYear()))
  })

  it('muestra el transportista con su CUIT con guiones', async () => {
    renderizar()

    expect(await screen.findByRole('option', { name: 'Transportes Díaz — 20-12345678-6' })).toBeInTheDocument()
  })

  /** FR-006, FR-007: la lista con el total, anunciada y sin campo donde escribir el total. */
  it('lista los viajes con el total y habilita guardar', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await elegirYBuscar(usuario)

    expect(await screen.findByText('$ 355.000,00')).toBeInTheDocument()
    expect(screen.getByText('Se encontraron 3 viajes por $ 355.000,00.')).toHaveAttribute('role', 'status')
    expect(screen.getByText('Facturado')).toBeInTheDocument()
    expect(listarDisponibles).toHaveBeenCalledWith(3, 7, 2026)
    expect(screen.getByRole('button', { name: 'Guardar liquidación' })).toBeEnabled()
  })

  /** FR-010: cambiar la selección vacía la lista y deshabilita guardar. */
  it('cambiar el mes vacía la lista y deshabilita guardar', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await elegirYBuscar(usuario)
    await screen.findByRole('table')

    await usuario.selectOptions(screen.getByLabelText('Mes'), '6')

    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Guardar liquidación' })).toBeDisabled()
    expect(screen.getByText('Cambiaste la selección. Buscá los viajes de nuevo.')).toBeInTheDocument()
  })

  /** FR-009: sin viajes, el mensaje nombra al transportista y el período. */
  it('informa que no hay viajes nombrando transportista y período', async () => {
    listarDisponibles.mockResolvedValue([])
    const usuario = userEvent.setup()
    renderizar()

    await elegirYBuscar(usuario)

    expect(
      await screen.findByText(
        'Transportes Díaz no tiene viajes rendidos para liquidar en 07/2026. Los viajes pendientes, en ' +
          'curso, anulados o ya liquidados no se ofrecen.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Guardar liquidación' })).toBeDisabled()
  })

  /** FR-012a: todos en cero no se guarda, y se dice por qué. */
  it('con total en cero no habilita guardar', async () => {
    listarDisponibles.mockResolvedValue([viaje(9, 20, 0)])
    const usuario = userEvent.setup()
    renderizar()

    await elegirYBuscar(usuario)

    expect(await screen.findByText('No hay importe a liquidar.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Guardar liquidación' })).toBeDisabled()
  })

  it('muestra el rechazo de un viaje ya liquidado', async () => {
    const mensaje =
      'El viaje #13 ya está en la liquidación LQ-5. Buscá los viajes de nuevo para ver los que siguen disponibles.'
    generarLiquidacion.mockRejectedValue(
      new ErrorHttp(409, { codigo: 'viaje_ya_liquidado', mensaje }),
    )
    const usuario = userEvent.setup()
    renderizar()

    await elegirYBuscar(usuario)
    await usuario.click(await screen.findByRole('button', { name: 'Guardar liquidación' }))

    expect(await screen.findByText(mensaje)).toHaveAttribute('role', 'alert')
    expect(navegar).not.toHaveBeenCalled()
  })

  /** FR-019: guarda exactamente los viajes revisados y navega al detalle con la confirmación. */
  it('guarda los viajes revisados y navega al detalle con el aviso', async () => {
    generarLiquidacion.mockResolvedValue({
      id: 12,
      numero: 'LQ-12',
      importeTotal: 355_000,
      viajes: VIAJES,
    })
    const usuario = userEvent.setup()
    renderizar()

    await elegirYBuscar(usuario)
    await usuario.click(await screen.findByRole('button', { name: 'Guardar liquidación' }))

    await waitFor(() =>
      expect(navegar).toHaveBeenCalledWith('/liquidaciones/12', {
        state: { aviso: 'Se generó la liquidación LQ-12 por $ 355.000,00 con 3 viajes.' },
      }),
    )
    expect(generarLiquidacion).toHaveBeenCalledWith({
      transportistaId: 3,
      mes: 7,
      anio: 2026,
      viajeIds: [1, 2, 3],
    })
  })
})
