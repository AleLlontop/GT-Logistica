import { DialogoConfirmacion } from '../../usuarios/componentes/DialogoConfirmacion'

/** Qué se está por dar de baja, que es lo que decide el texto de la confirmación. */
export type QueSeDaDeBaja =
  | { tipo: 'chofer'; apellido: string; nombre: string }
  | { tipo: 'reactivarChofer'; apellido: string; nombre: string }
  | { tipo: 'transportista'; nombre: string }
  | { tipo: 'documento'; tipoDocumento: string; numero: string }

interface Props {
  que: QueSeDaDeBaja
  onConfirmar: () => void
  onCancelar: () => void
}

/**
 * Confirmaciones de baja del módulo (FR-026, SC-008).
 *
 * Reutiliza el diálogo del Módulo 2 —con su manejo de foco y `Escape`— y sólo aporta los textos,
 * que son los de `contracts/README.md`. Cancelar no dispara ninguna llamada.
 *
 * La de eliminar un documento es la única que habla de **borrar** y no de dar de baja: es la única
 * operación del módulo que no se puede revertir (FR-015d), y el texto tiene que decirlo.
 */
export function ConfirmacionBaja({ que, onConfirmar, onCancelar }: Props) {
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

function textoDe(que: QueSeDaDeBaja) {
  switch (que.tipo) {
    case 'chofer':
      return {
        titulo: 'Dar de baja el chofer',
        mensaje:
          `¿Confirmás la baja de ${que.apellido}, ${que.nombre}? Va a quedar inactivo y no va a ` +
          'poder asignarse a un viaje. Su documentación se conserva.',
      }

    case 'reactivarChofer':
      return {
        titulo: 'Reactivar el chofer',
        mensaje:
          `¿Confirmás la reactivación de ${que.apellido}, ${que.nombre}? Va a volver al listado y ` +
          'su documentación va a contar de nuevo.',
      }

    case 'transportista':
      return {
        titulo: 'Dar de baja el transportista',
        mensaje:
          `¿Confirmás la baja de ${que.nombre}? Va a dejar de ofrecerse al registrar o reasignar ` +
          'choferes.',
      }

    case 'documento':
      return {
        titulo: 'Eliminar el documento',
        mensaje:
          `¿Confirmás que querés eliminar el ${que.tipoDocumento} N° ${que.numero}? Se borra junto ` +
          'con su archivo adjunto y no se puede deshacer.',
      }
  }
}
