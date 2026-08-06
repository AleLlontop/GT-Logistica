import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import type { Persona } from '../../../compartido/tipos'
import { listarPersonas, nombreCompleto } from '../personas/servicios/personas'

interface Props {
  /** `null` significa "sin persona asociada", que es un caso válido y habitual (FR-008). */
  valor: number | null
  onCambio: (personaId: number | null) => void
  deshabilitado?: boolean
}

/**
 * Selector de la persona a asociar a un usuario.
 *
 * Sólo ofrece personas registradas y activas (FR-023), y no permite crear una desde acá: para eso
 * está la pantalla del padrón.
 *
 * El padrón arranca vacío en toda instalación nueva (FR-024), así que la lista vacía no es un error
 * sino el estado inicial esperado: en ese caso se muestra una leyenda que lleva a cargar la primera
 * persona, en vez de un desplegable vacío sin explicación.
 */
export function SelectorPersona({ valor, onCambio, deshabilitado = false }: Props) {
  const [personas, setPersonas] = useState<Persona[] | null>(null)

  useEffect(() => {
    let vigente = true

    listarPersonas({ soloActivas: true })
      .then((lista) => {
        if (vigente) {
          setPersonas(lista)
        }
      })
      .catch(() => {
        if (vigente) {
          setPersonas([])
        }
      })

    return () => {
      vigente = false
    }
  }, [])

  if (personas === null) {
    return (
      <div className="campo">
        <span id="persona-etiqueta">Persona asociada</span>
        <p role="status">Cargando personas…</p>
      </div>
    )
  }

  if (personas.length === 0) {
    return (
      <div className="campo">
        <span id="persona-etiqueta">Persona asociada</span>
        <p className="campo__vacio">
          No hay personas registradas. Cargá una desde la pantalla{' '}
          <Link to="/personas">Personas</Link>.
        </p>
      </div>
    )
  }

  return (
    <div className="campo">
      <label htmlFor="personaId">Persona asociada</label>
      <select
        id="personaId"
        name="personaId"
        value={valor ?? ''}
        disabled={deshabilitado}
        onChange={(evento) =>
          onCambio(evento.target.value === '' ? null : Number(evento.target.value))
        }
      >
        <option value="">Sin persona asociada</option>
        {personas.map((persona) => (
          <option key={persona.id} value={persona.id}>
            {nombreCompleto(persona)} — DNI {persona.dni}
          </option>
        ))}
      </select>
    </div>
  )
}
