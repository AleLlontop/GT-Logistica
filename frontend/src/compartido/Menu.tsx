import { NavLink } from 'react-router-dom'
import { agruparEnSecciones, type NombreDeSeccion } from './seccionesDeMenu'
import { cn } from './ui/cn'
import {
  IconoAdministracion,
  IconoConfiguracion,
  IconoOperacion,
  IconoPadrones,
  IconoSeguimiento,
} from './ui/iconos'
import type { OpcionMenu } from './tipos'

interface Props {
  opciones: OpcionMenu[]
}

const ICONO_DE_SECCION: Record<NombreDeSeccion, typeof IconoOperacion> = {
  'Operación': IconoOperacion,
  Padrones: IconoPadrones,
  Seguimiento: IconoSeguimiento,
  'Configuración': IconoConfiguracion,
  'Administración': IconoAdministracion,
}

/**
 * La navegación del sistema.
 *
 * Dibuja exactamente las opciones que llegan del servidor, sin lógica propia de permisos (FR-020 del
 * Módulo 1). Ocultar una opción nunca es la protección: el backend rechaza igual la operación. Lo
 * único que este componente decide es **dónde** va cada una (research §6).
 *
 * Antes eran catorce entradas planas en una barra horizontal, mezclando la operación diaria con
 * catálogos de configuración, con *Totales* y *Totales facturados* una al lado de la otra. Ahora van
 * agrupadas en cinco secciones, y la opción abierta se distingue por **fondo, peso y una barra a la
 * izquierda** —no sólo por color (FR-014)—, igual que la sección que la contiene.
 *
 * La lista puede venir vacía —un usuario cuyos roles todavía no habilitan nada— y en ese caso la
 * navegación no se muestra, sin que eso impida usar la pantalla de inicio.
 */
export function Menu({ opciones }: Props) {
  const secciones = agruparEnSecciones(opciones)

  if (secciones.length === 0) {
    return null
  }

  return (
    <nav
      aria-label="Menú principal"
      className="shrink-0 border-r border-borde bg-superficie md:w-60"
    >
      <div className="flex flex-col gap-6 px-3 py-5">
        {secciones.map((seccion) => {
          const Icono = ICONO_DE_SECCION[seccion.nombre]

          return (
            <div key={seccion.nombre}>
              <h2 className="flex items-center gap-2 px-2 pb-2 text-xs font-semibold tracking-wide text-texto-suave uppercase">
                <Icono aria-hidden="true" className="size-4" />
                {seccion.nombre}
              </h2>

              <ul className="m-0 flex list-none flex-col gap-0.5 p-0">
                {seccion.opciones.map((opcion) => (
                  <li key={opcion.codigo}>
                    <NavLink
                      to={opcion.ruta}
                      end
                      className={({ isActive }) =>
                        cn(
                          'block rounded-chico border-l-4 px-3 py-1.5 text-sm no-underline',
                          isActive
                            ? 'border-acento bg-acento-fondo font-semibold text-acento'
                            : 'border-transparent text-texto hover:bg-superficie-hundida',
                        )
                      }
                    >
                      {opcion.etiqueta}
                    </NavLink>
                  </li>
                ))}
              </ul>
            </div>
          )
        })}
      </div>
    </nav>
  )
}
