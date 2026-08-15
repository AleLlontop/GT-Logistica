import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { FichaFactura } from './FichaFactura'
import type { FacturaDetalle } from '../servicios/servicioFacturas'

vi.mock('react-router-dom', async () => {
  const real = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')

  return { ...real, useParams: () => ({ id: '12' }), useNavigate: () => vi.fn() }
})

const obtenerFactura = vi.fn()
const registrarCobro = vi.fn()
const anularFactura = vi.fn()

vi.mock('../servicios/servicioFacturas', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioFacturas')>(
    '../servicios/servicioFacturas',
  )

  return {
    ...real,
    obtenerFactura: (...args: unknown[]) => obtenerFactura(...args),
    registrarCobro: (...args: unknown[]) => registrarCobro(...args),
    anularFactura: (...args: unknown[]) => anularFactura(...args),
  }
})

function detalle(parcial: Partial<FacturaDetalle> = {}): FacturaDetalle {
  return {
    id: 12,
    numeroComprobante: '0014-00000003',
    fecha: '2026-08-12',
    tipoComprobante: 'facturaA',
    tipoFacturacion: 'original',
    condicionDeVenta: 'cuentaCorriente',
    mes: 8,
    anio: 2026,
    detalle: 'Servicios de transporte del período.',
    emisor: {
      razonSocial: 'G&T Logística S.R.L.',
      cuit: '30712345671',
      domicilio: 'Av. Pellegrini 1234, Rosario',
      condicionIva: 'IVA Responsable Inscripto',
      ingresosBrutos: '902-123456-7',
      inicioActividades: '2018-03-01',
      puntoDeVenta: '0014',
      cbu: '0170099220000067797470',
      telefono: '0341-444-4444',
      email: 'administracion@gtlogistica.com.ar',
    },
    cliente: {
      id: 7,
      razonSocial: 'Distribuidora del Litoral',
      cuit: '27000000015',
      domicilio: 'Ruta 9 km 312',
      activo: true,
    },
    viajes: [
      {
        id: 41,
        numero: 1041,
        fecha: '2026-08-05',
        numeroRemito: 'R-41',
        origen: 'Rosario',
        destino: 'Córdoba',
        importe: 100_000,
      },
    ],
    neto: 100_000,
    iva: 21_000,
    alicuota: 21,
    total: 121_000,
    cae: '75123456789012',
    caeVencimiento: '2026-08-22',
    vencimientoPago: '2026-09-11',
    estado: 'pendiente',
    fechaCobro: null,
    motivoAnulacion: null,
    reemplazaA: null,
    reemplazadaPor: null,
    documentoUrl: '/api/facturas/12/documento',
    historial: [
      {
        estadoAnterior: null,
        estadoNuevo: 'pendiente',
        usuario: 'jlopez',
        ocurridoEn: '2026-08-12T13:14:00Z',
      },
    ],
    ...parcial,
  }
}

function renderizar(puedeGestionar = true, puedeAnular = true) {
  render(
    <MemoryRouter>
      <FichaFactura puedeGestionar={puedeGestionar} puedeAnular={puedeAnular} />
    </MemoryRouter>,
  )
}

describe('FichaFactura', () => {
  beforeEach(() => {
    obtenerFactura.mockReset().mockResolvedValue(detalle())
    registrarCobro.mockReset().mockResolvedValue(detalle({ estado: 'pagada', fechaCobro: '2026-09-01' }))
    anularFactura.mockReset().mockResolvedValue(
      detalle({ estado: 'anulada', motivoAnulacion: 'Cliente equivocado.' }),
    )
  })

  /** FR-034 y FR-034a: el aviso de datos congelados es permanente, no un tooltip. */
  it('muestra el aviso de datos congelados', async () => {
    renderizar()

    expect(
      await screen.findByText(
        'Estos datos son los que tenía la factura el día que se emitió. Un cambio posterior en la ' +
          'configuración o en el padrón no la modifica.',
      ),
    ).toBeInTheDocument()
  })

  /** FR-031c: el documento no es el comprobante fiscal, y la pantalla lo dice. */
  it('ofrece el documento con la nota de que no es el comprobante fiscal', async () => {
    renderizar()

    const enlace = await screen.findByRole('link', { name: 'Ver el documento' })

    expect(enlace).toHaveAttribute('href', '/api/facturas/12/documento')
    expect(
      screen.getByText(
        'Este documento es la representación impresa de la factura, no el comprobante fiscal. La ' +
          'validez la da el CAE, que se obtiene en AFIP/ARCA por fuera del sistema.',
      ),
    ).toBeInTheDocument()
  })

  /**
   * FR-037: una entrada sin estado nuevo se lee `Corrección de datos`. La ausencia **es** la marca: el
   * sistema registra quién y cuándo, y no qué campos cambiaron.
   */
  it('lee una entrada de corrección como Corrección de datos', async () => {
    obtenerFactura.mockResolvedValue(
      detalle({
        historial: [
          {
            estadoAnterior: null,
            estadoNuevo: 'pendiente',
            usuario: 'jlopez',
            ocurridoEn: '2026-08-12T13:14:00Z',
          },
          {
            estadoAnterior: null,
            estadoNuevo: null,
            usuario: 'mgarcia',
            ocurridoEn: '2026-08-21T14:30:00Z',
          },
        ],
      }),
    )

    renderizar()

    expect(await screen.findByText('Corrección de datos')).toBeInTheDocument()
  })

  /** FR-050: las dos direcciones de la referencia de refacturación. */
  it('muestra a qué factura reemplaza cuando es una Refacturación', async () => {
    obtenerFactura.mockResolvedValue(
      detalle({
        tipoFacturacion: 'refacturacion',
        reemplazaA: {
          id: 9,
          numeroComprobante: '0014-00000001',
          fecha: '2026-07-30',
          estado: 'anulada',
        },
      }),
    )

    renderizar()

    expect(await screen.findByText(/Reemplaza a la factura/)).toBeInTheDocument()
    expect(screen.getByText(/del 30\/07\/2026, anulada\./)).toBeInTheDocument()
  })

  it('muestra qué Refacturación la reemplazó cuando está anulada', async () => {
    obtenerFactura.mockResolvedValue(
      detalle({
        estado: 'anulada',
        motivoAnulacion: 'Importes mal cargados.',
        reemplazadaPor: {
          id: 15,
          numeroComprobante: '0014-00000004',
          fecha: '2026-08-20',
          estado: 'pendiente',
        },
      }),
    )

    renderizar()

    expect(await screen.findByText(/Reemplazada por la Refacturación/)).toBeInTheDocument()
    expect(screen.getByText('Importes mal cargados.')).toBeInTheDocument()
  })

  // ── Las acciones según estado y permiso (contracts/README §Acciones) ─────────────────────────

  it('en pendiente ofrece corregir, cobrar y anular', async () => {
    renderizar()

    expect(await screen.findByRole('button', { name: 'Corregir datos' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Registrar cobro' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Anular' })).toBeInTheDocument()
  })

  /** En `pagada` sólo corregir: `pagada` es terminal y no hay nada que cobrar ni anular (FR-043). */
  it('en pagada ofrece sólo corregir', async () => {
    obtenerFactura.mockResolvedValue(detalle({ estado: 'pagada', fechaCobro: '2026-09-01' }))

    renderizar()

    expect(await screen.findByRole('button', { name: 'Corregir datos' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Registrar cobro' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Anular' })).not.toBeInTheDocument()
  })

  /**
   * En `anulada` **ninguna acción**, y **no existe ninguna para devolverla a `pendiente`**: no está
   * oculta, no existe (FR-038, FR-043).
   */
  it('en anulada no ofrece ninguna acción de escritura', async () => {
    obtenerFactura.mockResolvedValue(
      detalle({ estado: 'anulada', motivoAnulacion: 'Cliente equivocado.' }),
    )

    renderizar()

    await screen.findByText(/Esta factura está anulada/)

    expect(screen.queryByRole('button', { name: 'Corregir datos' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Registrar cobro' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Anular' })).not.toBeInTheDocument()

    // Y no hay ninguna acción inventada de reactivación.
    expect(screen.queryByRole('button', { name: /reactivar/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /revertir/i })).not.toBeInTheDocument()
  })

  /** FR-067: `facturacion.anular` es un permiso aparte. */
  it('sin permiso de anular no ofrece Anular pero sí las otras dos', async () => {
    renderizar(true, false)

    expect(await screen.findByRole('button', { name: 'Corregir datos' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Registrar cobro' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Anular' })).not.toBeInTheDocument()
  })

  it('sin permiso de gestión no ofrece ninguna acción de escritura', async () => {
    renderizar(false, false)

    await screen.findByRole('link', { name: 'Ver el documento' })

    expect(screen.queryByRole('button', { name: 'Corregir datos' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Registrar cobro' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Anular' })).not.toBeInTheDocument()
  })

  // ── El cobro (User Story 5) ─────────────────────────────────────────────────────────────────

  /** La advertencia dice que el paso no se revierte, y es literal (FR-043). */
  it('el formulario de cobro advierte que el paso no se revierte', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Registrar cobro' }))

    expect(
      await screen.findByText(
        'La factura queda en estado Pagada. Es un paso que no se revierte: el sistema no ofrece ' +
          'ninguna acción para volver atrás un cobro.',
      ),
    ).toBeInTheDocument()
  })

  it('registra el cobro y anuncia el resultado', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Registrar cobro' }))

    const dialogo = await screen.findByRole('dialog')
    await usuario.click(within(dialogo).getByRole('button', { name: 'Registrar cobro' }))

    await waitFor(() => expect(registrarCobro).toHaveBeenCalled())

    const aviso = await screen.findByText('Se registró el cobro de la factura 0014-00000003.')
    expect(aviso).toHaveAttribute('role', 'status')
  })

  // ── La anulación (User Story 6) ─────────────────────────────────────────────────────────────

  /** FR-046: el botón no se habilita sin motivo escrito. */
  it('el diálogo de anulación no habilita el botón sin motivo', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Anular' }))

    const dialogo = await screen.findByRole('dialog')

    expect(within(dialogo).getByRole('button', { name: 'Anular factura' })).toBeDisabled()

    await usuario.type(
      within(dialogo).getByLabelText(/Motivo de la anulación/),
      'Se facturó al cliente equivocado.',
    )

    expect(within(dialogo).getByRole('button', { name: 'Anular factura' })).toBeEnabled()
  })

  /** US6 esc. 3: cancelar no modifica nada, y eso empieza por no llamar al backend. */
  it('cancelar la anulación no llama al backend', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Anular' }))

    const dialogo = await screen.findByRole('dialog')
    await usuario.click(within(dialogo).getByRole('button', { name: 'Cancelar' }))

    expect(anularFactura).not.toHaveBeenCalled()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('anula con el motivo escrito y anuncia que los viajes volvieron a rendido', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Anular' }))

    const dialogo = await screen.findByRole('dialog')

    await usuario.type(
      within(dialogo).getByLabelText(/Motivo de la anulación/),
      'Se facturó al cliente equivocado.',
    )

    await usuario.click(within(dialogo).getByRole('button', { name: 'Anular factura' }))

    await waitFor(() =>
      expect(anularFactura).toHaveBeenCalledWith(12, 'Se facturó al cliente equivocado.'),
    )

    expect(
      await screen.findByText(
        'Se anuló la factura 0014-00000003. Sus 1 viajes volvieron a estado rendido y quedan ' +
          'disponibles para facturar de nuevo.',
      ),
    ).toBeInTheDocument()
  })

  /**
   * FR-043a: anular una cobrada se rechaza informando desde qué fecha lo está, **sin ofrecer revertir el
   * cobro**.
   */
  it('muestra el rechazo de anular una factura cobrada sin sugerir revertirla', async () => {
    anularFactura.mockRejectedValue(
      new ErrorHttp(409, {
        codigo: 'factura_cobrada',
        mensaje: 'La factura 0014-00000003 está cobrada desde el 01/09/2026 y no se puede anular.',
      } as never),
    )

    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Anular' }))

    const dialogo = await screen.findByRole('dialog')
    await usuario.type(within(dialogo).getByLabelText(/Motivo de la anulación/), 'Error de carga.')
    await usuario.click(within(dialogo).getByRole('button', { name: 'Anular factura' }))

    expect(
      await screen.findByText(
        'La factura 0014-00000003 está cobrada desde el 01/09/2026 y no se puede anular.',
      ),
    ).toBeInTheDocument()

    expect(screen.queryByRole('button', { name: /revertir/i })).not.toBeInTheDocument()
  })

  /** La confirmación de la emisión viaja con la navegación y se anuncia acá (FR-014). */
  it('formatea los importes con el separador argentino', async () => {
    renderizar()

    expect(await screen.findByText('$ 121.000,00')).toBeInTheDocument()
    expect(screen.getByText('IVA (21%)')).toBeInTheDocument()
  })
})
