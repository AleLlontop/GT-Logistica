import { cn } from './cn'
import {
  IconoAnulado,
  IconoEnRegla,
  IconoPendiente,
  IconoProximoAvencer,
  IconoVencido,
} from './iconos'

/**
 * El indicador de estado, único para los cinco juegos de estados del sistema (data-model §3).
 *
 * **`texto` es obligatorio.** No es una comodidad: es la forma de que FR-040 —ninguna información
 * se comunica sólo por color— no dependa de que alguien se acuerde. La primitiva no se puede
 * dibujar sin la palabra, y esa palabra es siempre la que ya estaba en pantalla: sale de
 * `NombresDeEstado` y de los `TEXTO_ESTADO_*` de cada módulo, que FR-004 congela.
 *
 * A la palabra se le suman un color **y una forma**, para que la distinción sobreviva a una
 * captura en escala de grises y a cualquier daltonismo (SC-012).
 */

type Tono = 'neutro' | 'exito' | 'advertencia' | 'error' | 'acento' | 'atenuado'

const TONOS: Record<Tono, string> = {
  neutro: 'bg-superficie-hundida text-texto-suave border-borde-fuerte',
  exito: 'bg-exito-fondo text-exito border-exito',
  advertencia: 'bg-advertencia-fondo text-advertencia border-advertencia',
  error: 'bg-error-fondo text-error border-error',
  acento: 'bg-acento-fondo text-acento border-acento',
  atenuado: 'bg-superficie-hundida text-texto-tenue border-borde-fuerte',
}

const ICONOS: Partial<Record<Tono, typeof IconoEnRegla>> = {
  exito: IconoEnRegla,
  advertencia: IconoProximoAvencer,
  error: IconoVencido,
  neutro: IconoPendiente,
  atenuado: IconoAnulado,
}

/**
 * De qué tono es cada valor. Las claves son los valores que el API ya devuelve en camelCase
 * (convención [003]); lo que no figura cae en `neutro`, que es un estado válido y no un error.
 */
const TONO_POR_VALOR: Record<string, Tono> = {
  // Documentación (Módulos 3 y 4)
  enRegla: 'exito',
  vigente: 'exito',
  proximaAvencer: 'advertencia',
  vencida: 'error',
  sinDocumentacion: 'neutro',

  // Viaje (Módulo 5)
  pendiente: 'neutro',
  enCurso: 'acento',
  rendido: 'exito',
  anulado: 'atenuado',
  facturado: 'exito',

  // Factura (Módulo 6)
  pagada: 'exito',
  anulada: 'atenuado',

  // Vehículo (Módulo 4)
  disponible: 'exito',
  enViaje: 'acento',
  fueraDeServicio: 'advertencia',

  // Alta y baja
  activo: 'neutro',
  inactivo: 'atenuado',
  bloqueado: 'error',
  dadoDeBaja: 'atenuado',
}

interface Props {
  /** El valor tal como lo devuelve el API, en camelCase. */
  valor: string
  /** La palabra que ya está en pantalla. Obligatoria. */
  texto: string
  className?: string
}

export function Estado({ valor, texto, className }: Props) {
  const tono = TONO_POR_VALOR[valor] ?? 'neutro'
  const Icono = ICONOS[tono]

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-chico border px-2 py-0.5 text-xs font-medium',
        TONOS[tono],
        className,
      )}
    >
      {Icono !== undefined && <Icono aria-hidden="true" className="size-3.5 shrink-0" />}
      {texto}
    </span>
  )
}
