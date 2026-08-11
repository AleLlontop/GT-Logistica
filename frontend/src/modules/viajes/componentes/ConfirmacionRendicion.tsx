import { DialogoConfirmacion } from '../../usuarios/componentes/DialogoConfirmacion'

interface Props {
  numero: number
  onConfirmar: () => void
  onCancelar: () => void
}

/**
 * Confirmación de la rendición sin importe (FR-038, US4 esc. 6 y 7).
 *
 * **Este diálogo no lo dispara la pantalla: lo dispara el `409` del backend.** Es la diferencia con
 * todas las confirmaciones anteriores del sistema —las bajas del Módulo 3 y del 4—, que se pedían acá
 * y se ejecutaban directo, porque todas se deshacen. Rendir con importe en cero no se deshace: el
 * viaje queda inmutable para siempre (FR-018, SC-007a).
 *
 * **Cancelar no dispara ninguna segunda petición.** El viaje sigue en curso con su importe en cero, y
 * se puede completar antes de volver a rendirlo.
 */
export function ConfirmacionRendicion({ numero, onConfirmar, onCancelar }: Props) {
  return (
    <DialogoConfirmacion
      titulo={`¿Rendir el viaje ${numero} sin importe?`}
      mensaje={
        'El viaje va a quedar cerrado con importe $ 0,00. Después no se va a poder corregir: un ' +
        'viaje rendido no se edita, no se reasigna y no se anula.'
      }
      etiquetaConfirmar="Rendir sin importe"
      onConfirmar={onConfirmar}
      onCancelar={onCancelar}
    />
  )
}
