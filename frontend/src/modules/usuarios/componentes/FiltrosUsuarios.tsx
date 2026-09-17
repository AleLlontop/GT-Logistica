import type { ReactNode } from 'react'
import { Boton } from '../../../compartido/ui/Boton'
import { Filtros as ContenedorDeFiltros, FranjaDeBusqueda, FranjaDeFiltros } from '../../../compartido/ui/Filtros'
import { clasesDeEtiquetaDeFiltro, clasesDeFiltro } from '../../../compartido/ui/clases'
import type { CodigoRol, EstadoUsuario } from '../../../compartido/tipos'
import { FILTROS_VACIOS, type Filtros } from '../servicios/formato'
import { ESTADOS_DE_USUARIO, ROLES_DEL_SISTEMA } from '../servicios/usuarios'

interface Props {
  valor: Filtros
  onCambio: (filtros: Filtros) => void
  /** Cantidad y criterio de orden (FR-035). */
  resumen?: ReactNode
}

/**
 * Los cuatro filtros del listado (FR-011 del Módulo 2), combinables entre sí.
 *
 * *Nombre de usuario* y *email* son campos de texto y traen todo lo que **contenga** lo escrito, sin
 * distinguir mayúsculas. *Rol* y *estado* son listas desplegables de selección exacta.
 *
 * **Esta pantalla tiene dos buscadores y los dos suben a la franja de arriba** (FR-033): los dos
 * traen por coincidencia parcial y son con los que se entra a la pantalla. Los dos desplegables
 * quedan debajo, y **el que está filtrando se distingue** del que no (FR-034).
 */
export function FiltrosUsuarios({ valor, onCambio, resumen }: Props) {
  function actualizar<C extends keyof Filtros>(campo: C, nuevo: Filtros[C]) {
    onCambio({ ...valor, [campo]: nuevo })
  }

  return (
    <ContenedorDeFiltros resumen={resumen}>
      <FranjaDeBusqueda>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-username" className={clasesDeEtiquetaDeFiltro}>
            Nombre de usuario
          </label>
          <input
            id="filtro-username"
            type="search"
            value={valor.username}
            onChange={(evento) => actualizar('username', evento.target.value)}
            className={clasesDeFiltro(valor.username.trim() !== '')}
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-email" className={clasesDeEtiquetaDeFiltro}>
            Email
          </label>
          <input
            id="filtro-email"
            type="search"
            placeholder="nombre@empresa.com.ar"
            value={valor.email}
            onChange={(evento) => actualizar('email', evento.target.value)}
            className={clasesDeFiltro(valor.email.trim() !== '')}
          />
        </div>
      </FranjaDeBusqueda>

      <FranjaDeFiltros>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-rol" className={clasesDeEtiquetaDeFiltro}>
            Rol
          </label>
          <select
            id="filtro-rol"
            value={valor.rol}
            onChange={(evento) => actualizar('rol', evento.target.value as CodigoRol | '')}
            className={clasesDeFiltro(valor.rol !== '')}
          >
            <option value="">Todos</option>
            {ROLES_DEL_SISTEMA.map((rol) => (
              <option key={rol.codigo} value={rol.codigo}>
                {rol.nombre}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="filtro-estado" className={clasesDeEtiquetaDeFiltro}>
            Estado
          </label>
          <select
            id="filtro-estado"
            value={valor.estado}
            onChange={(evento) => actualizar('estado', evento.target.value as EstadoUsuario | '')}
            className={clasesDeFiltro(valor.estado !== '')}
          >
            <option value="">Todos</option>
            {ESTADOS_DE_USUARIO.map((estado) => (
              <option key={estado.codigo} value={estado.codigo}>
                {estado.nombre}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <label className={clasesDeEtiquetaDeFiltro + ' invisible'} aria-hidden="true">
            Limpiar
          </label>
          <Boton
            variante="secundario"
            tamanio="chico"
            onClick={() => onCambio(FILTROS_VACIOS)}
          >
            Limpiar filtros
          </Boton>
        </div>
      </FranjaDeFiltros>
    </ContenedorDeFiltros>
  )
}
