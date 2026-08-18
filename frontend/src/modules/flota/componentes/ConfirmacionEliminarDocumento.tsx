import { DialogoConfirmacion } from '../../../compartido/ui/DialogoConfirmacion'

interface Props {
  onConfirmar: () => void
  onCancelar: () => void
}

/**
 * Confirmación de eliminación de un documento (FR-027, SC-009, US3 esc. 10 y 11).
 *
 * **Es la única confirmación del módulo que advierte que no se puede deshacer**, porque es la única
 * operación que borra de verdad: el documento y su archivo adjunto desaparecen. La baja de un
 * vehículo o de un tipo es lógica y se revierte (FR-028).
 *
 * **Cancelar no dispara ninguna llamada**: el componente sólo avisa y quien lo usa decide (US3
 * esc. 11).
 */
export function ConfirmacionEliminarDocumento({ onConfirmar, onCancelar }: Props) {
  return (
    <DialogoConfirmacion
      titulo="Eliminar el documento"
      mensaje={
        '¿Eliminar este documento? Se borra junto con su archivo adjunto y esta acción no se puede ' +
        'deshacer.'
      }
      onConfirmar={onConfirmar}
      onCancelar={onCancelar}
    />
  )
}
