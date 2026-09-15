import type { ReactNode } from 'react'
import { cn } from './cn'

/**
 * El indicador de estado, único para los cinco juegos de estados del sistema (data-model §3).
 *
 * **`texto` es obligatorio.** No es una comodidad: es la forma de que FR-055 —ninguna información se
 * comunica sólo por color— no dependa de que alguien se acuerde. La primitiva no se puede dibujar sin
 * la palabra, y esa palabra es siempre la que ya estaba en pantalla: sale de `NombresDeEstado` y de
 * los `TEXTO_ESTADO_*` de cada módulo, que FR-066 congela.
 *
 * **`forma` también es obligatoria** y sin valor por defecto, por la misma razón que `variante` en
 * `Boton`: una forma que se elige sola es una decisión que nadie revisa. Distingue el estado **del
 * que trata la pantalla** —`pastilla`— del estado de alta y baja que sólo acompaña —`punto`—, para
 * que en una fila de chofer el semáforo de documentación no compita con *Activo*.
 *
 * A la palabra se le suman un color **y una forma**, para que la distinción sobreviva a una captura
 * en escala de grises y a cualquier daltonismo (SC-014).
 */

export type FormaDeEstado = 'pastilla' | 'punto'

type Tono = 'rendido' | 'pendiente' | 'facturado' | 'anulado' | 'danger' | 'neutro'

/** El relleno y el texto de cada tono, para la forma de pastilla. */
const PASTILLAS: Record<Tono, string> = {
  rendido: 'bg-estado-rendido-bg text-estado-rendido',
  pendiente: 'bg-estado-pendiente-bg text-estado-pendiente',
  facturado: 'bg-estado-facturado-bg text-estado-facturado',
  anulado: 'bg-estado-anulado-bg text-estado-anulado',
  danger: 'bg-danger-bg text-danger-text',
  neutro: 'bg-surface-mute text-ink-soft',
}

/** El color del punto, para la forma de punto. El texto va siempre en `ink-soft`. */
const PUNTOS: Record<Tono, string> = {
  rendido: 'bg-estado-rendido',
  pendiente: 'bg-estado-pendiente',
  facturado: 'bg-estado-facturado',
  anulado: 'bg-estado-anulado',
  danger: 'bg-danger',
  neutro: 'bg-dim',
}

/**
 * De qué tono es cada valor. Las claves son los valores que el API ya devuelve en camelCase
 * (convención [003]); lo que no figura cae en `neutro`, que es un estado válido y no un error.
 */
const TONO_POR_VALOR: Record<string, Tono> = {
  // Documentación (Módulos 3 y 4)
  enRegla: 'rendido',
  vigente: 'rendido',
  proximaAvencer: 'pendiente',
  vencida: 'danger',
  sinDocumentacion: 'neutro',

  // Viaje (Módulo 5)
  pendiente: 'pendiente',
  enCurso: 'facturado',
  rendido: 'rendido',
  facturado: 'facturado',
  anulado: 'anulado',

  // Factura (Módulo 6)
  pagada: 'rendido',
  anulada: 'anulado',

  // Adelanto (Módulo 10). `pendiente` y `anulado` ya están. `rechazado` comparte tono con `anulado`:
  // los dos son finales y no cuentan, y la palabra los distingue (FR-039).
  aprobado: 'rendido',
  rechazado: 'anulado',

  // Vehículo (Módulo 4)
  disponible: 'rendido',
  enViaje: 'facturado',
  fueraDeServicio: 'pendiente',

  // Alta y baja
  activo: 'rendido',
  activa: 'rendido',
  inactivo: 'anulado',
  inactiva: 'anulado',
  dadoDeBaja: 'anulado',
  bloqueado: 'danger',
}

interface Props {
  /** El valor tal como lo devuelve el API, en camelCase. Un valor desconocido cae en neutro. */
  valor: string
  /** La palabra que ya está en pantalla. Obligatoria: el color nunca comunica solo (FR-055). */
  texto: string
  /**
   * `pastilla` para el estado **del que trata la pantalla**; `punto` para un estado de alta y baja
   * que sólo acompaña y no debe competir con él (FR-055).
   */
  forma: FormaDeEstado
  /**
   * El dato accesorio del estado —comprobante, fecha, motivo—. Va **debajo**, en menor jerarquía, y
   * nunca repite la palabra del estado (FR-057): la celda dice *Facturado* y debajo `0000-00000111`,
   * no *Facturado en 0000-00000111*.
   */
  detalle?: ReactNode
  className?: string
}

export function Estado({ valor, texto, forma, detalle, className }: Props) {
  const tono = TONO_POR_VALOR[valor] ?? 'neutro'

  const indicador =
    forma === 'pastilla' ? (
      <span
        className={cn(
          'inline-flex w-fit items-center gap-1.5 rounded-pastilla px-2.5 py-1',
          'text-[12.5px] font-semibold tracking-tight',
          PASTILLAS[tono],
        )}
      >
        <span aria-hidden="true" className="size-1.5 rounded-pastilla bg-current" />
        {texto}
      </span>
    ) : (
      <span className="inline-flex w-fit items-center gap-1.5 text-[12.5px] font-medium text-ink-soft">
        <span aria-hidden="true" className={cn('size-1.5 rounded-pastilla', PUNTOS[tono])} />
        {texto}
      </span>
    )

  if (detalle === undefined) {
    return <span className={cn('inline-flex', className)}>{indicador}</span>
  }

  return (
    <span className={cn('flex flex-col gap-1', className)}>
      {indicador}
      {/* El detalle no lleva mono por defecto: lo lleva cuando **es** un identificador, y eso lo
          decide quien lo pasa. Un "Cobrada el 01/09/2026" en mono no ayuda a nadie. */}
      <span className="text-[11px] text-faint">{detalle}</span>
    </span>
  )
}
