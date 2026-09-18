import { useCallback, useEffect, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { formatearInstante } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { AsideDeFicha, CifraDestacada } from '../../../compartido/ui/AsideDeFicha'
import {
  clasesDeAvisoDePantalla,
  clasesDeBoton,
  clasesDeCirculoDeIcono,
} from '../../../compartido/ui/clases'
import { cn } from '../../../compartido/ui/cn'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { Estado } from '../../../compartido/ui/Estado'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { FichaCuerpo, FichaSeccion } from '../../../compartido/ui/Ficha'
import { IconoNuevo } from '../../../compartido/ui/iconos'
import { Paginacion } from '../../../compartido/ui/Paginacion'
import { TablaDeMovimientos } from '../componentes/TablaDeMovimientos'
import {
  esSinPermiso,
  listarMovimientosDeCaja,
  MENSAJE_SIN_PERMISO,
  NOMBRES_DE_ESTADO,
  obtenerCaja,
  type CajaDetalle,
  type PaginaDeMovimientos,
} from '../servicios/servicioCaja'

const TITULO = 'Caja'

const VOLVER = { ruta: '/caja', etiqueta: 'Volver a cajas' }

interface Props {
  /** `caja.gestionar`. Sin él, la caja se mira sin acciones aunque sea propia. */
  puedeGestionar: boolean
}

/**
 * Detalle de una caja: resumen en vivo —saldo inicial, totales y saldo actual, calculados por el servidor—
 * y sus movimientos (SC-007, CA5).
 *
 * **Ofrece operar sólo si el permiso y `puedeOperar` lo admiten**: abierta y propia. *Registrar movimiento*
 * es la primaria, porque es lo que se repite en el día; *Cerrar caja* es secundaria, porque ocurre una vez
 * (plan §UI Design Check). Ocultarlas es una cortesía: la restricción es el servidor.
 */
export function DetalleDeCaja({ puedeGestionar }: Props) {
  const { id } = useParams()
  const cajaId = Number(id)
  const ubicacion = useLocation()

  const [caja, setCaja] = useState<CajaDetalle | null>(null)
  const [noEncontrada, setNoEncontrada] = useState(false)
  const [sinPermiso, setSinPermiso] = useState(false)
  const [errorDeCarga, setErrorDeCarga] = useState<string | null>(null)

  const [pagina, setPagina] = useState(1)
  const [movimientos, setMovimientos] = useState<PaginaDeMovimientos | null>(null)

  // La confirmación de apertura, movimiento o cierre viaja con la navegación y se anuncia acá.
  const aviso = (ubicacion.state as { aviso?: string } | null)?.aviso ?? null

  useEffect(() => {
    obtenerCaja(cajaId)
      .then((traida) => {
        setCaja(traida)
        setErrorDeCarga(null)
      })
      .catch((fallo) => {
        if (fallo instanceof ErrorHttp && fallo.estado === 404) {
          setNoEncontrada(true)
        } else if (esSinPermiso(fallo)) {
          setSinPermiso(true)
        } else {
          setErrorDeCarga('No pudimos traer la caja. Volvé a intentar en unos minutos.')
        }
      })
  }, [cajaId])

  const traerMovimientos = useCallback(() => {
    listarMovimientosDeCaja(cajaId, pagina)
      .then(setMovimientos)
      .catch(() => setMovimientos(null))
  }, [cajaId, pagina])

  useEffect(() => {
    traerMovimientos()
  }, [traerMovimientos])

  if (sinPermiso) {
    return (
      <section>
        <EncabezadoDePantalla titulo={TITULO} />
        <p role="alert" className={clasesDeAvisoDePantalla.advertencia}>
          {MENSAJE_SIN_PERMISO}
        </p>
      </section>
    )
  }

  if (noEncontrada) {
    return (
      <section>
        <EncabezadoDePantalla titulo={TITULO} />
        <p role="alert" className="m-0 text-[13px] text-ink-soft">
          No encontramos esa caja.{' '}
          <Link to="/caja" className="text-brand underline underline-offset-2">
            Volver a cajas
          </Link>
        </p>
      </section>
    )
  }

  if (caja === null) {
    return (
      <section>
        <EncabezadoDePantalla titulo={TITULO} volverA={VOLVER} />
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

  const opera = puedeGestionar && caja.puedeOperar
  const cerrada = caja.estado === 'cerrada'

  const acciones = opera ? (
    <>
      <Link to={`/caja/${caja.id}/cierre`} className={clasesDeBoton('secundario')}>
        Cerrar caja
      </Link>
      <Link to={`/caja/${caja.id}/movimientos/nuevo`} className={clasesDeBoton('primario')}>
        Registrar movimiento
        <span aria-hidden="true" className={clasesDeCirculoDeIcono}>
          <IconoNuevo className="size-3" />
        </span>
      </Link>
    </>
  ) : undefined

  return (
    <section>
      <EncabezadoDePantalla
        titulo={TITULO}
        volverA={VOLVER}
        resumen={
          <span className="flex flex-col gap-1.5">
            <span className="flex flex-wrap items-center gap-2">
              <Estado valor={caja.estado} texto={NOMBRES_DE_ESTADO[caja.estado]} forma="pastilla" />
            </span>
            <span>
              {caja.responsable.nombre} · abierta el {formatearInstante(caja.fechaApertura)}
              {caja.fechaCierre !== null && ` · cerrada el ${formatearInstante(caja.fechaCierre)}`}
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

      <FichaCuerpo
        aside={
          <AsideDeFicha
            destacado={
              <div className="flex flex-col gap-3.5">
                {cerrada && caja.saldoFinal !== null ? (
                  <CifraDestacada rotulo="Saldo final" valor={formatearPesos(caja.saldoFinal)} />
                ) : (
                  <CifraDestacada rotulo="Saldo actual" valor={formatearPesos(caja.saldoActual)} />
                )}
                <p className="m-0 text-[12.5px] text-faint">
                  {cerrada
                    ? 'Cerrada: ya no admite movimientos.'
                    : 'Saldo inicial más ingresos menos egresos.'}
                </p>
              </div>
            }
          />
        }
      >
        <FichaSeccion titulo="Resumen" id="titulo-resumen-caja">
          <dl>
            <dt>Saldo inicial</dt>
            <dd className="font-bold">{formatearPesos(caja.saldoInicial)}</dd>

            <dt>Total de ingresos</dt>
            <dd className="font-bold">{formatearPesos(caja.totalIngresos)}</dd>

            <dt>Total de egresos</dt>
            <dd className="font-bold">{formatearPesos(caja.totalEgresos)}</dd>

            <dt>{cerrada ? 'Saldo final' : 'Saldo actual'}</dt>
            <dd className="font-bold">
              {formatearPesos(cerrada && caja.saldoFinal !== null ? caja.saldoFinal : caja.saldoActual)}
            </dd>
          </dl>
        <FichaSeccion titulo="Movimientos" id="titulo-movimientos-caja">
          {movimientos === null ? (
            <p role="status" className="m-0 px-[22px] py-5 text-[13px] text-ink-soft">
              Cargando movimientos…
            </p>
          ) : movimientos.items.length === 0 ? (
            <EstadoVacio caso="vacio" className="border-0 shadow-none">
              Esta caja no tiene movimientos.
            </EstadoVacio>
          ) : (
            <>
              <TablaDeMovimientos titulo="Movimientos de la caja" movimientos={movimientos.items} />
              <div className="px-[22px] pb-4">
                <Paginacion
                  pagina={movimientos.pagina}
                  total={movimientos.total}
                  tamanioPagina={movimientos.tamanioPagina}
                  nombrePlural="movimientos"
                  onCambiarPagina={setPagina}
                />
              </div>
            </>
          )}
          <p className="m-0 px-[22px] pb-4 text-[12.5px]">
            <Link to={`/movimientos-caja?cajaId=${caja.id}`} className="text-brand underline underline-offset-2">
              Ver en movimientos de caja
            </Link>
          </p>
        </FichaSeccion>
      </FichaCuerpo>
    </section>
  )
}
