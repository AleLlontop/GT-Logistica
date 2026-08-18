import { DialogoConfirmacion } from '../../../compartido/ui/DialogoConfirmacion'

interface Props {
  razonSocial: string
  onConfirmar: () => void
  onCancelar: () => void
}

/**
 * Confirmación de la baja de un cliente (FR-005, US1 esc. 7).
 *
 * Reutiliza el diálogo del Módulo 2 —con su manejo de foco y `Escape`— y sólo aporta el texto, que es
 * el de `contracts/README.md`. **Cancelar no dispara ninguna llamada**: el componente sólo avisa y
 * quien lo usa decide.
 *
 * **El alta de nuevo no tiene su propia confirmación**, y es deliberado: no destruye nada y se
 * deshace con la baja, que sí la pide (FR-007, precedente [004]).
 */
export function ConfirmacionBajaCliente({ razonSocial, onConfirmar, onCancelar }: Props) {
  return (
    <DialogoConfirmacion
      titulo={`¿Dar de baja a ${razonSocial}?`}
      mensaje={
        'Deja de ofrecerse al registrar viajes. Sus viajes históricos se conservan y podés darlo ' +
        'de alta de nuevo cuando quieras.'
      }
      etiquetaConfirmar="Dar de baja"
      onConfirmar={onConfirmar}
      onCancelar={onCancelar}
    />
  )
}
