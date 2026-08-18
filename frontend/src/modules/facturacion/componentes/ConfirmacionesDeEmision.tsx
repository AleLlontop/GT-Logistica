import { Dialogo } from '../../../compartido/ui/Dialogo'
import type { MotivoConfirmacion } from '../servicios/api'

interface Props {
  motivo: MotivoConfirmacion
  /** El texto que armó el servidor, con el viaje y las fechas ya nombrados (FR-032). */
  mensaje: string
  trabajando: boolean
  onCancelar: () => void
  onConfirmar: () => void
}

/** El título de cada diálogo, palabra por palabra (contracts/README §Confirmaciones previas). */
const TITULOS: Record<MotivoConfirmacion, string> = {
  viajeEnCero: 'Un viaje incluido no tiene importe',
  fechaAnteriorAViaje: 'La fecha de la factura es anterior a la de un viaje',
}

/**
 * Los dos diálogos de FR-032, disparados por el `409` del servidor.
 *
 * **Las dos confirmaciones viven en el backend y no acá**, a diferencia de todas las bajas del sistema:
 * la emisión no se deshace, así que el primer intento responde `409` sin crear nada y el segundo lleva
 * `confirmado: true`. El criterio es la reversibilidad, no la gravedad del aviso (convención [005],
 * research §11).
 *
 * Eso hace que este componente sea **reactivo y no preventivo**: no adivina si hace falta confirmar
 * —eso lo decide el servidor, que es el que ve los importes reales de la base—, sino que muestra lo que
 * el rechazo trajo. El texto lo arma el backend porque nombra el viaje y las fechas puntuales.
 */
export function ConfirmacionesDeEmision({
  motivo,
  mensaje,
  trabajando,
  onCancelar,
  onConfirmar,
}: Props) {
  return (
    <Dialogo titulo={`${TITULOS[motivo]}`} onCerrar={onCancelar}>

      <p>{mensaje}</p>

      <div className="acciones">
        <button type="button" onClick={onCancelar} disabled={trabajando}>
          Cancelar
        </button>
        <button type="button" onClick={onConfirmar} disabled={trabajando}>
          Emitir igual
        </button>
      </div>
    </Dialogo>
  )
}
