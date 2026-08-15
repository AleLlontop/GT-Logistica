import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { CorreccionFactura } from './CorreccionFactura'
import type { FacturaDetalle } from '../servicios/servicioFacturas'

vi.mock('react-router-dom', async () => {
  const real = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')

  return { ...real, useParams: () => ({ id: '12' }), useNavigate: () => vi.fn() }
})

const obtenerFactura = vi.fn()
const corregirFactura = vi.fn()

vi.mock('../servicios/servicioFacturas', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioFacturas')>(
    '../servicios/servicioFacturas',
  )

  return {
    ...real,
    obtenerFactura: (...args: unknown[]) => obtenerFactura(...args),
    corregirFactura: (...args: unknown[]) => corregirFactura(...args),
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
    detalle: 'Servicios del período.',
    emisor: {
      razonSocial: 'G&T Logística S.R.L.',
      cuit: '30712345671',
      domicilio: 'Av. Pellegrini 1234',
      condicionIva: 'IVA Responsable Inscripto',
      ingresosBrutos: null,
      inicioActividades: null,
      puntoDeVenta: '0014',
      cbu: null,
      telefono: null,
      email: null,
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
    historial: [],
    ...parcial,
  }
}

function renderizar() {
  render(
    <MemoryRouter>
      <CorreccionFactura />
    </MemoryRouter>,
  )
}

describe('CorreccionFactura', () => {
  beforeEach(() => {
    obtenerFactura.mockReset().mockResolvedValue(detalle())
    corregirFactura.mockReset().mockResolvedValue(detalle({ cae: '99999999999999' }))
  })

  /**
   * FR-036: **sólo cuatro campos editables**, y el resto **no es un campo deshabilitado** — es un dato.
   * Es la diferencia entre "no podés editarlo" y "no es algo que se edite".
   */
  it('ofrece exactamente los cuatro campos editables y ninguno más', async () => {
    renderizar()

    expect(await screen.findByLabelText('Detalle')).toBeEnabled()
    expect(screen.getByLabelText('CAE')).toBeEnabled()
    expect(screen.getByLabelText('Vencimiento del CAE')).toBeEnabled()
    expect(screen.getByLabelText('Vencimiento de pago')).toBeEnabled()

    // El cliente, los viajes y los importes no son campos.
    expect(screen.queryByLabelText('Cliente')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Neto')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Total')).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/viaje/i)).not.toBeInTheDocument()
  })

  it('muestra el aviso de que el resto no se modifica', async () => {
    renderizar()

    expect(
      await screen.findByText(
        'El cliente, los viajes y los importes de una factura emitida no se modifican. Si están mal, ' +
          'la factura se anula y se emite una Refacturación.',
      ),
    ).toBeInTheDocument()
  })

  /** El cuerpo lleva cuatro campos y ninguno más: no hay nada que ignorar del lado del servidor. */
  it('manda sólo los cuatro campos corregibles', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.clear(await screen.findByLabelText('CAE'))
    await usuario.type(screen.getByLabelText('CAE'), '99999999999999')
    await usuario.click(screen.getByRole('button', { name: 'Guardar cambios' }))

    await waitFor(() => expect(corregirFactura).toHaveBeenCalled())

    const cuerpo = corregirFactura.mock.calls[0][1]

    expect(Object.keys(cuerpo).sort()).toEqual(
      ['cae', 'caeVencimiento', 'detalle', 'vencimientoPago'].sort(),
    )
  })

  /** FR-031b: el mensaje menciona la regeneración del documento, que es lo que cambió afuera. */
  it('el guardado menciona la regeneración del documento y se anuncia', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await screen.findByLabelText('CAE')
    await usuario.click(screen.getByRole('button', { name: 'Guardar cambios' }))

    const aviso = await screen.findByText(
      'Se guardaron los cambios y se regeneró el documento de la factura.',
    )

    expect(aviso).toHaveAttribute('role', 'status')
  })

  /** US4 esc. 8: corregir una factura pagada está permitido y su fecha de cobro sigue a la vista. */
  it('deja corregir una factura pagada y muestra su fecha de cobro', async () => {
    obtenerFactura.mockResolvedValue(detalle({ estado: 'pagada', fechaCobro: '2026-09-01' }))

    renderizar()

    expect(await screen.findByLabelText('CAE')).toBeEnabled()
    expect(screen.getByText('01/09/2026')).toBeInTheDocument()
  })

  /** FR-038: la anulada es el único estado que cierra la corrección. */
  it('con una factura anulada no ofrece el formulario', async () => {
    obtenerFactura.mockResolvedValue(
      detalle({ estado: 'anulada', motivoAnulacion: 'Cliente equivocado.' }),
    )

    renderizar()

    expect(
      await screen.findByText('Una factura anulada no se puede corregir.'),
    ).toBeInTheDocument()

    expect(screen.queryByLabelText('CAE')).not.toBeInTheDocument()
  })

  /** US4 esc. 6: una factura emitida no puede quedarse sin CAE. */
  it('marca el CAE cuando el backend lo rechaza por vacío', async () => {
    corregirFactura.mockRejectedValue(
      new ErrorHttp(400, {
        codigo: 'cae_requerido',
        mensaje: 'Una factura emitida no puede quedarse sin CAE.',
        campo: 'cae',
      } as never),
    )

    const usuario = userEvent.setup()
    renderizar()

    await usuario.clear(await screen.findByLabelText('CAE'))
    await usuario.click(screen.getByRole('button', { name: 'Guardar cambios' }))

    expect(
      await screen.findByText('Una factura emitida no puede quedarse sin CAE.'),
    ).toBeInTheDocument()

    expect(screen.getByLabelText('CAE')).toHaveAttribute('aria-invalid', 'true')
  })
})
