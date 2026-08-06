import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ErrorHttp } from '../../../../compartido/clienteHttp'
import type { Persona } from '../../../../compartido/tipos'
import { DialogoConfirmacion } from '../../componentes/DialogoConfirmacion'
import { formatearFecha, NOMBRE_DE_TIPO_INTEGRANTE } from '../../servicios/formato'
import { darDeBajaPersona, listarPersonas } from '../servicios/personas'

// Dos mensajes distintos a propósito (FR-025): "todavía no cargaste ninguna" y "tu búsqueda no
// encontró nada" son situaciones distintas y llevan a acciones distintas.
const MENSAJE_PADRON_VACIO =
  'Todavía no hay personas cargadas. Registrá la primera para poder asociarla a un usuario.'
const MENSAJE_SIN_COINCIDENCIAS = 'No hay personas que coincidan con la búsqueda.'

/**
 * Padrón de personas (User Story 6).
 *
 * El padrón arranca vacío en toda instalación nueva (FR-024), así que la lista vacía es el estado
 * inicial esperado y no un error.
 */
export function ListadoPersonas() {
  const [texto, setTexto] = useState('')
  const [personas, setPersonas] = useState<Persona[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [aBajar, setABajar] = useState<Persona | null>(null)

  const traer = useCallback(() => {
    listarPersonas({ texto })
      .then((lista) => {
        setPersonas(lista)
        setError(null)
      })
      .catch(() => setError('No pudimos traer el padrón. Volvé a intentar en unos minutos.'))
  }, [texto])

  useEffect(() => {
    traer()
  }, [traer])

  async function confirmarBaja() {
    if (aBajar === null) {
      return
    }

    try {
      await darDeBajaPersona(aBajar.id)
      setError(null)
      traer()
    } catch (fallo) {
      // Acá cae el rechazo de FR-028: la persona está vinculada a un usuario. El mensaje del
      // servidor identifica a cuál.
      setError(
        fallo instanceof ErrorHttp
          ? fallo.detalle.mensaje
          : 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
      )
    } finally {
      setABajar(null)
    }
  }

  const buscando = texto.trim() !== ''

  return (
    <main>
      <h1>Personas</h1>

      <Link to="/personas/nueva">Nueva persona</Link>

      <div className="campo">
        <label htmlFor="busqueda">Buscar por nombre, apellido o DNI</label>
        <input
          id="busqueda"
          type="search"
          value={texto}
          onChange={(evento) => setTexto(evento.target.value)}
        />
      </div>

      {error !== null && <p role="alert">{error}</p>}

      {personas === null && error === null && <p role="status">Cargando personas…</p>}

      {personas !== null && personas.length === 0 && (
        <p role="status">{buscando ? MENSAJE_SIN_COINCIDENCIAS : MENSAJE_PADRON_VACIO}</p>
      )}

      {personas !== null && personas.length > 0 && (
        <table>
          <caption>Padrón de personas</caption>
          <thead>
            <tr>
              <th scope="col">Nombre</th>
              <th scope="col">Apellido</th>
              <th scope="col">DNI</th>
              <th scope="col">Tipo</th>
              <th scope="col">Teléfono</th>
              <th scope="col">Email</th>
              <th scope="col">Fecha de nacimiento</th>
              <th scope="col">Estado</th>
              <th scope="col">Acciones</th>
            </tr>
          </thead>
          <tbody>
            {personas.map((persona) => (
              <tr key={persona.id}>
                <td>{persona.nombre}</td>
                <td>{persona.apellido}</td>
                <td>{persona.dni}</td>
                <td>{NOMBRE_DE_TIPO_INTEGRANTE[persona.tipo]}</td>
                <td>{persona.telefono}</td>
                <td>{persona.email}</td>
                <td>{formatearFecha(persona.fechaNacimiento)}</td>
                <td>{persona.activa ? 'Activa' : 'Dada de baja'}</td>
                <td>
                  <Link to={`/personas/${persona.id}/editar`}>Editar</Link>
                  {persona.activa && (
                    <button type="button" onClick={() => setABajar(persona)}>
                      Dar de baja
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {aBajar !== null && (
        <DialogoConfirmacion
          titulo="Dar de baja"
          mensaje={`¿Confirmás la baja de ${aBajar.nombre} ${aBajar.apellido}? Va a dejar de estar disponible para asociar a un usuario.`}
          onConfirmar={confirmarBaja}
          onCancelar={() => setABajar(null)}
        />
      )}
    </main>
  )
}
