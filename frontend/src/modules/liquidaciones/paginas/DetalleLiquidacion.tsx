import { useCallback, useEffect, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { formatearCuit } from '../../../compartido/cuit'
import { formatearFecha, formatearInstante } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { AsideDeFicha, BloqueDeAside, CifraDestacada } from '../../../compartido/ui/AsideDeFicha'
import { Boton } from '../../../compartido/ui/Boton'
import { Callout } from '../../../compartido/ui/Callout'
import { clasesDeAvisoDePantalla } from '../../../compartido/ui/clases'
import { cn } from '../../../compartido/ui/cn'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { Estado } from '../../../compartido/ui/Estado'
import { FichaCuerpo, FichaSeccion } from '../../../compartido/ui/Ficha'
import { IconoAnulado, IconoEnRegla } from '../../../compartido/ui/iconos'
import { EntradaDeLineaDeTiempo, LineaDeTiempo } from '../../../compartido/ui/LineaDeTiempo'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { TokenDeIdentificador } from '../../../compartido/ui/TokenDeIdentificador'
import { DialogoAnulacion } from '../componentes/DialogoAnulacion'
import { DialogoOrdenDePago } from '../componentes/DialogoOrdenDePago'
import { TablaDeViajes } from '../componentes/TablaDeViajes'
import {
  anularLiquidacion,
  formatearPeriodo,
  NOMBRES_DE_ESTADO,
  obtenerLiquidacion,
  registrarOrdenDePago,
  type CambioDeLiquidacion,
  type LiquidacionDetalle,
} from '../servicios/servicioLiquidaciones'

const VOLVER = { ruta: '/liquidaciones', etiqueta: 'Volver a liquidaciones' }

/** Sin borde propio: la tabla va adentro de la tarjeta de su sección. */
const TABLA_EN_TARJETA = 'rounded-none border-0 shadow-none'

interface Props {
  /** `liquidaciones.gestionar`: registrar pagos, editar y anular. Quien sólo consulta no ve acciones. */
  puedeGestionar: boolean
}

/**
 * Detalle de una liquidación (User Story 3, FR-025 a FR-028).
 *
 * Es lo que deja trazable la relación **viaje ↔ liquidación ↔ orden de pago**, y es el detalle que se le
 * puede mostrar al fletero. **Ofrece exactamente las acciones que las banderas del servidor y el permiso
 * admiten**; ocultarlas es una cortesía y la restricción es el `403` y el `409` del servidor.
 *
 * Las órdenes de pago **no tienen ninguna acción por fila**: no se modifican ni se eliminan (FR-044).
 */
export function DetalleLiquidacion({ puedeGestionar }: Props) {
  const { id } = useParams()
  const liquidacionId = Number(id)
  const navegar = useNavigate()
  const ubicacion = useLocation()

  const [detalle, setDetalle] = useState<LiquidacionDetalle | null>(null)
  const [noEncontrada, setNoEncontrada] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // La confirmación de la generación o de la edición viaja con la navegación y se anuncia acá (FR-019).
  const [aviso, setAviso] = useState<string | null>(
    (ubicacion.state as { aviso?: string } | null)?.aviso ?? null,
  )

  const [pagando, setPagando] = useState(false)
  const [anulando, setAnulando] = useState(false)

  const traer = useCallback(() => {
    obtenerLiquidacion(liquidacionId)
      .then((traida) => {
        setDetalle(traida)
        setError(null)
      })
      .catch((fallo) => {
        if (fallo instanceof ErrorHttp && fallo.estado === 404) {
          setNoEncontrada(true)
        } else {
          setError('No pudimos traer la liquidación. Volvé a intentar en unos minutos.')
        }
      })
  }, [liquidacionId])

  useEffect(() => {
    traer()
  }, [traer])

  if (noEncontrada) {
    return (
      <section>
        <EncabezadoDePantalla titulo="Liquidación" />
        <p role="alert" className="m-0 text-[13px] text-ink-soft">
          No encontramos esa liquidación.{' '}
          <Link to="/liquidaciones" className="text-brand underline underline-offset-2">
            Volver a liquidaciones
          </Link>
        </p>
      </section>
    )
  }

  if (detalle === null) {
    return (
      <section>
        <EncabezadoDePantalla titulo="Liquidación" volverA={VOLVER} />
        {error !== null ? (
          <p role="alert" className={clasesDeAvisoDePantalla.error}>
            {error}
          </p>
        ) : (
          <p role="status" className="m-0 text-[13px] text-ink-soft">
            Cargando liquidación…
          </p>
        )}
      </section>
    )
  }

  const liquidacion = detalle
  const anulada = liquidacion.estado === 'anulada'
  const conPagos = liquidacion.importePagado > 0
  const hayAcciones =
    puedeGestionar &&
    (liquidacion.puedeEditarse || liquidacion.puedeAnularse || liquidacion.puedeRegistrarPago)

  async function anular(motivo: string) {
    const cuantos = liquidacion.viajes.length
    const actualizada = await anularLiquidacion(liquidacion.id, motivo)

    setDetalle(actualizada)
    setAnulando(false)
    setAviso(
      `Se anuló la liquidación ${actualizada.numero}. ` +
        (cuantos === 1 ? 'Su viaje quedó disponible.' : `Sus ${cuantos} viajes quedaron disponibles.`),
    )
  }

  function alRegistrarPago(actualizada: LiquidacionDetalle) {
    const previas = new Set(liquidacion.ordenesDePago.map((orden) => orden.id))
    const nueva = actualizada.ordenesDePago.find((orden) => !previas.has(orden.id))

    setDetalle(actualizada)
    setPagando(false)

    if (nueva !== undefined) {
      setAviso(
        `Se registró la orden de pago ${nueva.numero} por ${formatearPesos(nueva.importe)}.` +
          (actualizada.estado === 'pagada' ? ' La liquidación quedó pagada.' : ''),
      )
    }
  }

  return (
    <section>
      <EncabezadoDePantalla
        titulo="Liquidación"
        volverA={VOLVER}
        resumen={
          <span className="flex flex-col gap-1.5">
            <span className="flex flex-wrap items-center gap-2">
              <TokenDeIdentificador numero={liquidacion.numero} sinNumeral />
              <Estado
                valor={liquidacion.estado}
                texto={NOMBRES_DE_ESTADO[liquidacion.estado]}
                forma="pastilla"
              />
            </span>
            <span>
              {liquidacion.transportista.razonSocial} · {formatearPeriodo(liquidacion.mes, liquidacion.anio)} ·
              Generada el {formatearFecha(liquidacion.fechaGeneracion)}
            </span>
          </span>
        }
        accionPrincipal={
          hayAcciones ? (
            <>
              {liquidacion.puedeEditarse && (
                <Boton
                  variante="secundario"
                  onClick={() => navegar(`/liquidaciones/${liquidacion.id}/editar`)}
                >
                  Editar liquidación
                </Boton>
              )}
              {liquidacion.puedeAnularse && (
                <Boton
                  variante="destructivo"
                  onClick={() => setAnulando(true)}
                  icono={<IconoAnulado className="size-3" />}
                >
                  Anular liquidación
                </Boton>
              )}
              {liquidacion.puedeRegistrarPago && (
                <Boton
                  variante="primario"
                  onClick={() => setPagando(true)}
                  icono={<IconoEnRegla className="size-3" />}
                >
                  Registrar orden de pago
                </Boton>
              )}
            </>
          ) : undefined
        }
      />

      {/* Un resultado que aparece sin que la pantalla cambie: el rol va en el `<p>` del texto ([008]). */}
      {aviso !== null && (
        <p role="status" className={cn(clasesDeAvisoDePantalla.exito, 'mb-[18px]')}>
          {aviso}
        </p>
      )}
      {error !== null && (
        <p role="alert" className={cn(clasesDeAvisoDePantalla.error, 'mb-[18px]')}>
          {error}
        </p>
      )}

      <CalloutDeEstado liquidacion={liquidacion} puedeGestionar={puedeGestionar} conPagos={conPagos} />

      <FichaCuerpo
        aside={
          <AsideDeFicha
            destacado={
              anulada ? (
                <div className="flex flex-col gap-3.5">
                  <CifraDestacada rotulo="Importe total" valor={formatearPesos(liquidacion.importeTotal)} />
                  <p className="m-0 text-[12.5px] text-faint">Anulada: no hay saldo por pagar.</p>
                </div>
              ) : (
                <div className="flex flex-col gap-3.5">
                  <CifraDestacada rotulo="Resta pagar" valor={formatearPesos(liquidacion.restaPagar ?? 0)} />
                  <dl className="m-0 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-[12.5px]">
                    <dt className="text-faint">Importe total</dt>
                    <dd className="m-0 text-right font-medium">{formatearPesos(liquidacion.importeTotal)}</dd>
                    <dt className="text-faint">Pagado</dt>
                    <dd className="m-0 text-right font-medium">{formatearPesos(liquidacion.importePagado)}</dd>
                  </dl>
                </div>
              )
            }
          >
            <BloqueDeAside titulo="Transportista">
              <p className="m-0 font-semibold">
                {liquidacion.transportista.razonSocial}
                {!liquidacion.transportista.activo && (
                  <span className="ml-2 text-[11.5px] font-medium text-faint">Inactivo</span>
                )}
              </p>
              <p className="m-0 font-mono text-[12.5px] text-faint">
                {formatearCuit(liquidacion.transportista.cuit)}
              </p>
            </BloqueDeAside>
          </AsideDeFicha>
        }
      >
        <FichaSeccion
          titulo={anulada ? 'Viajes que agrupaba' : 'Viajes liquidados'}
          id="titulo-viajes-liquidacion"
        >
          <TablaDeViajes
            viajes={liquidacion.viajes}
            titulo={anulada ? 'Viajes que agrupaba' : 'Viajes liquidados'}
            conEnlace
            className={TABLA_EN_TARJETA}
          />
        </FichaSeccion>

        <FichaSeccion titulo="Órdenes de pago" id="titulo-ordenes-de-pago">
          {liquidacion.ordenesDePago.length === 0 ? (
            <p className="m-0 px-[22px] py-5 text-[13px] text-ink-soft">
              Todavía no se registró ninguna orden de pago.
            </p>
          ) : (
            <Listado className={TABLA_EN_TARJETA}>
              <TablaDesplazable>
                <table>
                  <caption>Órdenes de pago de la liquidación {liquidacion.numero}</caption>
                  <thead>
                    <tr>
                      <th scope="col">Orden</th>
                      <th scope="col">Fecha de pago</th>
                      <th scope="col">Registrada por</th>
                      <th scope="col" className="text-right">
                        Importe
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {liquidacion.ordenesDePago.map((orden) => (
                      <tr key={orden.id}>
                        <td>
                          <TokenDeIdentificador numero={orden.numero} sinNumeral />
                        </td>
                        <td className="whitespace-nowrap">{formatearFecha(orden.fechaPago)}</td>
                        <td>
                          <span className="flex flex-col gap-0.5">
                            <span>{orden.registradaPor}</span>
                            <span className="text-[11.5px] text-faint">
                              {formatearInstante(orden.registradaEn)}
                            </span>
                          </span>
                        </td>
                        <td className="text-right font-bold whitespace-nowrap">
                          {formatearPesos(orden.importe)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </TablaDesplazable>
            </Listado>
          )}
        </FichaSeccion>

        <FichaSeccion titulo="Historial" id="titulo-historial-liquidacion">
          <div className="px-[22px] py-5">
            <LineaDeTiempo>
              {liquidacion.historial.map((entrada, indice) => (
                <EntradaDeLineaDeTiempo
                  key={`${entrada.ocurridoEn}-${indice}`}
                  actual={indice === liquidacion.historial.length - 1}
                  cuando={formatearInstante(entrada.ocurridoEn)}
                  que={`${VERBO[entrada.operacion]} por ${entrada.usuario}`}
                  motivo={detalleDeEntrada(entrada, liquidacion.motivoAnulacion)}
                />
              ))}
            </LineaDeTiempo>
          </div>
        </FichaSeccion>
      </FichaCuerpo>

      {pagando && (
        <DialogoOrdenDePago
          numero={liquidacion.numero}
          transportista={liquidacion.transportista.razonSocial}
          restaPagar={liquidacion.restaPagar ?? 0}
          fechaGeneracion={liquidacion.fechaGeneracion}
          hoy={hoyEnIso()}
          onEnviar={(orden) => registrarOrdenDePago(liquidacion.id, orden)}
          onRegistrada={alRegistrarPago}
          onCancelar={() => setPagando(false)}
        />
      )}

      {anulando && (
        <DialogoAnulacion
          numero={liquidacion.numero}
          cantidadDeViajes={liquidacion.viajes.length}
          onAnular={anular}
          onCancelar={() => setAnulando(false)}
        />
      )}
    </section>
  )
}

const VERBO: Record<CambioDeLiquidacion['operacion'], string> = {
  generacion: 'Generada',
  edicion: 'Editada',
  anulacion: 'Anulada',
  pagada: 'Pagada',
}

/** `Quitó #13 · Agregó #21`, omitiendo la parte sin viajes; en la anulación, su motivo. */
function detalleDeEntrada(entrada: CambioDeLiquidacion, motivoAnulacion: string | null): string | undefined {
  if (entrada.operacion === 'anulacion') {
    return motivoAnulacion === null ? undefined : `Motivo: ${motivoAnulacion}`
  }

  if (entrada.operacion !== 'edicion') {
    return undefined
  }

  const partes = [
    entrada.viajesQuitados.length > 0 ? `Quitó ${numerosDeViaje(entrada.viajesQuitados)}` : null,
    entrada.viajesAgregados.length > 0 ? `Agregó ${numerosDeViaje(entrada.viajesAgregados)}` : null,
  ].filter((parte) => parte !== null)

  return partes.length === 0 ? undefined : partes.join(' · ')
}

function numerosDeViaje(numeros: number[]): string {
  return numeros.map((numero) => `#${numero}`).join(', ')
}

interface CalloutProps {
  liquidacion: LiquidacionDetalle
  puedeGestionar: boolean
  conPagos: boolean
}

/**
 * Qué pasa **y** qué hacer (principio 4 de gt-ui). Quien sólo consulta ve el mismo aviso sin la
 * instrucción que no puede seguir.
 */
function CalloutDeEstado({ liquidacion, puedeGestionar, conPagos }: CalloutProps) {
  let contenido: React.ReactNode = null

  if (liquidacion.estado === 'anulada') {
    contenido = (
      <Callout tono="anulado" titulo="Liquidación anulada.">
        Motivo: {liquidacion.motivoAnulacion}. Sus viajes quedaron disponibles para liquidarse de nuevo desde{' '}
        {puedeGestionar ? (
          <Link to="/liquidaciones/nueva" className="text-brand underline underline-offset-2">
            Generar liquidación
          </Link>
        ) : (
          'Generar liquidación'
        )}
        .
      </Callout>
    )
  } else if (liquidacion.estado === 'pagada') {
    contenido = (
      <Callout tono="rendido" titulo="Liquidación pagada — cerrada.">
        No se edita, no se anula y no admite más órdenes de pago.
      </Callout>
    )
  } else if (conPagos) {
    contenido = (
      <Callout tono="pendiente" titulo="Tiene pagos registrados — ya no se edita ni se anula.">
        {puedeGestionar ? 'Registrá el resto del pago para cerrarla.' : null}
      </Callout>
    )
  } else if (!liquidacion.puedeEditarse) {
    contenido = (
      <Callout tono="pendiente" titulo="El transportista ya no se puede liquidar — la liquidación no se edita.">
        Se puede seguir pagando; si está mal armada, anulala.
      </Callout>
    )
  }

  return contenido === null ? null : <div className="mb-[18px]">{contenido}</div>
}

/** Hoy en `yyyy-MM-dd`, construido con los tres números para no pasar por `new Date(iso)`. */
function hoyEnIso(): string {
  const hoy = new Date()

  return `${hoy.getFullYear()}-${String(hoy.getMonth() + 1).padStart(2, '0')}-${String(hoy.getDate()).padStart(2, '0')}`
}
