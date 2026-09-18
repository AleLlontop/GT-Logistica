import { useEffect, useState, type ReactNode } from 'react'
import { formatearFecha } from '../../../compartido/fechas'
import { Boton } from '../../../compartido/ui/Boton'
import { clasesDeEtiquetaDeFiltro, clasesDeFiltro } from '../../../compartido/ui/clases'
import { Filtros, FranjaDeFiltros } from '../../../compartido/ui/Filtros'
import {
  FILTROS_MOVIMIENTOS_INICIALES,
  listarCajas,
  rangoInvertido,
  type CajaListado,
  type FiltrosMovimientos,
} from '../servicios/servicioCaja'

/** Un filtro en error: el mismo control, con el borde y el fondo del error (FR-023). */
const EN_ERROR = 'border-danger bg-danger-bg'

interface Props {
  valor: FiltrosMovimientos
  onCambio: (filtros: FiltrosMovimientos) => void
  /** Lo que declara qué se está mostrando. Un listado nunca oculta filas en silencio ([003]). */
  declaracion?: ReactNode
  resumen?: ReactNode
}

/**
 * Rango de fechas y caja, combinables (FR-023, FR-024). Cada caja se nombra por su responsable y el día de
 * apertura: no tiene número visible.
 *
 * **El rango invertido marca los dos campos** y la pantalla no consulta mientras tanto.
 */
export function FiltrosDeMovimientos({ valor, onCambio, declaracion, resumen }: Props) {
  const [cajas, setCajas] = useState<CajaListado[]>([])

  useEffect(() => {
    listarCajas(1)
      .then((pagina) => setCajas(pagina.items))
      .catch(() => setCajas([]))
  }, [])

  function cambiar(parcial: Partial<FiltrosMovimientos>) {
    onCambio({ ...valor, ...parcial })
  }

  const invertido = rangoInvertido(valor)

  // La caja pedida por dirección puede no estar en la primera página: se ofrece igual.
  const fuera = valor.cajaId !== '' && !cajas.some((caja) => caja.id === valor.cajaId)

  return (
    <Filtros declaracion={declaracion} resumen={resumen}>
      <FranjaDeFiltros>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-desde-movimientos" className={clasesDeEtiquetaDeFiltro}>
            Desde
          </label>
          <input
            id="filtro-desde-movimientos"
            type="date"
            value={valor.desde}
            onChange={(evento) => cambiar({ desde: evento.target.value })}
            aria-invalid={invertido}
            aria-describedby={invertido ? 'error-rango-movimientos' : undefined}
            className={clasesDeFiltro(valor.desde !== '', invertido ? EN_ERROR : undefined)}
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-hasta-movimientos" className={clasesDeEtiquetaDeFiltro}>
            Hasta
          </label>
          <input
            id="filtro-hasta-movimientos"
            type="date"
            value={valor.hasta}
            onChange={(evento) => cambiar({ hasta: evento.target.value })}
            aria-invalid={invertido}
            aria-describedby={invertido ? 'error-rango-movimientos' : undefined}
            className={clasesDeFiltro(valor.hasta !== '', invertido ? EN_ERROR : undefined)}
          />
          {invertido && (
            <p id="error-rango-movimientos" className="m-0 text-[11.5px] font-medium text-danger-text">
              La fecha desde es posterior a la hasta.
            </p>
          )}
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-caja-movimientos" className={clasesDeEtiquetaDeFiltro}>
            Caja
          </label>
          <select
            id="filtro-caja-movimientos"
            value={valor.cajaId}
            onChange={(evento) =>
              cambiar({ cajaId: evento.target.value === '' ? '' : Number(evento.target.value) })
            }
            className={clasesDeFiltro(valor.cajaId !== '')}
          >
            <option value="">Todas las cajas</option>
            {fuera && <option value={valor.cajaId}>Caja elegida</option>}
            {cajas.map((caja) => (
              <option key={caja.id} value={caja.id}>
                {caja.responsable.nombre} · {formatearFecha(caja.fechaApertura)}
              </option>
            ))}
          </select>
        </div>

        <div className="ml-auto flex flex-col gap-1.5">
          <label className={clasesDeEtiquetaDeFiltro + ' invisible'} aria-hidden="true">
            Limpiar
          </label>
          <Boton variante="secundario" tamanio="chico" onClick={() => onCambio(FILTROS_MOVIMIENTOS_INICIALES)}>
            Limpiar filtros
          </Boton>
        </div>
      </FranjaDeFiltros>
    </Filtros>
  )
}
