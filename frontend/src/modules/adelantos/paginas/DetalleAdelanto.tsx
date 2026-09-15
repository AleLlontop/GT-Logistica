import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { formatearFecha, formatearInstante } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { AsideDeFicha, CifraDestacada } from '../../../compartido/ui/AsideDeFicha'
import { Boton } from '../../../compartido/ui/Boton'
import { Callout } from '../../../compartido/ui/Callout'
import { clasesDeAvisoDePantalla } from '../../../compartido/ui/clases'
import { cn } from '../../../compartido/ui/cn'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { Estado } from '../../../compartido/ui/Estado'
import { FichaCuerpo, FichaSeccion } from '../../../compartido/ui/Ficha'
import { IconoAnulado, IconoEnRegla } from '../../../compartido/ui/iconos'
import { EntradaDeLineaDeTiempo, LineaDeTiempo } from '../../../compartido/ui/LineaDeTiempo'
import { DialogoAnulacion } from '../componentes/DialogoAnulacion'
import { DialogoRechazo } from '../componentes/DialogoRechazo'
import {
  anularAdelanto,
  aprobarAdelanto,
  esSinPermiso,
  formatearPersona,
  mensajeDeRechazo,
  NOMBRES_DE_ESTADO,
  NOMBRES_DE_TIPO,
  obtenerAdelanto,
  rechazarAdelanto,
  type AdelantoDetalle,
  type OperacionDeAdelanto,
} from '../servicios/servicioAdelantos'

const TITULO = 'Adelanto'

const VOLVER = { ruta: '/adelantos', etiqueta: 'Volver a adelantos' }

export const MENSAJE_SIN_PERMISO_PARA_VER_ADELANTO =
  'No tenés permiso para ver este adelanto. Si lo necesitás, pedíselo a quien administra los usuarios del sistema.'

const VERBO: Record<OperacionDeAdelanto, string> = {
  registro: 'Registrado',
  aprobacion: 'Aprobado',
  rechazo: 'Rechazado',
  anulacion: 'Anulado',
}

interface Props {
  /** `adelantos.gestionar`: aprobar, rechazar y anular. Quien sólo consulta no ve acciones. */
  puedeGestionar: boolean
}

/**
 * Detalle de un adelanto (User Story 3, FR-019, FR-020, FR-036), y la pantalla desde la que se resuelve
 * (User Stories 4 y 5).
 *
 * **Ofrece exactamente las acciones que las banderas del servidor y el permiso admiten**; ocultarlas es una
 * cortesía y la restricción es el `403` y el `409` del servidor. Un `409` al aprobar **relee** el detalle,
 * para que la pantalla muestre el estado en que quedó (FR-026).
 */
export function DetalleAdelanto({ puedeGestionar }: Props) {
  const { id } = useParams()
  const adelantoId = Number(id)
  const ubicacion = useLocation()

  const [detalle, setDetalle] = useState<AdelantoDetalle | null>(null)
  const [noEncontrado, setNoEncontrado] = useState(false)
  const [sinPermiso, setSinPermiso] = useState(false)
  const [errorDeCarga, setErrorDeCarga] = useState<string | null>(null)

  // La confirmación del registro viaja con la navegación y se anuncia acá (FR-011).
  const [aviso, setAviso] = useState<string | null>(
    (ubicacion.state as { aviso?: string } | null)?.aviso ?? null,
  )
  const [alerta, setAlerta] = useState<string | null>(null)

  const [aprobando, setAprobando] = useState(false)
  const [rechazando, setRechazando] = useState(false)
  const [anulando, setAnulando] = useState(false)

  const traer = useCallback(() => {
    obtenerAdelanto(adelantoId)
      .then((traido) => {
        setDetalle(traido)
        setErrorDeCarga(null)
      })
      .catch((fallo) => {
        if (fallo instanceof ErrorHttp && fallo.estado === 404) {
          setNoEncontrado(true)
        } else if (esSinPermiso(fallo)) {
          setSinPermiso(true)
        } else {
          setErrorDeCarga('No pudimos traer el adelanto. Volvé a intentar en unos minutos.')
        }
      })
  }, [adelantoId])

  useEffect(() => {
    traer()
  }, [traer])

  // FR-043: sin el permiso, ni datos ni acciones, y nada que invite a reintentar.
  if (sinPermiso) {
    return (
      <section>
        <EncabezadoDePantalla titulo={TITULO} />
        <p role="alert" className={clasesDeAvisoDePantalla.advertencia}>
          {MENSAJE_SIN_PERMISO_PARA_VER_ADELANTO}
        </p>
      </section>
    )
  }

  if (noEncontrado) {
    return (
      <section>
        <EncabezadoDePantalla titulo={TITULO} />
        <p role="alert" className="m-0 text-[13px] text-ink-soft">
          No encontramos ese adelanto.{' '}
          <Link to="/adelantos" className="text-brand underline underline-offset-2">
            Volver a adelantos
          </Link>
        </p>
      </section>
    )
  }

  if (detalle === null) {
    return (
      <section>
        <EncabezadoDePantalla titulo={TITULO} volverA={VOLVER} />
        {errorDeCarga !== null ? (
          <p role="alert" className={clasesDeAvisoDePantalla.error}>
            {errorDeCarga}
          </p>
        ) : (
          <p role="status" className="m-0 text-[13px] text-ink-soft">
            Cargando adelanto…
          </p>
        )}
      </section>
    )
  }

  const adelanto = detalle
  const persona = formatearPersona(adelanto.persona)
  const importe = formatearPesos(adelanto.importe)

  async function aprobar() {
    setAprobando(true)
    setAlerta(null)

    try {
      const actualizado = await aprobarAdelanto(adelanto.id)

      setDetalle(actualizado)
      setAviso(`Se aprobó el adelanto de ${importe} para ${persona}.`)
    } catch (fallo) {
      setAviso(null)
      setAlerta(mensajeDeRechazo(fallo))

      // Otro usuario lo resolvió mientras tanto: se relee para mostrar cómo quedó.
      if (fallo instanceof ErrorHttp && fallo.estado === 409) {
        traer()
      }
    } finally {
      setAprobando(false)
    }
  }

  async function rechazar(motivo: string) {
    const actualizado = await rechazarAdelanto(adelanto.id, motivo)

    setDetalle(actualizado)
    setRechazando(false)
    setAlerta(null)
    setAviso(`Se rechazó el adelanto de ${importe} para ${persona}.`)
  }

  async function anular(motivo: string) {
    const actualizado = await anularAdelanto(adelanto.id, motivo)

    setDetalle(actualizado)
    setAnulando(false)
    setAlerta(null)
    setAviso(`Se anuló el adelanto de ${importe} para ${persona}.`)
  }

  const acciones =
    puedeGestionar && adelanto.puedeResolverse ? (
      <>
        <Boton variante="secundario" onClick={() => setRechazando(true)} disabled={aprobando}>
          Rechazar adelanto
        </Boton>
        <Boton
          variante="primario"
          onClick={() => void aprobar()}
          disabled={aprobando}
          icono={<IconoEnRegla className="size-3" />}
        >
          Aprobar adelanto
        </Boton>
      </>
    ) : puedeGestionar && adelanto.puedeAnularse ? (
      // En un aprobado no hay primario: la única acción es destructiva (plan §UI Design Check).
      <Boton variante="destructivo" onClick={() => setAnulando(true)} icono={<IconoAnulado className="size-3" />}>
        Anular adelanto
      </Boton>
    ) : undefined

  return (
    <section>
      <EncabezadoDePantalla
        titulo={TITULO}
        volverA={VOLVER}
        resumen={
          <span className="flex flex-col gap-1.5">
            <span className="flex flex-wrap items-center gap-2">
              <Estado valor={adelanto.estado} texto={NOMBRES_DE_ESTADO[adelanto.estado]} forma="pastilla" />
            </span>
            <span>
              {persona} · {NOMBRES_DE_TIPO[adelanto.tipo]} · {formatearFecha(adelanto.fecha)}
            </span>
          </span>
        }
        accionPrincipal={acciones}
      />

      {/* Un resultado que aparece sin que la pantalla cambie: el rol va en el `<p>` del texto ([008]). */}
      {aviso !== null && (
        <p role="status" className={cn(clasesDeAvisoDePantalla.exito, 'mb-[18px]')}>
          {aviso}
        </p>
      )}
      {alerta !== null && (
        <p role="alert" className={cn(clasesDeAvisoDePantalla.error, 'mb-[18px]')}>
          {alerta}
        </p>
      )}

      <CalloutDeEstado adelanto={adelanto} puedeGestionar={puedeGestionar} />

      <FichaCuerpo
        aside={
          <AsideDeFicha
            destacado={
              <div className="flex flex-col gap-3.5">
                <CifraDestacada rotulo="Importe" valor={importe} />
                <p className="m-0 text-[12.5px] text-faint">
                  {adelanto.estado === 'pendiente'
                    ? 'Todavía no cuenta como adelantado.'
                    : adelanto.estado === 'aprobado'
                      ? 'Cuenta en el total adelantado.'
                      : 'No cuenta en el total adelantado.'}
                </p>
              </div>
            }
          />
        }
      >
        <FichaSeccion titulo="Datos del adelanto" id="titulo-datos-adelanto">
          <dl>
            <dt>Persona</dt>
            <dd>
              <span className="flex flex-col gap-0.5">
                <span>{persona}</span>
                <span className="font-mono text-[11.5px] text-faint">{adelanto.persona.dni}</span>
              </span>
            </dd>

            <dt>Tipo</dt>
            <dd>{NOMBRES_DE_TIPO[adelanto.tipo]}</dd>

            <dt>Fecha</dt>
            <dd>{formatearFecha(adelanto.fecha)}</dd>

            <dt>Motivo</dt>
            <dd>{adelanto.motivo}</dd>
          </dl>
        </FichaSeccion>

        <FichaSeccion titulo="Historial" id="titulo-historial-adelanto">
          <div className="px-[22px] py-5">
            <LineaDeTiempo>
              {adelanto.historial.map((entrada, indice) => (
                <EntradaDeLineaDeTiempo
                  key={`${entrada.operacion}-${entrada.ocurridoEn}`}
                  actual={indice === adelanto.historial.length - 1}
                  cuando={formatearInstante(entrada.ocurridoEn)}
                  que={`${VERBO[entrada.operacion]} por ${entrada.usuario}`}
                  motivo={entrada.motivo === null ? undefined : `Motivo: ${entrada.motivo}`}
                />
              ))}
            </LineaDeTiempo>
          </div>
        </FichaSeccion>
      </FichaCuerpo>

      {rechazando && (
        <DialogoRechazo
          persona={persona}
          importe={adelanto.importe}
          onRechazar={rechazar}
          onCancelar={() => setRechazando(false)}
        />
      )}

      {anulando && (
        <DialogoAnulacion
          persona={persona}
          importe={adelanto.importe}
          onAnular={anular}
          onCancelar={() => setAnulando(false)}
        />
      )}
    </section>
  )
}

/**
 * Qué pasa **y** qué hacer (principio 4 de gt-ui). Quien sólo consulta lo ve **sin** la instrucción que
 * no puede seguir. Un aprobado no lleva callout.
 */
function CalloutDeEstado({ adelanto, puedeGestionar }: { adelanto: AdelantoDetalle; puedeGestionar: boolean }) {
  let contenido: ReactNode = null

  if (adelanto.estado === 'pendiente') {
    contenido = (
      <Callout tono="pendiente" titulo="Pendiente de aprobación.">
        Todavía no suma en el total adelantado.{puedeGestionar ? ' Aprobalo, o rechazalo con su motivo.' : ''}
      </Callout>
    )
  } else if (adelanto.estado === 'rechazado') {
    contenido = (
      <Callout tono="anulado" titulo="Adelanto rechazado — cerrado.">
        Motivo: {adelanto.motivoRechazo}. No se corrige ni se vuelve a presentar
        {puedeGestionar ? ': si hace falta, registrá un adelanto nuevo.' : '.'}
      </Callout>
    )
  } else if (adelanto.estado === 'anulado') {
    contenido = (
      <Callout tono="anulado" titulo="Adelanto anulado — cerrado.">
        Motivo: {adelanto.motivoAnulacion}. Ya no suma en el total adelantado.
        {puedeGestionar ? ' Si correspondía a otra persona o a otro importe, registrá un adelanto nuevo.' : ''}
      </Callout>
    )
  }

  return contenido === null ? null : <div className="mb-[18px]">{contenido}</div>
}
