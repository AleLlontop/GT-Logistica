import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { AltaFactura } from './AltaFactura'

const navegar = vi.fn()

vi.mock('react-router-dom', async () => {
  const real = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')

  return { ...real, useNavigate: () => navegar }
})

const listarClientes = vi.fn()
vi.mock('../../viajes/clientes/servicioClientes', async () => {
  const real = await vi.importActual<typeof import('../../viajes/clientes/servicioClientes')>(
    '../../viajes/clientes/servicioClientes',
  )

  return { ...real, listarClientes: (...args: unknown[]) => listarClientes(...args) }
})

const listarFacturables = vi.fn()
const listarAnuladasSinReemplazo = vi.fn()
const emitirFactura = vi.fn()
const obtenerEmpresaEmisoraParaAlta = vi.fn()
const pedirVistaPrevia = vi.fn()

vi.mock('../servicios/servicioFacturas', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioFacturas')>(
    '../servicios/servicioFacturas',
  )

  return {
    ...real,
    listarFacturables: (...args: unknown[]) => listarFacturables(...args),
    listarAnuladasSinReemplazo: (...args: unknown[]) => listarAnuladasSinReemplazo(...args),
    emitirFactura: (...args: unknown[]) => emitirFactura(...args),
    obtenerEmpresaEmisoraParaAlta: () => obtenerEmpresaEmisoraParaAlta(),
    pedirVistaPrevia: (...args: unknown[]) => pedirVistaPrevia(...args),
  }
})

const CLIENTE = {
  id: 7,
  razonSocial: 'Distribuidora del Litoral',
  cuit: '27000000015',
  telefono: '0341-555-5555',
  email: 'compras@litoral.com.ar',
  direccion: 'Ruta 9 km 312',
  activo: true,
}

const VIAJES = [
  {
    id: 41,
    numero: 1041,
    fecha: '2026-08-05',
    numeroRemito: 'R-41',
    origen: 'Rosario',
    destino: 'Córdoba',
    importe: 30_000,
    puedeFacturarse: true,
    motivoNoFacturable: null,
  },
  {
    id: 42,
    numero: 1042,
    fecha: '2026-08-12',
    numeroRemito: null,
    origen: 'Rosario',
    destino: 'Santa Fe',
    importe: 52_644.63,
    puedeFacturarse: false,
    motivoNoFacturable: 'sinRemito' as const,
  },
]

function renderizar() {
  return render(
    <MemoryRouter>
      <AltaFactura />
    </MemoryRouter>,
  )
}

/** Completa lo que el formulario exige para que `peticion` deje de ser `null`. */
async function completar(usuario: ReturnType<typeof userEvent.setup>) {
  await usuario.selectOptions(await screen.findByLabelText('Cliente'), '7')
  await screen.findByLabelText('Incluir el viaje 1041')
  await usuario.click(screen.getByLabelText('Incluir el viaje 1041'))
  await usuario.type(screen.getByLabelText('Número de comprobante'), '0014-00000003')
  await usuario.type(screen.getByLabelText('CAE'), '75123456789012')
  await usuario.type(screen.getByLabelText('Vencimiento del CAE'), '2026-09-01')
}

describe('AltaFactura', () => {
  beforeEach(() => {
    navegar.mockReset()
    listarClientes.mockReset().mockResolvedValue({
      items: [CLIENTE],
      total: 1,
      pagina: 1,
      tamanioPagina: 20,
    })
    listarFacturables.mockReset().mockResolvedValue(VIAJES)
    listarAnuladasSinReemplazo.mockReset().mockResolvedValue([])
    obtenerEmpresaEmisoraParaAlta.mockReset().mockResolvedValue({
      configurada: true,
      faltantes: [],
    })
    emitirFactura.mockReset().mockResolvedValue({
      id: 12,
      numeroComprobante: '0014-00000003',
      viajes: [{ id: 41 }],
    })
    pedirVistaPrevia.mockReset().mockResolvedValue(new Blob(['%PDF'], { type: 'application/pdf' }))
  })

  /** El desplegable ofrece **sólo activos**: no se le puede facturar a un cliente de baja (FR-011). */
  it('pide sólo los clientes activos', async () => {
    renderizar()

    await waitFor(() =>
      expect(listarClientes).toHaveBeenCalledWith(
        expect.objectContaining({ soloActivos: true }),
        1,
      ),
    )
  })

  it('avisa cuando no hay clientes activos con el texto del contrato', async () => {
    listarClientes.mockResolvedValue({ items: [], total: 0, pagina: 1, tamanioPagina: 20 })

    renderizar()

    expect(
      await screen.findByText(
        'No hay clientes activos en el padrón. Registrá o reactivá un cliente en el Módulo de viajes ' +
          'para poder emitirle una factura.',
      ),
    ).toBeInTheDocument()
  })

  /** FR-006: el aviso llega antes de completar trece campos, y nombra los datos que faltan. */
  it('avisa que la empresa emisora está incompleta nombrando los faltantes', async () => {
    obtenerEmpresaEmisoraParaAlta.mockResolvedValue({
      configurada: false,
      faltantes: ['razón social', 'CUIT', 'domicilio'],
    })

    renderizar()

    expect(
      await screen.findByText(
        'Falta configurar la empresa emisora: razón social, CUIT, domicilio. Cargalos en Empresa ' +
          'emisora para poder emitir.',
      ),
    ).toBeInTheDocument()
  })

  /**
   * FR-020 y FR-025: los importes se recalculan al cambiar el tipo de comprobante, sin volver a pedir
   * nada al servidor. El valor que se guarda sigue siendo el del backend (FR-024).
   */
  it('recalcula los importes al cambiar el tipo de comprobante', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.selectOptions(await screen.findByLabelText('Cliente'), '7')
    await usuario.click(await screen.findByLabelText('Incluir el viaje 1041'))

    expect(screen.getByText('IVA (21%)')).toBeInTheDocument()
    expect(screen.getByText('$ 6.300,00')).toBeInTheDocument()

    await usuario.selectOptions(screen.getByLabelText('Tipo de comprobante'), 'facturaC')

    expect(screen.getByText('IVA (0%)')).toBeInTheDocument()
    expect(screen.getAllByText('$ 30.000,00')).toHaveLength(3)
  })

  /** FR-019a: el viaje sin remito no se puede seleccionar. */
  it('no deja seleccionar un viaje sin remito', async () => {
    const usuario = userEvent.setup()
    renderizar()

    // La lista se carga al elegir cliente: sin cliente no hay período que consultar (FR-015).
    await usuario.selectOptions(await screen.findByLabelText('Cliente'), '7')

    expect(await screen.findByLabelText('Incluir el viaje 1042')).toBeDisabled()
    expect(screen.getByText('Sin remito — no se puede facturar')).toBeInTheDocument()
  })

  /** El cuerpo del `POST` **no lleva** neto, iva ni total: los calcula el servidor (FR-024). */
  it('emite sin mandar los importes en el cuerpo', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Emitir factura' }))

    await waitFor(() => expect(emitirFactura).toHaveBeenCalled())

    const cuerpo = emitirFactura.mock.calls[0][0]

    expect(cuerpo).not.toHaveProperty('neto')
    expect(cuerpo).not.toHaveProperty('iva')
    expect(cuerpo).not.toHaveProperty('total')
    expect(cuerpo.viajeIds).toEqual([41])
  })

  /**
   * FR-014 y convención [005]: al emitir **el formulario no queda en pantalla**. Se navega a la ficha y
   * la confirmación viaja con la navegación.
   */
  it('navega a la ficha con la confirmación y no deja el formulario en pantalla', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Emitir factura' }))

    await waitFor(() =>
      expect(navegar).toHaveBeenCalledWith('/facturas/12', {
        state: {
          aviso: 'Se emitió la factura 0014-00000003. Sus 1 viajes quedaron en estado facturado.',
        },
      }),
    )
  })

  /**
   * FR-032: el `409` del servidor abre el diálogo y el reintento lleva `confirmado: true`. **El primer
   * intento no creó nada**, y la confirmación vive en el backend porque la emisión no se deshace.
   */
  it('abre el diálogo de confirmación ante el 409 y reintenta con confirmado', async () => {
    emitirFactura.mockRejectedValueOnce(
      new ErrorHttp(409, {
        codigo: 'emision_requiere_confirmacion',
        mensaje:
          'El viaje N° 1041 tiene importe $ 0,00 y no suma al neto. Una vez emitida, la factura no ' +
          'cambia de importes: sólo se corrige anulándola.',
        motivoConfirmacion: 'viajeEnCero',
      } as never),
    )

    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Emitir factura' }))

    expect(
      await screen.findByRole('heading', { name: 'Un viaje incluido no tiene importe' }),
    ).toBeInTheDocument()

    expect(screen.getByText(/tiene importe \$ 0,00 y no suma al neto/)).toBeInTheDocument()

    await usuario.click(screen.getByRole('button', { name: 'Emitir igual' }))

    await waitFor(() => expect(emitirFactura).toHaveBeenCalledTimes(2))
    expect(emitirFactura.mock.calls[1][0]).toMatchObject({ confirmado: true })
  })

  /** El segundo diálogo de FR-032, con su título propio. */
  it('abre el diálogo de la fecha anterior con su título propio', async () => {
    emitirFactura.mockRejectedValueOnce(
      new ErrorHttp(409, {
        codigo: 'emision_requiere_confirmacion',
        mensaje: 'El viaje N° 1041 es del 12/08/2026, posterior a la fecha de facturación 05/08/2026.',
        motivoConfirmacion: 'fechaAnteriorAViaje',
      } as never),
    )

    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Emitir factura' }))

    expect(
      await screen.findByRole('heading', {
        name: 'La fecha de la factura es anterior a la de un viaje',
      }),
    ).toBeInTheDocument()
  })

  /** Cancelar la confirmación no vuelve a llamar al servidor: nada se creó y nada se crea. */
  it('cancelar la confirmación no reintenta la emisión', async () => {
    emitirFactura.mockRejectedValueOnce(
      new ErrorHttp(409, {
        codigo: 'emision_requiere_confirmacion',
        mensaje: 'El viaje N° 1041 tiene importe $ 0,00 y no suma al neto.',
        motivoConfirmacion: 'viajeEnCero',
      } as never),
    )

    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Emitir factura' }))

    // El `Cancelar` del diálogo, no el del formulario: se busca dentro del `role="dialog"` para no
    // depender del orden en que aparecen los dos botones con el mismo nombre.
    const dialogo = await screen.findByRole('dialog')

    await usuario.click(
      await within(dialogo).findByRole('button', { name: 'Cancelar' }),
    )

    expect(emitirFactura).toHaveBeenCalledTimes(1)
    expect(navegar).not.toHaveBeenCalled()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  /** El rechazo por número duplicado marca el campo y no navega a ningún lado. */
  it('marca el número de comprobante cuando el backend lo rechaza', async () => {
    emitirFactura.mockRejectedValue(
      new ErrorHttp(400, {
        codigo: 'numero_duplicado',
        mensaje:
          'El número 0014-00000003 ya lo usa la factura del 01/08/2026 de Otro Cliente. Cargá otro ' +
          'número.',
        campo: 'numeroComprobante',
      } as never),
    )

    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Emitir factura' }))

    expect(await screen.findByText(/ya lo usa la factura del 01\/08\/2026/)).toBeInTheDocument()
    expect(screen.getByLabelText('Número de comprobante')).toHaveAttribute('aria-invalid', 'true')
    expect(navegar).not.toHaveBeenCalled()
  })

  /**
   * FR-033: la vista previa es **el PDF del servidor** mostrado en un marco, no una maqueta dibujada
   * acá. El test verifica que se pida al servidor y que se muestre en un `<iframe>`.
   */
  it('pide la vista previa al servidor y la muestra en un marco', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Ver vista previa' }))

    await waitFor(() => expect(pedirVistaPrevia).toHaveBeenCalled())

    expect(
      await screen.findByTitle('Vista previa del documento de la factura'),
    ).toBeInTheDocument()

    expect(
      screen.getByText(
        'Así va a salir la factura. Revisala antes de confirmar: una vez emitida, el cliente, los ' +
          'viajes y los importes no se pueden cambiar.',
      ),
    ).toBeInTheDocument()
  })

  /** Con `Original` el desplegable de la factura reemplazada no aparece (FR-049). */
  it('ofrece el desplegable de factura reemplazada sólo con Refacturación', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await screen.findByLabelText('Tipo de facturación')

    expect(screen.queryByLabelText('Factura que reemplaza')).not.toBeInTheDocument()

    await usuario.selectOptions(screen.getByLabelText('Tipo de facturación'), 'refacturacion')

    expect(screen.getByLabelText('Factura que reemplaza')).toBeInTheDocument()
  })
})
