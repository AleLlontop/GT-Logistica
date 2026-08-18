import { DialogoConfirmacion } from '../../../compartido/ui/DialogoConfirmacion'

/** Qué se está por hacer, que es lo que decide el texto de la confirmación. */
export type QueSeConfirma =
  | { tipo: 'baja'; patente: string }
  | { tipo: 'reactivacion'; patente: string }

interface Props {
  que: QueSeConfirma
  onConfirmar: () => void
  onCancelar: () => void
}

/**
 * Confirmaciones de baja y reactivación de una unidad (FR-007, FR-008e, SC-009, US6 esc. 6).
 *
 * Reutiliza el diálogo del Módulo 2 —con su manejo de foco y `Escape`— y sólo aporta los textos, que
 * son los de `contracts/README.md`. **Cancelar no dispara ninguna llamada**: el componente sólo avisa
 * y quien lo usa decide.
 *
 * Ninguna de las dos advierte que no se puede deshacer, y es correcto: la baja de un vehículo es
 * lógica y se revierte reactivándolo. La única operación irreversible del módulo es eliminar un
 * documento, y su confirmación vive aparte (FR-028).
 */
export function ConfirmacionBajaVehiculo({ que, onConfirmar, onCancelar }: Props) {
  const { titulo, mensaje } = textoDe(que)

  return (
    <DialogoConfirmacion
      titulo={titulo}
      mensaje={mensaje}
      onConfirmar={onConfirmar}
      onCancelar={onCancelar}
    />
  )
}

function textoDe(que: QueSeConfirma) {
  switch (que.tipo) {
    case 'baja':
      return {
        titulo: 'Dar de baja la unidad',
        mensaje:
          `¿Dar de baja la unidad ${que.patente}? Va a dejar de figurar en el listado y en el panel ` +
          'de vencimientos. Su documentación se conserva y podés reactivarla más adelante.',
      }

    case 'reactivacion':
      return {
        titulo: 'Reactivar la unidad',
        mensaje:
          `¿Reactivar la unidad ${que.patente}? Vuelve al listado y al panel de vencimientos con ` +
          'toda su documentación.',
      }
  }
}
