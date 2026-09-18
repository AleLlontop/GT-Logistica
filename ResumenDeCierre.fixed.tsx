import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { formatearPesos } from '../../../compartido/moneda'
import { AsideDeFicha, CifraDestacada } from '../../../compartido/ui/AsideDeFicha'
import { Boton } from '../../../compartido/ui/Boton'
import { clasesDeAvisoDePantalla } from '../../../compartido/ui/clases'
import { cn } from '../../../compartido/ui/cn'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { FichaCuerpo, FichaSeccion } from '../../../compartido/ui/Ficha'
import { IconoEnRegla } from '../../../compartido/ui/iconos'
import { Listado } from '../../../compartido/ui/Listado'
import { TablaDeMovimientos } from '../componentes/TablaDeMovimientos'
import {
  cerrarCaja,
  detalleDeError,
  esSinPermiso,
  MENSAJE_INESPERADO,
  MENSAJE_SIN_PERMISO,
  mensajeDeRechazo,
  obtenerResumenDeCierre,
  type ResumenDeCierre as Resumen,
} from '../servicios/servicioCaja'

const TITULO = 'Cerrar caja'

export const MENSAJE_CAJA_CERRADA = 'Caja cerrada con éxito.'

export const MENSAJE_SIN_MOVIMIENTOS =
  'Esta caja no tiene movimientos. El saldo final es igual al saldo inicial.'

/**
 * El resumen previo al cierre y su confirmación (User Story 3, FR-016 a FR-022).
 *
 * **Es la confirmación**: no hay diálogo aparte ni campos. *Confirmar cierre* manda el saldo final que se
 * está mostrando, y si entró un movimiento entre medio el servidor no cierra: devuelve el resumen nuevo,
 * que reemplaza al de la pantalla para confirmar de nuevo (research §3). *Cancelar* vuelve sin llamar al
 * servidor (CA7).
 *
 * **El aviso sin permiso lo decide el `403` de su carga**, que exige gestionar (convención [010]).
 */
export function ResumenDeCierre() {
  const { id } = useParams()
  const cajaId = Number(id)
  const navegar = useNavigate()

  const [resumen, setResumen] = useState<Resumen | null>(null)
  const [sinPermiso, setSinPermiso] = useState(false)
  // Al cargar: no existe, está cerrada o es de otro. Se dice y no se ofrece confirmar.
  const [noCerrable, setNoCerrable] = useState<string | null>(null)
  const [errorDeCarga, setErrorDeCarga] = useState<string | null>(null)

  const [enviando, setEnviando] = useState(false)
  const [anuncio, setAnuncio] = useState<string | null>(null)
  const [alerta, setAlerta] = useState<string | null>(null)

  const volver = { ruta: `/caja/${cajaId}`, etiqueta: 'Volver a la caja' }

  useEffect(() => {
    obtenerResumenDeCierre(cajaId)
      .then(setResumen)
      .catch((fallo) => {
        if (esSinPermiso(fallo)) {
          setSinPermiso(true)
        } else if (fallo instanceof ErrorHttp && (fallo.estado === 404 || fallo.estado === 409)) {
          setNoCerrable(detalleDeError(fallo)?.mensaje ?? MENSAJE_INESPERADO)
        } else {
          setErrorDeCarga('No pudimos traer el resumen. Volvé a intentar en unos minutos.')
        }
      })
  }, [cajaId])

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

  if (noCerrable !== null) {
    return (
      <section>
        <EncabezadoDePantalla titulo={TITULO} volverA={volver} />
        <p role="alert" className={clasesDeAvisoDePantalla.advertencia}>
          {noCerrable}{' '}
          <Link to="/caja" className="underline underline-offset-2">
            Ir a cajas
          </Link>
        </p>
      </section>
    )
  }

  if (resumen === null) {
    return (
      <section>
        <EncabezadoDePantalla titulo={TITULO} volverA={volver} />
        {errorDeCarga !== null ? (
          <p role="alert" className={clasesDeAvisoDePantalla.error}>
            {errorDeCarga}
          </p>
        ) : (
          <p role="status" className="m-0 text-[13px] text-ink-soft">
            Cargando resumen…
          </p>
        )}
      </section>
    )
  }

  const mostrado = resumen

  async function confirmar() {
    setEnviando(true)
    setAlerta(null)

    try {
      const resultado = await cerrarCaja(cajaId, mostrado.saldoFinal)

      if (resultado.tipo === 'cerrada') {
        navegar(`/caja/${cajaId}`, { state: { aviso: MENSAJE_CAJA_CERRADA } })
        return
      }

      // El resumen cambió: se muestra el nuevo y se pide confirmar de nuevo (US3 esc. 7).
      setResumen(resultado.resumen)
      setAnuncio(resultado.mensaje)
    } catch (fallo) {
      setAnuncio(null)
      setAlerta(mensajeDeRechazo(fallo))
    }

    setEnviando(false)
  }

  return (
    <section>
      <EncabezadoDePantalla
        titulo={TITULO}
        volverA={volver}
        accionPrincipal={
          <>
            <Boton variante="secundario" onClick={() => navegar(`/caja/${cajaId}`)} disabled={enviando}>
              Cancelar
            </Boton>
            <Boton
              variante="primario"
              onClick={() => void confirmar()}
              disabled={enviando}
              icono={<IconoEnRegla className="size-3" />}
            >
              Confirmar cierre
            </Boton>
          </>
        }
      />

      {/* Los números cambiaron sin que la pantalla cambie: el rol va en el `<p>` del texto ([008]). */}
      {anuncio !== null && (
        <p role="status" className={cn(clasesDeAvisoDePantalla.advertencia, 'mb-[18px]')}>
          {anuncio}
        </p>
      )}
      {alerta !== null && (
        <p role="alert" className={cn(clasesDeAvisoDePantalla.error, 'mb-[18px]')}>
          {alerta}
        </p>
      )}

      <FichaCuerpo
        aside={
          <AsideDeFicha
            destacado={
              <div className="flex flex-col gap-3.5">
                <CifraDestacada rotulo="Saldo final" valor={formatearPesos(mostrado.saldoFinal)} />
                <p className="m-0 text-[12.5px] text-faint">
                  Queda registrado al confirmar y la caja ya no admite movimientos.
                </p>
              </div>
            }
          />
        }
      >
        <FichaSeccion titulo="Resumen" id="titulo-resumen-cierre">
          <dl>
            <dt>Saldo inicial</dt>
            <dd className="font-bold">{formatearPesos(mostrado.saldoInicial)}</dd>

            <dt>Total de ingresos</dt>
            <dd className="font-bold">{formatearPesos(mostrado.totalIngresos)}</dd>

            <dt>Total de egresos</dt>
            <dd className="font-bold">{formatearPesos(mostrado.totalEgresos)}</dd>
          </dl>
        </FichaSeccion>
      </FichaCuerpo>

      <div className="mt-[18px]">
        <FichaSeccion titulo="Movimientos" id="titulo-movimientos-cierre">
          {mostrado.movimientos.length === 0 ? (
            <EstadoVacio caso="vacio" className="border-0 shadow-none">
              {MENSAJE_SIN_MOVIMIENTOS}
            </EstadoVacio>
          ) : (
            <Listado className="rounded-none border-0 shadow-none">
              <TablaDeMovimientos titulo="Movimientos de la caja" movimientos={mostrado.movimientos} />
            </Listado>
          )}
        </FichaSeccion>
      </div>
    </section>
  )
}
