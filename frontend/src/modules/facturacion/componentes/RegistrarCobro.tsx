import { BarraDeAcciones, LEYENDA_DE_OBLIGATORIOS } from '../../../compartido/ui/BarraDeAcciones'
import { Boton } from '../../../compartido/ui/Boton'
import { IconoEnRegla } from '../../../compartido/ui/iconos'
import { Dialogo } from '../../../compartido/ui/Dialogo'
import { clasesDeFormulario } from '../../../compartido/ui/clases'
import { useState, type FormEvent } from 'react'

export const ADVERTENCIA_COBRO =
  'La factura queda en estado Pagada. Es un paso que no se revierte: el sistema no ofrece ninguna ' +
  'acción para volver atrás un cobro.'

interface Props {
  numero: string
  /** La fecha propuesta, en `yyyy-MM-dd`: hoy (contracts/README §Registrar el cobro). */
  fechaPropuesta: string
  trabajando: boolean
  onRegistrar: (fechaCobro: string) => void
  onCancelar: () => void
}

/**
 * Formulario chico dentro de la ficha, con un solo campo: la fecha de cobro (FR-042).
 *
 * **La advertencia dice que el paso no se revierte, y es literal**: `pagada` es terminal y no existe
 * ninguna acción que devuelva la factura a `pendiente` (FR-043). No está oculta: no existe. Decirlo antes
 * es lo único honesto cuando la operación no se puede deshacer.
 *
 * **La confirmación la pide la pantalla**, a diferencia de las dos de la emisión: el cobro no necesita que
 * el servidor calcule nada para saber si hay que preguntar, y el dato que falta —la fecha— es parte del
 * mismo formulario (research §11).
 */
export function RegistrarCobro({
  numero,
  fechaPropuesta,
  trabajando,
  onRegistrar,
  onCancelar,
}: Props) {
  const [fechaCobro, setFechaCobro] = useState(fechaPropuesta)

  function enviar(evento: FormEvent) {
    evento.preventDefault()
    onRegistrar(fechaCobro)
  }

  return (
    <Dialogo titulo={`Registrar el cobro de la factura ${numero}`} onCerrar={onCancelar}>

      <p>{ADVERTENCIA_COBRO}</p>

      <form onSubmit={enviar} noValidate className={clasesDeFormulario}>
        <div className="campo max-w-campo-corto">
          <label htmlFor="fechaCobro">Fecha de cobro</label>
          <input
            id="fechaCobro"
            type="date"
            required
            value={fechaCobro}
            onChange={(evento) => setFechaCobro(evento.target.value)}
          />
        </div>

        {/* Diálogo: la barra va al pie de su contenedor, que ya está anclado (FR-030). */}
        <BarraDeAcciones anclaje="contenedor" leyenda={LEYENDA_DE_OBLIGATORIOS}>
          <Boton variante="secundario" onClick={onCancelar} disabled={trabajando}>
            Cancelar
          </Boton>
          <Boton
            type="submit"
            variante="primario"
            disabled={trabajando || fechaCobro === ''}
            icono={<IconoEnRegla className="size-3" />}
          >
            Registrar cobro
          </Boton>
        </BarraDeAcciones>
      </form>
    </Dialogo>
  )
}
