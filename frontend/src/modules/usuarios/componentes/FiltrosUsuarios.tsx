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
    <section aria-label="Filtros" className="filtros">
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
