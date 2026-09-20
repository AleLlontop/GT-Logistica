import { useCallback, useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { formatearFecha } from '../../../compartido/fechas'
import { clasesDeAvisoDePantalla } from '../../../compartido/ui/clases'
import { cn } from '../../../compartido/ui/cn'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { Listado } from '../../../compartido/ui/Listado'
import { Paginacion } from '../../../compartido/ui/Paginacion'
import { GenerarReporte } from '../../reportes/componentes/GenerarReporte'
import { FiltrosDeMovimientos } from '../componentes/FiltrosDeMovimientos'
import { TablaDeMovimientos } from '../componentes/TablaDeMovimientos'
import {
  esSinPermiso,
  FILTROS_MOVIMIENTOS_INICIALES,
  listarMovimientos,
  MENSAJE_SIN_PERMISO,
  rangoInvertido,
  type FiltrosMovimientos,
  type PaginaDeMovimientos,
} from '../servicios/servicioCaja'

const TITULO = 'Movimientos de caja'

export const MENSAJE_SIN_MOVIMIENTOS = 'No existen movimientos para los filtros aplicados.'

/** Qué se está mostrando, en palabras: un listado nunca oculta filas en silencio (FR-027, [003]). */
function declaracionDe(filtros: FiltrosMovimientos): string {
  const partes: string[] = []

  if (filtros.desde !== '' && filtros.hasta !== '') {
    partes.push(`del ${formatearFecha(filtros.desde)} al ${formatearFecha(filtros.hasta)}`)
  } else if (filtros.desde !== '') {
    partes.push(`desde el ${formatearFecha(filtros.desde)}`)
  } else if (filtros.hasta !== '') {
    partes.push(`hasta el ${formatearFecha(filtros.hasta)}`)
  }

  if (filtros.cajaId !== '') {
    partes.push('de la caja elegida')
  }

  return partes.length === 0
    ? 'Mostrando los movimientos de todas las cajas.'
    : `Mostrando los movimientos ${partes.join(' ')}.`
}

/**
 * Consulta de movimientos por rango de fechas y/o por caja (User Story 4, FR-023 a FR-025). **De sólo
 * lectura**: no hay acción primaria, y el gerente la usa sin poder operar.
 *
 * Admite `?cajaId=` para llegar filtrado desde una caja.
 */
interface Props {
  /** `reportes.emitir`. Sin él, *Generar reporte* no se dibuja (Módulo 12, FR-013). */
  puedeEmitirReportes: boolean
}

export function ConsultaDeMovimientos({ puedeEmitirReportes }: Props) {
  const [parametros] = useSearchParams()
  const cajaInicial = Number(parametros.get('cajaId'))

  const [filtros, setFiltros] = useState<FiltrosMovimientos>({
    ...FILTROS_MOVIMIENTOS_INICIALES,
    cajaId: Number.isInteger(cajaInicial) && cajaInicial > 0 ? cajaInicial : '',
  })
  const [pagina, setPagina] = useState(1)
  const [resultado, setResultado] = useState<PaginaDeMovimientos | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [sinPermiso, setSinPermiso] = useState(false)

  const invertido = rangoInvertido(filtros)

  const traer = useCallback(() => {
    // Con el rango invertido no se consulta.
    if (rangoInvertido(filtros)) {
      return
    }

    listarMovimientos(filtros, pagina)
      .then((traida) => {
        setResultado(traida)
        setError(null)
      })
      .catch((fallo) => {
        if (esSinPermiso(fallo)) {
          setSinPermiso(true)
        } else {
          setError('No pudimos traer los movimientos. Volvé a intentar en unos minutos.')
        }
      })
  }, [filtros, pagina])

  useEffect(() => {
    traer()
  }, [traer])

  // Cualquier cambio de filtro vuelve a la página 1.
  function cambiarFiltros(nuevos: FiltrosMovimientos) {
    setFiltros(nuevos)
    setPagina(1)
  }

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

  const visible = invertido ? null : resultado

  return (
    <section>
      <EncabezadoDePantalla
        titulo={TITULO}
        /*
          Módulo 12: la acción es **secundaria** y la pantalla **sigue sin acción primaria** — es de
          sólo lectura, y el gerente la usa sin poder operar (FR-005).

          Se le pasan los filtros aplicados y el `total` del listado: el reporte abarca todas las
          filas del filtro, no las de la página visible (FR-007).
        */
        accionSecundaria={
          <GenerarReporte
            reporte="movimientos-caja"
            puedeEmitir={puedeEmitirReportes}
            cantidadDeFilas={visible?.total ?? 0}
            filtros={{ desde: filtros.desde, hasta: filtros.hasta, cajaId: filtros.cajaId }}
          />
        }
      />

      {error !== null && (
        <p role="alert" className={cn(clasesDeAvisoDePantalla.error, 'mb-4')}>
          {error}
        </p>
      )}

      <Listado>
        <FiltrosDeMovimientos
          valor={filtros}
          onCambio={cambiarFiltros}
          declaracion={<span role="status">{declaracionDe(filtros)}</span>}
          resumen={
            visible !== null && (
              <>
                <span>
                  {visible.total} {visible.total === 1 ? 'movimiento' : 'movimientos'}
                </span>
                <span>Ordenado por fecha, del más reciente al más antiguo</span>
              </>
            )
          }
        />

        {invertido && (
          <EstadoVacio caso="sinCoincidencias" className="border-0 shadow-none">
            Corregí el rango de fechas para ver los movimientos.
          </EstadoVacio>
        )}

        {!invertido && resultado === null && error === null && (
          <EstadoVacio caso="cargando" className="border-0 shadow-none">
            Cargando movimientos…
          </EstadoVacio>
        )}

        {visible !== null && visible.items.length === 0 && (
          <EstadoVacio caso="sinCoincidencias" className="border-0 shadow-none">
            {MENSAJE_SIN_MOVIMIENTOS}
          </EstadoVacio>
        )}

        {visible !== null && visible.items.length > 0 && (
          <TablaDeMovimientos titulo={TITULO} movimientos={visible.items} />
        )}
      </Listado>

      {visible !== null && (
        <Paginacion
          pagina={visible.pagina}
          total={visible.total}
          tamanioPagina={visible.tamanioPagina}
          nombrePlural="movimientos"
          onCambiarPagina={setPagina}
        />
      )}
    </section>
  )
}
