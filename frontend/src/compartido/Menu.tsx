import { NavLink } from 'react-router-dom'
import type { OpcionMenu } from './tipos'

interface Props {
  opciones: OpcionMenu[]
}

/**
 * Dibuja exactamente las opciones que llegan del servidor, sin lógica propia de permisos
 * (FR-020, research §8).
 *
 * Ocultar una opción nunca es la protección: el backend rechaza igual la operación (FR-008). Este
 * componente sólo evita ofrecer lo que no corresponde.
 *
 * La lista puede venir vacía —un usuario cuyos roles todavía no habilitan nada implementado— y en
 * ese caso el menú simplemente no muestra opciones, sin que eso impida usar la pantalla de inicio.
 */
export function Menu({ opciones }: Props) {
  if (opciones.length === 0) {
    return null
  }

  return (
    <nav aria-label="Menú principal">
      <ul>
        {opciones.map((opcion) => (
          <li key={opcion.codigo}>
            <NavLink to={opcion.ruta}>{opcion.etiqueta}</NavLink>
          </li>
        ))}
      </ul>
    </nav>
  )
}
