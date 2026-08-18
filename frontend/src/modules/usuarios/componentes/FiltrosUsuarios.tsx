import type { CodigoRol, EstadoUsuario } from '../../../compartido/tipos'
import { FILTROS_VACIOS, type Filtros } from '../servicios/formato'
import { ESTADOS_DE_USUARIO, ROLES_DEL_SISTEMA } from '../servicios/usuarios'

interface Props {
  valor: Filtros
  onCambio: (filtros: Filtros) => void
}

/**
 * Los cuatro filtros del listado (FR-011), combinables entre sí.
 *
 * *Username* y *email* son campos de texto y traen todo lo que **contenga** lo escrito, sin
 * distinguir mayúsculas. *Rol* y *estado* son listas desplegables de selección exacta.
 */
export function FiltrosUsuarios({ valor, onCambio }: Props) {
  function actualizar<C extends keyof Filtros>(campo: C, nuevo: Filtros[C]) {
    onCambio({ ...valor, [campo]: nuevo })
  }

  return (
    <section aria-label="Filtros" className="flex flex-wrap items-end gap-4 border-b border-borde bg-superficie-hundida px-4 py-3 [&_.campo]:flex [&_.campo]:flex-col [&_.campo]:gap-1 [&_label]:text-xs [&_label]:font-medium [&_label]:text-texto-suave [&_select]:rounded-chico [&_select]:border [&_select]:border-borde-fuerte [&_select]:bg-superficie [&_select]:px-2 [&_select]:py-1.5 [&_select]:text-sm [&_select]:text-texto [&_input]:rounded-chico [&_input]:border [&_input]:border-borde-fuerte [&_input]:bg-superficie [&_input]:px-2 [&_input]:py-1.5 [&_input]:text-sm [&_input]:text-texto [&_button]:rounded-chico [&_button]:border [&_button]:border-borde-fuerte [&_button]:bg-superficie [&_button]:px-3 [&_button]:py-1.5 [&_button]:text-sm">
      <div className="campo">
        <label htmlFor="filtro-username">Nombre de usuario</label>
        <input
          id="filtro-username"
          type="search"
          value={valor.username}
          onChange={(evento) => actualizar('username', evento.target.value)}
        />
      </div>

      <div className="campo">
        <label htmlFor="filtro-email">Email</label>
        <input
          id="filtro-email"
          type="search"
          value={valor.email}
          onChange={(evento) => actualizar('email', evento.target.value)}
        />
      </div>

      <div className="campo">
        <label htmlFor="filtro-rol">Rol</label>
        <select
          id="filtro-rol"
          value={valor.rol}
          onChange={(evento) => actualizar('rol', evento.target.value as CodigoRol | '')}
        >
          <option value="">Todos</option>
          {ROLES_DEL_SISTEMA.map((rol) => (
            <option key={rol.codigo} value={rol.codigo}>
              {rol.nombre}
            </option>
          ))}
        </select>
      </div>

      <div className="campo">
        <label htmlFor="filtro-estado">Estado</label>
        <select
          id="filtro-estado"
          value={valor.estado}
          onChange={(evento) => actualizar('estado', evento.target.value as EstadoUsuario | '')}
        >
          <option value="">Todos</option>
          {ESTADOS_DE_USUARIO.map((estado) => (
            <option key={estado.codigo} value={estado.codigo}>
              {estado.nombre}
            </option>
          ))}
        </select>
      </div>

      <button type="button" onClick={() => onCambio(FILTROS_VACIOS)}>
        Limpiar filtros
      </button>
    </section>
  )
}
