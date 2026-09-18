import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { BarraDeAcciones, LEYENDA_DE_OBLIGATORIOS } from '../../../compartido/ui/BarraDeAcciones'
import { Boton } from '../../../compartido/ui/Boton'
import { clasesDeAvisoDePantalla, clasesDeFormularioAgrupado } from '../../../compartido/ui/clases'
import { cn } from '../../../compartido/ui/cn'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { IconoEnRegla } from '../../../compartido/ui/iconos'
import { InputImporte } from '../../../compartido/ui/InputImporte'
import { SeccionNumerada } from '../../../compartido/ui/SeccionNumerada'
import { CampoDeCaja } from '../componentes/CampoDeCaja'
import { leerImporte } from '../componentes/importe'
import {
  CodigosErrorCaja,
  detalleDeError,
  esSinPermiso,
  listarFacturasPendientes,
  listarOrdenesDePago,
  MENSAJE_ACCION_SIN_PERMISO,
  MENSAJE_INESPERADO,
  MENSAJE_SIN_CAJA_ABIERTA,
  MENSAJE_SIN_PERMISO,
  NOMBRES_DE_TIPO,
  obtenerCaja,
  registrarMovimiento,
  type OpcionDeReferencia,
  type TipoMovimientoCaja,
} from '../servicios/servicioCaja'

const TITULO = 'Registrar movimiento'

const LARGO_MAXIMO_DEL_CONCEPTO = 200

export const MENSAJE_SIN_PERMISO_PARA_REGISTRAR =
  'No tenés permiso para registrar movimientos de caja. Si lo necesitás, pedíselo a quien administra los usuarios del sistema.'

export const MENSAJE_MOVIMIENTO_REGISTRADO = 'Movimiento registrado con éxito.'

export const AVISO_TOPE_DE_ORDENES = 'Se muestran las 50 órdenes de pago más recientes.'

type Campo = 'tipo' | 'importe' | 'concepto' | 'referencia'

const TIPOS: TipoMovimientoCaja[] = ['ingreso', 'egreso']

interface Props {
  /**
   * `caja.gestionar`, de la sesión. La carga de esta pantalla —`GET /api/caja/{id}`— es de consulta y a
   * Gerencia le responde `200`, así que el `403` no la decide: la decide el permiso (contracts §Pantallas).
   */
  puedeGestionar: boolean
}

/**
 * Registrar un ingreso o un egreso sobre la caja abierta propia (User Story 2, FR-006 a FR-015).
 *
 * **Sin caja operable no hay formulario** (CA3): si la caja no existe, está cerrada o es de otro, se dice y
 * se ofrece ir a las cajas. Si deja de serlo con el formulario en pantalla, el aviso aparece sin perder lo
 * cargado.
 */
export function RegistrarMovimiento({ puedeGestionar }: Props) {
  const { id } = useParams()
  const cajaId = Number(id)

  // `null` mientras carga; después, si se puede operar.
  const [operable, setOperable] = useState<boolean | null>(null)
  const [sinPermiso, setSinPermiso] = useState(false)
  const [errorDeCarga, setErrorDeCarga] = useState<string | null>(null)

  useEffect(() => {
    if (!puedeGestionar) return

    obtenerCaja(cajaId)
      .then((caja) => setOperable(caja.puedeOperar))
      .catch((fallo) => {
        if (fallo instanceof ErrorHttp && fallo.estado === 404) {
          setOperable(false)
        } else if (esSinPermiso(fallo)) {
          setSinPermiso(true)
        } else {
          setErrorDeCarga('No pudimos traer la caja. Volvé a intentar en unos minutos.')
        }
      })
  }, [cajaId, puedeGestionar])

  const volver = { ruta: `/caja/${cajaId}`, etiqueta: 'Volver a la caja' }

  if (!puedeGestionar || sinPermiso) {
    return (
      <section>
        <EncabezadoDePantalla titulo={TITULO} />
        <p role="alert" className={clasesDeAvisoDePantalla.advertencia}>
          {sinPermiso ? MENSAJE_SIN_PERMISO : MENSAJE_SIN_PERMISO_PARA_REGISTRAR}
        </p>
      </section>
    )
  }

  if (operable === false) {
    return (
      <section>
        <EncabezadoDePantalla titulo={TITULO} volverA={volver} />
        <AvisoSinCaja />
      </section>
    )
  }

  if (operable === null) {
    return (
      <section>
        <EncabezadoDePantalla titulo={TITULO} volverA={volver} />
        {errorDeCarga !== null ? (
          <p role="alert" className={clasesDeAvisoDePantalla.error}>
            {errorDeCarga}
          </p>
        ) : (
          <p role="status" className="m-0 text-[13px] text-ink-soft">
            Cargando caja…
          </p>
        )}
      </section>
    )
  }

  return <FormularioDeMovimiento cajaId={cajaId} volver={volver} />
}

/** CA3: qué pasa y cuál es la salida. */
function AvisoSinCaja() {
  return (
    <p role="alert" className={cn(clasesDeAvisoDePantalla.advertencia, 'mb-[18px]')}>
      {MENSAJE_SIN_CAJA_ABIERTA}{' '}
      <Link to="/caja" className="underline underline-offset-2">
        Ir a cajas
      </Link>
    </p>
  )
}

function FormularioDeMovimiento({
  cajaId,
  volver,
}: {
  cajaId: number
  volver: { ruta: string; etiqueta: string }
}) {
  const navegar = useNavigate()

  const [tipo, setTipo] = useState<TipoMovimientoCaja | ''>('')
  const [importeTexto, setImporteTexto] = useState('')
  const [concepto, setConcepto] = useState('')
  const [referencia, setReferencia] = useState<number | ''>('')

  const [opciones, setOpciones] = useState<OpcionDeReferencia[]>([])
  // Cambiar de tipo dos veces seguidas no deja que la respuesta vieja pise a la nueva.
  const pedidoVigente = useRef(0)

  const [tocados, setTocados] = useState<Set<Campo>>(new Set())
  const [intento, setIntento] = useState(false)
  const [errorDeReferencia, setErrorDeReferencia] = useState<string | null>(null)

  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [sinCaja, setSinCaja] = useState(false)

  const importe = leerImporte(importeTexto)

  const errores: Record<Campo, string | null> = {
    tipo: tipo === '' ? 'Elegí si es un ingreso o un egreso.' : null,
    importe:
      importe.tipo === 'vacio' || (importe.tipo === 'valor' && importe.valor <= 0)
        ? 'Escribí un importe mayor que cero.'
        : importe.tipo === 'malEscrito'
          ? 'Escribí el importe con hasta dos decimales.'
          : null,
    concepto: concepto.trim() === '' ? 'Escribí el concepto del movimiento.' : null,
    // La referencia es opcional: sólo la marca el servidor al guardar.
    referencia: errorDeReferencia,
  }

  /** El error aparece después de tocar el campo o de intentar guardar, nunca al abrir. */
  function visible(campo: Campo): string | null {
    return campo === 'referencia' || tocados.has(campo) || intento ? errores[campo] : null
  }

  function tocar(campo: Campo) {
    setTocados((actuales) => new Set(actuales).add(campo))
  }

  /** La referencia vuelve a *Sin referencia* y se cargan las del tipo nuevo; importe y concepto quedan. */
  function elegirTipo(nuevo: TipoMovimientoCaja) {
    if (nuevo === tipo) return

    const pedido = ++pedidoVigente.current

    setTipo(nuevo)
    tocar('tipo')
    setReferencia('')
    setErrorDeReferencia(null)
    setOpciones([])

    const cargar = nuevo === 'ingreso' ? listarFacturasPendientes : listarOrdenesDePago

    cargar()
      .then((traidas) => {
        if (pedido === pedidoVigente.current) setOpciones(traidas)
      })
      .catch((fallo) => {
        if (pedido !== pedidoVigente.current) return

        setError(
          esSinPermiso(fallo)
            ? MENSAJE_ACCION_SIN_PERMISO
            : 'No pudimos traer las referencias. Volvé a intentar en unos minutos.',
        )
      })
  }

  async function guardar(evento: FormEvent) {
    evento.preventDefault()
    setIntento(true)

    if (errores.tipo !== null || errores.importe !== null || errores.concepto !== null) {
      return
    }

    if (tipo === '' || importe.tipo !== 'valor') {
      return
    }

    setEnviando(true)
    setError(null)
    setSinCaja(false)
    setErrorDeReferencia(null)

    try {
      await registrarMovimiento(cajaId, {
        tipo,
        importe: importe.valor,
        concepto: concepto.trim(),
        ...(referencia === ''
          ? {}
          : tipo === 'ingreso'
            ? { facturaId: referencia }
            : { ordenDePagoId: referencia }),
      })

      navegar(`/caja/${cajaId}`, { state: { aviso: MENSAJE_MOVIMIENTO_REGISTRADO } })
    } catch (fallo) {
      const detalle = detalleDeError(fallo)

      if (esSinPermiso(fallo)) {
        setError(MENSAJE_ACCION_SIN_PERMISO)
      } else if (detalle?.codigo === CodigosErrorCaja.referenciaInvalida) {
        setErrorDeReferencia(detalle.mensaje)
      } else if (
        (fallo instanceof ErrorHttp && fallo.estado === 404) ||
        detalle?.codigo === CodigosErrorCaja.cajaCerrada ||
        detalle?.codigo === CodigosErrorCaja.cajaAjena
      ) {
        setSinCaja(true)
      } else {
        setError(detalle?.mensaje ?? MENSAJE_INESPERADO)
      }

      setEnviando(false)
    }
  }

  const errorDeTipo = visible('tipo')

  return (
    <section>
      <EncabezadoDePantalla titulo={TITULO} volverA={volver} />

      {sinCaja && <AvisoSinCaja />}

      {error !== null && (
        <p role="alert" className={cn(clasesDeAvisoDePantalla.error, 'mb-[18px]')}>
          {error}
        </p>
      )}

      <form onSubmit={guardar} noValidate className={clasesDeFormularioAgrupado}>
        <SeccionNumerada numero={1} titulo="Movimiento">
          <div className={cn('campo', errorDeTipo !== null && 'con-error')}>
            {/* La misma etiqueta que `.campo>label`, con la marca de obligatorio generada como allí ([008]):
                un grupo de botones no puede llevar `required`. */}
            <span
              id="etiqueta-tipo"
              className="text-[12.5px] font-semibold tracking-[-0.01em] text-ink-soft after:ml-1 after:font-bold after:text-danger after:content-['*']"
            >
              Tipo
            </span>
            <div
              role="group"
              aria-labelledby="etiqueta-tipo"
              aria-describedby={errorDeTipo !== null ? 'error-tipo' : undefined}
              className="flex gap-2"
            >
              {TIPOS.map((opcion) => (
                // Secundarios los dos: el elegido se marca con el anillo, no con el relleno del primario,
                // que en esta pantalla es *Guardar movimiento*.
                <Boton
                  key={opcion}
                  variante="secundario"
                  tamanio="chico"
                  className={tipo === opcion ? 'bg-white text-ink ring-2 ring-brand' : undefined}
                  aria-pressed={tipo === opcion}
                  onClick={() => elegirTipo(opcion)}
                >
                  {NOMBRES_DE_TIPO[opcion]}
                </Boton>
              ))}
            </div>
            {errorDeTipo !== null && (
              <p className="campo__error" id="error-tipo">
                {errorDeTipo}
              </p>
            )}
          </div>

          <CampoDeCaja id="importe" etiqueta="Importe" ancho="max-w-campo-corto" error={visible('importe')}>
            <InputImporte
              id="importe"
              required
              placeholder="0,00"
              value={importeTexto}
              onChange={setImporteTexto}
              onBlur={() => tocar('importe')}
              aria-invalid={visible('importe') !== null}
              aria-describedby={visible('importe') !== null ? 'error-importe' : undefined}
            />
          </CampoDeCaja>

          <CampoDeCaja id="concepto" etiqueta="Concepto" ancho="max-w-campo-largo" error={visible('concepto')}>
            <input
              id="concepto"
              type="text"
              required
              maxLength={LARGO_MAXIMO_DEL_CONCEPTO}
              placeholder="Cobro flete a Cliente SA"
              value={concepto}
              onChange={(evento) => setConcepto(evento.target.value)}
              onBlur={() => tocar('concepto')}
              aria-invalid={visible('concepto') !== null}
              aria-describedby={visible('concepto') !== null ? 'error-concepto' : undefined}
            />
          </CampoDeCaja>

          <CampoDeCaja
            id="referencia"
            etiqueta="Referencia"
            ancho="max-w-campo-largo"
            error={visible('referencia')}
            ayuda={tipo === 'egreso' ? AVISO_TOPE_DE_ORDENES : undefined}
          >
            <select
              id="referencia"
              value={referencia}
              disabled={tipo === ''}
              onChange={(evento) => {
                setReferencia(evento.target.value === '' ? '' : Number(evento.target.value))
                setErrorDeReferencia(null)
              }}
              aria-invalid={visible('referencia') !== null}
              aria-describedby={
                [visible('referencia') !== null ? 'error-referencia' : '', tipo === 'egreso' ? 'ayuda-referencia' : '']
                  .filter(Boolean)
                  .join(' ') || undefined
              }
            >
              <option value="">Sin referencia</option>
              {opciones.map((opcion) => (
                <option key={opcion.id} value={opcion.id}>
                  {opcion.texto}
                </option>
              ))}
            </select>
          </CampoDeCaja>
        </SeccionNumerada>

        <BarraDeAcciones anclaje="viewport" leyenda={LEYENDA_DE_OBLIGATORIOS}>
          <Boton variante="secundario" onClick={() => navegar(`/caja/${cajaId}`)} disabled={enviando}>
            Cancelar
          </Boton>
          <Boton type="submit" variante="primario" disabled={enviando} icono={<IconoEnRegla className="size-3" />}>
            Guardar movimiento
          </Boton>
        </BarraDeAcciones>
      </form>
    </section>
  )
}
