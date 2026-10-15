import { useState } from 'react'
import { Link, NavLink } from 'react-router-dom'
import { agruparEnSecciones, type NombreDeSeccion } from './seccionesDeMenu'
import { cn } from './ui/cn'
import {
  IconoAdministracion,
  IconoCerrar,
  IconoCerrarSesion,
  IconoChevronAbajo,
  IconoConfiguracion,
  IconoContrasena,
  IconoOperacion,
  IconoPadrones,
  IconoSeguimiento,
} from './ui/iconos'
import { InterruptorModoOscuro } from './ui/InterruptorModoOscuro'
import type { OpcionMenu } from './tipos'

interface Props {
  opciones: OpcionMenu[]
  username: string
  onCerrarSesion: () => void
  /** Cierre opcional para navegación en dispositivos móviles al seleccionar una opción. */
  onNavegar?: () => void
  /** Botón de cierre para vista móvil. */
  onCerrar?: () => void
}

const ICONO_DE_SECCION: Record<NombreDeSeccion, typeof IconoOperacion> = {
  'Operación': IconoOperacion,
  Padrones: IconoPadrones,
  Seguimiento: IconoSeguimiento,
  'Configuración': IconoConfiguracion,
  'Administración': IconoAdministracion,
}

const ENTRADAS_PARA_MENU_LARGO = 15

/**
 * La navegación del sistema, como **isla flotante** (FR-009).
 *
 * 266 px de ancho, radio de isla, separada 18 px de los bordes por el relleno raíz del `Layout`.
 * Incluye cabeceras colapsables por sección para una navegación más cómoda.
 */
export function Menu({ opciones, username, onCerrarSesion, onNavegar, onCerrar }: Props) {
  const secciones = agruparEnSecciones(opciones)
  const menuLargo = opciones.length > ENTRADAS_PARA_MENU_LARGO

  // Todas las secciones abiertas por defecto
  const [seccionesAbiertas, setSeccionesAbiertas] = useState<Record<string, boolean>>({})

  function toggleSeccion(nombre: string) {
    setSeccionesAbiertas((prev) => ({
      ...prev,
      [nombre]: prev[nombre] !== undefined ? !prev[nombre] : false,
    }))
  }

  function estaAbierta(nombre: string) {
    return seccionesAbiertas[nombre] ?? true
  }

  return (
    <div className="flex w-[266px] shrink-0 flex-col rounded-island border border-line bg-surface shadow-island">
      <div className="flex items-center justify-between border-b border-line px-5 py-[18px]">
        <Link
          to="/"
          onClick={onNavegar}
          className="block text-[14px] leading-tight font-bold tracking-tight text-ink no-underline"
        >
          Sistema Integral de Gestión
        </Link>
        {onCerrar !== undefined && (
          <button
            type="button"
            aria-label="Cerrar menú"
            onClick={onCerrar}
            className="flex size-7 items-center justify-center rounded-pastilla text-ink-soft hover:bg-surface-mute cursor-pointer"
          >
            <IconoCerrar className="size-4" aria-hidden="true" />
          </button>
        )}
      </div>

      {secciones.length > 0 && (
        <nav aria-label="Menú principal" className="flex-1 overflow-y-auto px-3 py-4">
          <div className="flex flex-col gap-4">
            {secciones.map((seccion) => {
              const Icono = ICONO_DE_SECCION[seccion.nombre]
              const abierta = estaAbierta(seccion.nombre)

              return (
                <div key={seccion.nombre}>
                  <h2 className="m-0 p-0">
                    <button
                      type="button"
                      onClick={() => toggleSeccion(seccion.nombre)}
                      aria-expanded={abierta}
                      className="flex w-full items-center justify-between px-3 pb-2 text-[9.5px] font-bold tracking-[0.2em] text-faint uppercase hover:text-ink transition-colors cursor-pointer select-none group"
                    >
                      <span className="flex items-center gap-2">
                        <Icono aria-hidden="true" className="size-3.5 text-faint group-hover:text-ink transition-colors" />
                        <span>{seccion.nombre}</span>
                      </span>
                      <IconoChevronAbajo
                        aria-hidden="true"
                        className={cn(
                          'size-3 text-faint transition-transform duration-200 ease-gt group-hover:text-ink',
                          !abierta && '-rotate-90',
                        )}
                      />
                    </button>
                  </h2>

                  {abierta && (
                    <ul className="m-0 flex list-none flex-col gap-0.5 p-0">
                      {seccion.opciones.map((opcion) => (
                        <li key={opcion.codigo}>
                          <NavLink
                            to={opcion.ruta}
                            end
                            onClick={onNavegar}
                            className={({ isActive }) =>
                              cn(
                                'block rounded-pastilla px-3 py-2 text-[13px] tracking-tight no-underline',
                                'transition-colors duration-200 ease-gt',
                                isActive
                                  ? menuLargo
                                    ? 'bg-brand-soft font-bold text-brand-deep'
                                    : 'bg-ink font-bold text-white shadow-pill dark:bg-white dark:text-zinc-950'
                                  : 'font-medium text-ink-soft hover:bg-surface-mute',
                              )
                            }
                          >
                            {opcion.etiqueta}
                          </NavLink>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              )
            })}
          </div>
        </nav>
      )}

      {/* Pie de la isla: usuario y acciones */}
      <div className="mt-auto rounded-b-island border-t border-line bg-surface-mute px-4 py-4">
        <div className="flex items-center justify-between gap-2">
          <p className="m-0 truncate text-[13px] font-bold tracking-tight text-ink-soft">{username}</p>
          <InterruptorModoOscuro />
        </div>

        <div className="mt-2.5 flex flex-col gap-1">
          <Link
            to="/mi-cuenta/contrasena"
            onClick={onNavegar}
            className="inline-flex items-center gap-2 rounded-pastilla px-2 py-1 text-[12.5px] font-medium text-ink-soft no-underline transition-colors duration-200 ease-gt hover:bg-surface"
          >
            <IconoContrasena aria-hidden="true" className="size-3.5" />
            Cambiar contraseña
          </Link>

          <button
            type="button"
            onClick={onCerrarSesion}
            className="inline-flex items-center gap-2 rounded-pastilla px-2 py-1 text-left text-[12.5px] font-medium text-ink-soft transition-colors duration-200 ease-gt hover:bg-surface cursor-pointer"
          >
            <IconoCerrarSesion aria-hidden="true" className="size-3.5" />
            Cerrar sesión
          </button>
        </div>
      </div>
    </div>
  )
}
