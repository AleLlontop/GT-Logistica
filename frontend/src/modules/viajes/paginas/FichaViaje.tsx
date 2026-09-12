import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { Aviso } from '../../../compartido/ui/Aviso'
import { AsideDeFicha, BloqueDeAside, CifraDestacada } from '../../../compartido/ui/AsideDeFicha'
import { Boton } from '../../../compartido/ui/Boton'
import { Callout } from '../../../compartido/ui/Callout'
import { Estado } from '../../../compartido/ui/Estado'
import { FichaCuerpo, FichaSeccion } from '../../../compartido/ui/Ficha'
import { EntradaDeLineaDeTiempo, LineaDeTiempo } from '../../../compartido/ui/LineaDeTiempo'
import { IconoAnulado, IconoEditar } from '../../../compartido/ui/iconos'
import { useCallback, useEffect, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { formatearFecha, formatearInstante } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { ConfirmacionAnulacion } from '../componentes/ConfirmacionAnulacion'
import {
  ConfirmacionRendicion,
  MENSAJE_REMITO_REQUERIDO,
} from '../componentes/ConfirmacionRendicion'
import { CodigosErrorViajes, esErrorDeViajes } from '../servicios/api'
import {
  anularViaje,
  leyendaDeFactura,
  nombreConEstado,
  NOMBRES_DE_ESTADO,
  obtenerViaje,
  ponerViajeEnCurso,
  rendirViaje,
  type Advertencia,
  type ViajeDetalle,
} from '../servicios/servicioViajes'

const MENSAJE_VIAJE_RENDIDO =
  'Este viaje está rendido. Los viajes rendidos no se editan, no se reasignan y no se anulan.'
const MENSAJE_VIAJE_ANULADO =
  'Este viaje está anulado. No se edita, no se reasigna y no se puede volver atrás.'

/**
 * Módulo 6, FR-052. Dice **dónde** mirar para destrabarlo: sin la mención a la factura, quien opera ve
 * una ficha sin botones y no sabe qué hacer al respecto.
 */
const MENSAJE_VIAJE_FACTURADO =
  'Este viaje está facturado. No se edita, no se reasigna y no cambia de estado. Anulá la factura si ' +
  'necesitás corregirlo.'
const MENSAJE_FALTA_ASIGNAR = 'Asigná chofer y vehículo antes de poner el viaje en curso.'
const MENSAJE_ERROR_GENERICO = 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.'

interface Props {
  /**
   * `viajes.gestionar`. Quien sólo consulta ve la ficha completa y ninguna acción de escritura
   * (FR-052). Ocultarlas es una cortesía: invocarlas a mano igual devuelve 403 (SC-012).
   */
  puedeGestionar: boolean
}

/**
 * Ficha completa de un viaje (FR-045), con su historial de cambios de estado (FR-035).
 *
 * El historial se lee de la línea más vieja a la más nueva y la del alta se muestra como `Alta`: no
 * tiene estado anterior porque antes del alta no había estado.
 *
 * **La pantalla ofrece exactamente las acciones que el estado admite y ninguna más** (FR-018,
 * FR-020, contracts/README.md).
 */
export function FichaViaje({ puedeGestionar }: Props) {
  const { id } = useParams()
  const navegar = useNavigate()
  const viajeId = Number(id)

  const ubicacion = useLocation()

  const [viaje, setViaje] = useState<ViajeDetalle | null>(null)
  const [error, setError] = useState<string | null>(null)

  // El alta y la edición confirman acá, porque el guardado ocurrió en el formulario y la navegación
  // se llevó el resultado con ella (convención [003]). Las advertencias que no bloquean —origen igual
  // a destino, carga retroactiva— llegan por el mismo camino y se anuncian junto a la confirmación,
  // sin pedir ningún paso extra (FR-015a).
  const estadoDeNavegacion = ubicacion.state as {
    aviso?: string
    advertencias?: Advertencia[]
  } | null

  const [aviso, setAviso] = useState<string | null>(estadoDeNavegacion?.aviso ?? null)
  const [advertencias, setAdvertencias] = useState<Advertencia[]>(
    estadoDeNavegacion?.advertencias ?? [],
  )
  const [confirmandoRendicion, setConfirmandoRendicion] = useState(false)
  const [confirmandoAnulacion, setConfirmandoAnulacion] = useState(false)

  /** Módulo 6, FR-055a: el intento de rendir se rechazó porque falta el número de remito. */
  const [faltaRemito, setFaltaRemito] = useState(false)

  const traer = useCallback(() => {
    obtenerViaje(viajeId)
      .then((viaje) => {
        setViaje(viaje)
        setError(null)
      })
      .catch(() => setError('No pudimos traer el viaje. Volvé a intentar en unos minutos.'))
  }, [viajeId])

  useEffect(() => {
    traer()
  }, [traer])

  function mostrarFallo(fallo: unknown) {
    setError(fallo instanceof ErrorHttp ? fallo.detalle.mensaje : MENSAJE_ERROR_GENERICO)
  }

  // Toda acción sobre la ficha limpia lo anterior: la advertencia del alta habla del guardado que la
  // trajo, no del cambio de estado que se acaba de pedir.
  function limpiarAnuncios() {
    setError(null)
    setAviso(null)
    setAdvertencias([])
  }

  async function ponerEnCurso() {
    limpiarAnuncios()

    try {
      const actualizado = await ponerViajeEnCurso(viajeId)
      setViaje(actualizado)
      setAviso(`El viaje ${actualizado.numero} está en curso.`)
    } catch (fallo) {
      // Acá caen los rechazos de FR-025 y FR-026: falta asignar, unidad dada de baja, unidad ocupada
      // por otro viaje —con el número del que la ocupa, ya escrito en el mensaje—.
      mostrarFallo(fallo)
    }
  }

  /**
   * Con importe en cero el backend responde `409` sin cambiar nada y recién entonces se abre el
   * diálogo. La pantalla no adivina: pregunta y reacciona a lo que el servidor decide (FR-038).
   */
  async function rendir(confirmado = false) {
    limpiarAnuncios()
    setFaltaRemito(false)

    try {
      const actualizado = await rendirViaje(viajeId, confirmado)
      setViaje(actualizado)
      setConfirmandoRendicion(false)
      setAviso(`El viaje ${actualizado.numero} quedó rendido.`)
    } catch (fallo) {
      if (esErrorDeViajes(fallo, CodigosErrorViajes.rendicionRequiereConfirmacion)) {
        setConfirmandoRendicion(true)
        return
      }

      setConfirmandoRendicion(false)

      // Módulo 6, FR-055a. **No abre diálogo**: es un dato que falta, no un aviso que se confirma. Se
      // muestra el rechazo del servidor y se ofrece el camino para resolverlo, que es la pantalla de
      // edición — el remito no se carga desde la ficha.
      if (esErrorDeViajes(fallo, CodigosErrorViajes.remitoRequerido)) {
        setFaltaRemito(true)
      }

      mostrarFallo(fallo)
    }
  }

  async function anular(motivo: string) {
    limpiarAnuncios()

    try {
      const actualizado = await anularViaje(viajeId, motivo)
      setViaje(actualizado)
      setAviso(`El viaje ${actualizado.numero} quedó anulado.`)
    } catch (fallo) {
      mostrarFallo(fallo)
    } finally {
      setConfirmandoAnulacion(false)
    }
  }

  if (error !== null) {
    return (
      <section>
        <EncabezadoDePantalla titulo="Viaje" />
        <Aviso tono="error" rol="alert" className="mb-[18px]">
          {error}
        </Aviso>
      </section>
    )
  }

  if (viaje === null) {
    return (
      <section>
        <EncabezadoDePantalla titulo="Viaje" />
        <p role="status" className="m-0 text-[13px] text-ink-soft">Cargando…</p>
      </section>
    )
  }

  // Los dos estados que admiten escritura. Los terminales no ofrecen ninguna acción (FR-018).
  const enCurso = viaje.estado === 'pendiente' || viaje.estado === 'enCurso'
  const faltaAsignar = viaje.chofer === null || viaje.vehiculo === null

  /**
   * Los tres estados terminales dejan la ficha **sin acción principal**, y el callout es lo que lo
   * explica: dice qué pasa **y ofrece la salida** (FR-058). Sin él, la pantalla se lee como una
   * ficha a la que le faltan botones.
   */
  const bloqueo =
    viaje.estado === 'rendido'
      ? {
          tono: 'rendido' as const,
          titulo: 'Viaje rendido — cerrado para edición',
          mensaje: MENSAJE_VIAJE_RENDIDO,
        }
      : viaje.estado === 'anulado'
        ? { tono: 'anulado' as const, titulo: 'Viaje anulado', mensaje: MENSAJE_VIAJE_ANULADO }
        : viaje.estado === 'facturado'
          ? {
              tono: 'facturado' as const,
              titulo: 'Viaje facturado',
              mensaje: MENSAJE_VIAJE_FACTURADO,
            }
          : null

  return (
    <section>
      <EncabezadoDePantalla
        titulo={`Viaje ${viaje.numero}`}
        volverA={{ ruta: '/viajes', etiqueta: 'Volver al listado' }}
        resumen={
          <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <span>{nombreConEstado(viaje.cliente)}</span>
            <span aria-hidden="true">
              ·
            </span>
            <span>
              {viaje.origen}{' '}
              <span aria-hidden="true">
                →
              </span>{' '}
              {viaje.destino}
            </span>
            <span aria-hidden="true">
              ·
            </span>
            <span>{formatearFecha(viaje.fecha)}</span>
          </span>
        }
        accionPrincipal={
          puedeGestionar && enCurso ? (
            <>
              {/*
                **La acción principal de la ficha hay que designarla, no revestirla** (FR-019): sus
                botones eran todos `type="button"` y el encabezado los estilaba por descendiente, así
                que *Editar*, *Anular* y la asignación se dibujaban idénticas. La principal es la
                asignación —es lo que destraba el viaje—, lo demás es secundario y anular es
                destructivo. **Ningún verbo se reescribe** (FR-066).
              */}
              <Boton variante="secundario" onClick={() => navegar(`/viajes/${viaje.id}/editar`)}>
                Editar
              </Boton>

              {viaje.estado === 'pendiente' && (
                <Boton variante="secundario" onClick={ponerEnCurso} disabled={faltaAsignar}>
                  Poner en curso
                </Boton>
              )}

              {viaje.estado === 'enCurso' && (
                <Boton variante="secundario" onClick={() => rendir()}>
                  Rendir
                </Boton>
              )}

              {faltaRemito && (
                <Boton variante="secundario" onClick={() => navegar(`/viajes/${viaje.id}/editar`)}>
                  Cargar el remito
                </Boton>
              )}

              <Boton
                variante="destructivo"
                onClick={() => setConfirmandoAnulacion(true)}
                icono={<IconoAnulado className="size-3" />}
              >
                Anular
              </Boton>

              <Boton
                variante="primario"
                onClick={() => navegar(`/viajes/${viaje.id}/asignacion`)}
                icono={<IconoEditar className="size-3" />}
              >
                {viaje.chofer === null
                  ? 'Asignar chofer y vehículo'
                  : 'Reasignar chofer y vehículo'}
              </Boton>
            </>
          ) : undefined
        }
      />

      {error !== null && (
        <p
          role="alert"
          className="mb-[18px] rounded-card border border-line bg-danger-bg px-[18px] py-4 text-[13px] leading-5 font-medium text-danger-text"
        >
          {error}
        </p>
      )}
      {/*
        El anuncio va en el `<p>` que **contiene** el texto y no en un envoltorio: es lo que hace
        que quien usa lector de pantalla escuche exactamente el mensaje que apareció, y es lo que la
        suite verifica sobre el elemento del texto (convención [003]).
      */}
      {aviso !== null && (
        <p
          role="status"
          className="mb-[18px] rounded-card border border-line bg-estado-rendido-bg px-[18px] py-4 text-[13px] leading-5 font-medium text-estado-rendido"
        >
          {aviso}
        </p>
      )}

      {advertencias.map((advertencia) => (
        <p
          key={advertencia.codigo}
          role="status"
          className="mb-[18px] rounded-card border border-line bg-estado-pendiente-bg px-[18px] py-4 text-[13px] leading-5 font-medium text-estado-pendiente"
        >
          {advertencia.mensaje}
        </p>
      ))}

      {/* Deshabilitado **con el motivo a la vista**, no en silencio (FR-025 del Módulo 5). */}
      {faltaAsignar && viaje.estado === 'pendiente' && (
        <p role="status" className="mb-[18px] text-[12.5px] text-ink-soft">
          {MENSAJE_FALTA_ASIGNAR}
        </p>
      )}
      {faltaRemito && (
        <p role="status" className="mb-[18px] text-[12.5px] text-ink-soft">
          {MENSAJE_REMITO_REQUERIDO}
        </p>
      )}

      {bloqueo !== null && (
        <div className="mb-[18px]">
          <Callout tono={bloqueo.tono} titulo={bloqueo.titulo}>
            <span role="status">{bloqueo.mensaje}</span>
          </Callout>
        </div>
      )}

      <FichaCuerpo
        aside={
          <AsideDeFicha
            /* El dato de más valor de un viaje es su importe (data-model §8). */
            destacado={
              <CifraDestacada rotulo="Importe del viaje" valor={formatearPesos(viaje.importe)} />
            }
          >
            <BloqueDeAside titulo="Asignación">
              <p className="m-0">{nombreConEstado(viaje.chofer)}</p>
              <p className="m-0 font-mono text-[12.5px] text-faint">
                {nombreConEstado(viaje.vehiculo)}
              </p>
              <p className="m-0 text-[12.5px] text-faint">
                {nombreConEstado(viaje.transportista)}
              </p>
            </BloqueDeAside>

            <BloqueDeAside titulo="Facturación">
              {viaje.factura ? (
                <Link
                  to={`/facturas/${viaje.factura.id}`}
                  className="text-brand underline underline-offset-2"
                >
                  {leyendaDeFactura(viaje.factura, formatearFecha(viaje.factura.fecha))}
                </Link>
              ) : (
                /* El vacío se escribe: dice qué falta, nunca un guión (FR-038). */
                <p className="atenuada m-0">Todavía no se facturó</p>
              )}
            </BloqueDeAside>
          </AsideDeFicha>
        }
      >
        <FichaSeccion titulo="Datos del viaje" id="titulo-datos-viaje">
          <dl>
            <dt>Cliente</dt>
            <dd>{nombreConEstado(viaje.cliente)}</dd>

            <dt>Fecha</dt>
            <dd>
              {formatearFecha(viaje.fecha)}
              {/* La señal lleva la palabra que la explica, no sólo un color (FR-060). */}
              {viaje.esRetroactivo && ' — Carga retroactiva'}
            </dd>

            <dt>Recorrido</dt>
            <dd>
              {viaje.origen}{' '}
              <span aria-hidden="true" className="text-dim">
                →
              </span>{' '}
              {viaje.destino}
            </dd>

            <dt>Número de remito</dt>
            <dd className="font-mono">
              {viaje.numeroRemito ?? <span className="atenuada font-sans">Sin remito cargado</span>}
            </dd>

            <dt>Detalle de la carga</dt>
            <dd>{viaje.detalleCarga ?? <span className="atenuada">Sin detalle cargado</span>}</dd>

            <dt>Estado</dt>
            <dd>
              <Estado
                valor={viaje.estado}
                texto={NOMBRES_DE_ESTADO[viaje.estado]}
                forma="pastilla"
                detalle={viaje.demorado ? 'Demorado' : undefined}
              />
            </dd>

            {viaje.motivoAnulacion !== null && (
              <>
                <dt>Motivo de la anulación</dt>
                <dd>{viaje.motivoAnulacion}</dd>
              </>
            )}
          </dl>
        </FichaSeccion>

        <FichaSeccion titulo="Historial" id="titulo-historial-viaje">
          {/*
            El historial deja de ser una tabla de cuatro columnas (FR-054): el par `Estado anterior`
            + `Estado nuevo` nombraba una sola cosa —la transición— y obligaba a leer en horizontal
            algo que se lee en vertical. Ahora va completa en una línea, y el paso actual va marcado,
            que es lo que una tabla no podía decir.
          */}
          <div className="px-[22px] py-5">
            <LineaDeTiempo>
              {viaje.historial.map((cambio, indice) => (
                <EntradaDeLineaDeTiempo
                  key={`${cambio.ocurridoEn}-${indice}`}
                  actual={indice === viaje.historial.length - 1}
                  cuando={formatearInstante(cambio.ocurridoEn)}
                  quien={cambio.usuario}
                  que={
                    cambio.estadoAnterior === null ? (
                      'Alta'
                    ) : (
                      <>
                        {NOMBRES_DE_ESTADO[cambio.estadoAnterior]}{' '}
                        <span aria-hidden="true" className="text-dim">
                          →
                        </span>{' '}
                        {NOMBRES_DE_ESTADO[cambio.estadoNuevo]}
                      </>
                    )
                  }
                />
              ))}
            </LineaDeTiempo>
          </div>
        </FichaSeccion>
      </FichaCuerpo>

      {confirmandoRendicion && (
        <ConfirmacionRendicion
          numero={viaje.numero}
          onConfirmar={() => rendir(true)}
          onCancelar={() => setConfirmandoRendicion(false)}
        />
      )}

      {confirmandoAnulacion && (
        <ConfirmacionAnulacion
          numero={viaje.numero}
          onConfirmar={anular}
          onCancelar={() => setConfirmandoAnulacion(false)}
        />
      )}
    </section>
  )
}
