import { Boton } from './Boton'
import { Dialogo, DialogoAcciones } from './Dialogo'

interface Props {
  titulo: string
  mensaje: string
  /**
   * Texto del botón que confirma. Por defecto `Confirmar`.
   *
   * Lo trajo el Módulo 5, donde `contracts/README.md` fija el verbo de cada confirmación —`Dar de
   * baja`, `Rendir sin importe`—: un botón que dice qué va a pasar se lee sin releer el diálogo.
   */
  etiquetaConfirmar?: string
  onConfirmar: () => void
  onCancelar: () => void
}

/**
 * Confirmación explícita previa a una operación que no conviene hacer por accidente: las bajas
 * (FR-017 del Módulo 2) y el restablecimiento de contraseña. Cancelar no modifica nada.
 *
 * **Misma firma que la versión del Módulo 2**, de donde se mudó: los cinco envoltorios que ya
 * delegaban en ella —las bajas de chofer, transportista y documento, la de vehículo, la de cliente y
 * la rendición de viaje— sólo cambiaron de dónde la importan.
 *
 * Lo que cambió por dentro: se apoya en `Dialogo`, así que ahora el foco además queda retenido
 * (FR-036).
 */
export function DialogoConfirmacion({
  titulo,
  mensaje,
  etiquetaConfirmar = 'Confirmar',
  onConfirmar,
  onCancelar,
}: Props) {
  return (
    <Dialogo titulo={titulo} onCerrar={onCancelar}>
      <p className="mt-2 text-sm text-texto-suave">{mensaje}</p>

      <DialogoAcciones>
        <Boton variante="secundario" onClick={onCancelar}>
          Cancelar
        </Boton>
        <Boton variante="primario" onClick={onConfirmar}>
          {etiquetaConfirmar}
        </Boton>
      </DialogoAcciones>
    </Dialogo>
  )
}
