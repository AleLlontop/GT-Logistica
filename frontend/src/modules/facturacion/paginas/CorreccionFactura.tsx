import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { formatearFecha } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import {
  NOMBRES_DE_ESTADO,
  NOMBRES_DE_TIPO_COMPROBANTE,
} from '../servicios/api'
import {
  corregirFactura,
  nombreDeCliente,
  obtenerFactura,
  type FacturaDetalle,
} from '../servicios/servicioFacturas'

export const AVISO_SOLO_LECTURA =
  'El cliente, los viajes y los importes de una factura emitida no se modifican. Si están mal, la ' +
  'factura se anula y se emite una Refacturación.'

export const MENSAJE_CORREGIDA =
  'Se guardaron los cambios y se regeneró el documento de la factura.'

/**
 * Corrección de una factura emitida (User Story 4, FR-035 a FR-038).
 *
 * **Sólo cuatro campos editables**: detalle, CAE, vencimiento del CAE y vencimiento de pago. El resto se
 * muestra de sólo lectura con el aviso que explica por qué: el cliente, los viajes y los importes de una
 * factura emitida no se modifican por ninguna vía (FR-036, SC-013). No están deshabilitados — no son
 * campos.
 *
 * **El guardado menciona la regeneración del documento**, y no es un detalle decorativo: los cuatro campos
 * corregibles son exactamente los cuatro que salen impresos, así que el archivo cambia y quien corrige
 * tiene que saber que el PDF que ya mandó quedó viejo (FR-031b).
 *
 * **Corregir una factura `pagada` está permitido** y no le toca el estado ni la fecha de cobro (US4 esc. 8).
 * La única que cierra la corrección es la `anulada` (FR-038).
 */
export function CorreccionFactura() {
  const { id } = useParams()
  const navegar = useNavigate()
  const facturaId = Number(id)

  const [factura, setFactura] = useState<FacturaDetalle | null>(null)

  const [detalle, setDetalle] = useState('')
  const [cae, setCae] = useState('')
  const [caeVencimiento, setCaeVencimiento] = useState('')
  const [vencimientoPago, setVencimientoPago] = useState('')

  const [cargando, setCargando] = useState(true)
  const [guardando, setGuardando] = useState(false)
  const [aviso, setAviso] = useState<string | null>(null)
  const [errorGlobal, setErrorGlobal] = useState<string | null>(null)
  const [erroresDeCampo, setErroresDeCampo] = useState<Record<string, string>>({})

  function volcar(traida: FacturaDetalle) {
    setFactura(traida)
    setDetalle(traida.detalle ?? '')
    setCae(traida.cae)
    setCaeVencimiento(traida.caeVencimiento)
    setVencimientoPago(traida.vencimientoPago)
  }

  useEffect(() => {
    let vigente = true

    obtenerFactura(facturaId)
      .then((traida) => {
        if (vigente) volcar(traida)
      })
      .catch(() => {
        if (vigente) setErrorGlobal('No pudimos traer la factura.')
      })
      .finally(() => {
        if (vigente) setCargando(false)
      })

    return () => {
      vigente = false
    }
  }, [facturaId])

  async function guardar(evento: FormEvent) {
    evento.preventDefault()

    setGuardando(true)
    setAviso(null)
    setErrorGlobal(null)
    setErroresDeCampo({})

    try {
      // El cuerpo lleva **cuatro campos y ninguno más**: no hay nada que ignorar del lado del servidor
      // porque no se puede mandar (FR-036).
      volcar(
        await corregirFactura(facturaId, {
          detalle: detalle.trim() === '' ? null : detalle.trim(),
          cae: cae.trim(),
          caeVencimiento,
          vencimientoPago,
        }),
      )

      setAviso(MENSAJE_CORREGIDA)
    } catch (fallo) {
      if (fallo instanceof ErrorHttp) {
        setErrorGlobal(fallo.detalle.mensaje)

        if (fallo.detalle.campo) {
          setErroresDeCampo({ [fallo.detalle.campo]: 'Valor inválido o requerido.' })
        }
      } else {
        setErrorGlobal('Ocurrió un problema inesperado. Volvé a intentar en unos minutos.')
      }
    } finally {
      setGuardando(false)
    }
  }

  function classNameCampo(campo: string) {
    return `campo ${erroresDeCampo[campo] ? 'con-error' : ''}`
  }

  if (cargando) {
    return (
      <main>
        <h1>Corregir factura</h1>
        <p role="status">Cargando…</p>
      </main>
    )
  }

  if (factura === null) {
    return (
      <main>
        <h1>Corregir factura</h1>
        <p role="alert">{errorGlobal ?? 'No encontramos lo que buscabas.'}</p>
      </main>
    )
  }

  // FR-038: la anulada es el único estado que cierra la corrección.
  if (factura.estado === 'anulada') {
    return (
      <main>
        <h1>Corregir factura {factura.numeroComprobante}</h1>
        <p role="alert">Una factura anulada no se puede corregir.</p>
        <button type="button" onClick={() => navegar(`/facturas/${factura.id}`)}>
          Volver a la ficha
        </button>
      </main>
    )
  }

  return (
    <main>
      <h1>Corregir factura {factura.numeroComprobante}</h1>

      <p role="note">{AVISO_SOLO_LECTURA}</p>

      {/* El resto, de sólo lectura: no son campos deshabilitados, son datos (FR-036). */}
      <section aria-labelledby="titulo-no-editable">
        <h2 id="titulo-no-editable">Datos que no se modifican</h2>

        <dl>
          <dt>Cliente</dt>
          <dd>{nombreDeCliente(factura.cliente)}</dd>

          <dt>Tipo de comprobante</dt>
          <dd>{NOMBRES_DE_TIPO_COMPROBANTE[factura.tipoComprobante]}</dd>

          <dt>Período</dt>
          <dd>
            {String(factura.mes).padStart(2, '0')}/{factura.anio}
          </dd>

          <dt>Fecha de facturación</dt>
          <dd>{formatearFecha(factura.fecha)}</dd>

          <dt>Viajes incluidos</dt>
          <dd>{factura.viajes.map((viaje) => viaje.numero).join(', ')}</dd>

          <dt>Neto</dt>
          <dd>{formatearPesos(factura.neto)}</dd>

          <dt>IVA ({factura.alicuota}%)</dt>
          <dd>{formatearPesos(factura.iva)}</dd>

          <dt>Total</dt>
          <dd>{formatearPesos(factura.total)}</dd>

          <dt>Estado</dt>
          <dd>{NOMBRES_DE_ESTADO[factura.estado]}</dd>

          {/* Corregir una factura pagada no le toca ni el estado ni esta fecha (US4 esc. 8). */}
          {factura.fechaCobro !== null && (
            <>
              <dt>Fecha de cobro</dt>
              <dd>{formatearFecha(factura.fechaCobro)}</dd>
            </>
          )}
        </dl>
      </section>

      <form onSubmit={guardar} noValidate>
        {/* El guardado no cambia de pantalla, así que se anuncia acá (convención [003]). */}
        {aviso !== null && <p role="status">{aviso}</p>}
        {errorGlobal !== null && <p role="alert">{errorGlobal}</p>}

        <div className={classNameCampo('detalle')}>
          <label htmlFor="detalle-correccion">Detalle</label>
          <textarea
            id="detalle-correccion"
            maxLength={500}
            value={detalle}
            onChange={(evento) => setDetalle(evento.target.value)}
          />
        </div>

        <div className={classNameCampo('cae')}>
          <label htmlFor="cae-correccion">CAE</label>
          <input
            id="cae-correccion"
            type="text"
            required
            maxLength={20}
            value={cae}
            onChange={(evento) => setCae(evento.target.value)}
            aria-invalid={erroresDeCampo.cae !== undefined}
          />
          {erroresDeCampo.cae && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.cae}
            </p>
          )}
        </div>

        <div className={classNameCampo('caeVencimiento')}>
          <label htmlFor="caeVencimiento-correccion">Vencimiento del CAE</label>
          <input
            id="caeVencimiento-correccion"
            type="date"
            required
            value={caeVencimiento}
            onChange={(evento) => setCaeVencimiento(evento.target.value)}
            aria-invalid={erroresDeCampo.caeVencimiento !== undefined}
          />
          {erroresDeCampo.caeVencimiento && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.caeVencimiento}
            </p>
          )}
        </div>

        <div className={classNameCampo('vencimientoPago')}>
          <label htmlFor="vencimientoPago-correccion">Vencimiento de pago</label>
          <input
            id="vencimientoPago-correccion"
            type="date"
            required
            value={vencimientoPago}
            onChange={(evento) => setVencimientoPago(evento.target.value)}
            aria-invalid={erroresDeCampo.vencimientoPago !== undefined}
          />
          {erroresDeCampo.vencimientoPago && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.vencimientoPago}
            </p>
          )}
        </div>

        <div className="acciones">
          <button type="submit" disabled={guardando}>
            Guardar cambios
          </button>
          <button
            type="button"
            onClick={() => navegar(`/facturas/${factura.id}`)}
            disabled={guardando}
          >
            Volver a la ficha
          </button>
        </div>
      </form>
    </main>
  )
}
