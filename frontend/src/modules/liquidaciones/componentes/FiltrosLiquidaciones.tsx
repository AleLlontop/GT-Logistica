import { useEffect, useState, type ReactNode } from 'react'
import { aniosDelPeriodo } from '../../../compartido/fechas'
import { Filtros, FranjaDeFiltros } from '../../../compartido/ui/Filtros'
import { clasesDeEtiquetaDeFiltro, clasesDeFiltro } from '../../../compartido/ui/clases'
import {
  listarTransportistas,
  MESES,
  NOMBRES_DE_ESTADO,
  type EstadoLiquidacion,
  type FiltrosLiquidaciones as ValorDeFiltros,
  type TransportistaResumen,
} from '../servicios/servicioLiquidaciones'

interface Props {
  valor: ValorDeFiltros
  onCambio: (filtros: ValorDeFiltros) => void
  /** Lo que declara qué se está mostrando. Un listado nunca oculta filas en silencio ([003]). */
  declaracion?: ReactNode
  /** Cantidad y criterio de orden. */
  resumen?: ReactNode
}

/**
 * Los cuatro filtros del listado, combinables (FR-022).
 *
 * **El de transportista ofrece también los dados de baja**: sus liquidaciones siguen existiendo, y no
 * poder filtrarlas haría inalcanzable justo la consulta de lo que se le debe a un fletero que ya no opera.
 *
 * Sin buscador por texto: son cuatro desplegables y la franja de arriba no se dibuja (Principio III).
 */
export function FiltrosLiquidaciones({ valor, onCambio, declaracion, resumen }: Props) {
  const [transportistas, setTransportistas] = useState<TransportistaResumen[]>([])

  useEffect(() => {
    listarTransportistas(true)
      .then((respuesta) => setTransportistas(respuesta.transportistas))
      .catch(() => setTransportistas([]))
  }, [])

  function cambiar(parcial: Partial<ValorDeFiltros>) {
    onCambio({ ...valor, ...parcial })
  }

  function numeroOVacio(texto: string): number | '' {
    return texto === '' ? '' : Number(texto)
  }

  return (
    <Filtros declaracion={declaracion} resumen={resumen}>
      <FranjaDeFiltros>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-transportista-liquidacion" className={clasesDeEtiquetaDeFiltro}>
            Transportista
          </label>
          <select
            id="filtro-transportista-liquidacion"
            value={valor.transportistaId}
            onChange={(evento) => cambiar({ transportistaId: numeroOVacio(evento.target.value) })}
            className={clasesDeFiltro(valor.transportistaId !== '')}
          >
            <option value="">Todos los transportistas</option>
            {transportistas.map((transportista) => (
              <option key={transportista.id} value={transportista.id}>
                {transportista.activo
                  ? transportista.razonSocial
                  : `${transportista.razonSocial} (Inactivo)`}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-mes-liquidacion" className={clasesDeEtiquetaDeFiltro}>
            Mes
          </label>
          <select
            id="filtro-mes-liquidacion"
            value={valor.mes}
            onChange={(evento) => cambiar({ mes: numeroOVacio(evento.target.value) })}
            className={clasesDeFiltro(valor.mes !== '')}
          >
            <option value="">Todos los meses</option>
            {MESES.map((numero) => (
              <option key={numero} value={numero}>
                {String(numero).padStart(2, '0')}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-anio-liquidacion" className={clasesDeEtiquetaDeFiltro}>
            Año
          </label>
          <select
            id="filtro-anio-liquidacion"
            value={valor.anio}
            onChange={(evento) => cambiar({ anio: numeroOVacio(evento.target.value) })}
            className={clasesDeFiltro(valor.anio !== '')}
          >
            <option value="">Todos los años</option>
            {aniosDelPeriodo().map((numero) => (
              <option key={numero} value={numero}>
                {numero}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-estado-liquidacion" className={clasesDeEtiquetaDeFiltro}>
            Estado
          </label>
          <select
            id="filtro-estado-liquidacion"
            value={valor.estado}
            onChange={(evento) => cambiar({ estado: evento.target.value as EstadoLiquidacion | '' })}
            className={clasesDeFiltro(valor.estado !== '')}
          >
            <option value="">Todos los estados</option>
            <option value="pendiente">{NOMBRES_DE_ESTADO.pendiente}</option>
            <option value="pagada">{NOMBRES_DE_ESTADO.pagada}</option>
            <option value="anulada">{NOMBRES_DE_ESTADO.anulada}</option>
          </select>
        </div>
      </FranjaDeFiltros>
    </Filtros>
  )
}
