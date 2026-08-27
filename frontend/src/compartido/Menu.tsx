import { Link, NavLink } from 'react-router-dom'
import { agruparEnSecciones, type NombreDeSeccion } from './seccionesDeMenu'
import { cn } from './ui/cn'
import {
  IconoAdministracion,
  IconoCerrarSesion,
  IconoConfiguracion,
  IconoContrasena,
  IconoOperacion,
  IconoPadrones,
  IconoSeguimiento,
} from './ui/iconos'
import type { OpcionMenu } from './tipos'

interface Props {
  opciones: OpcionMenu[]
  username: string
  onCerrarSesion: () => void
}

const ICONO_DE_SECCION: Record<NombreDeSeccion, typeof IconoOperacion> = {
  'Operación': IconoOperacion,
  Padrones: IconoPadrones,
  Seguimiento: IconoSeguimiento,
  'Configuración': IconoConfiguracion,
  'Administración': IconoAdministracion,
}

/**
 * A partir de cuántas entradas el relleno pleno del ítem activo pesa demasiado.
 *
 * Con pocas entradas, `bg-ink` marca la posición de un vistazo. Con muchas, ese bloque oscuro se
 * vuelve el elemento más pesado de la pantalla y compite con la acción principal, así que el activo
 * pasa a fondo suave con texto de acento (FR-012). En los dos casos se distingue por **fondo y peso**
 * además de color, que es lo que la regla pide.
 */
const ENTRADAS_PARA_MENU_LARGO = 15

/**
 * La navegación del sistema, como **isla flotante** (FR-009).
 *
 * 266 px de ancho, radio de isla, separada 18 px de los bordes por el relleno raíz del `Layout`.
 * Nunca pegada al borde de la ventana: es lo que la hace leer como una superficie apoyada sobre el
 * lienzo y no como una columna recortada.
 *
 * Dibuja **exactamente** las opciones que llegan del servidor, sin lógica propia de permisos (FR-013,
 * FR-020 del Módulo 1). Ocultar una opción nunca es la protección: el backend rechaza igual la
 * operación. Lo único que este componente decide es **dónde** va cada una, y un código que el mapa de
 * secciones no conozca **se sigue dibujando**, en la última sección: es lo que permite que un módulo
 * futuro aparezca en el menú sin tocar el frontend.
 *
 * La lista puede venir vacía —un usuario cuyos roles todavía no habilitan nada— y en ese caso **no se
 * dibuja navegación**. La isla sigue estando, porque abajo viven la marca y el bloque de cuenta, y
 * *Cerrar sesión* tiene que estar disponible desde cualquier lugar del sistema (FR-013 del Módulo 1).
 */
export function Menu({ opciones, username, onCerrarSesion }: Props) {
  const secciones = agruparEnSecciones(opciones)
  const menuLargo = opciones.length > ENTRADAS_PARA_MENU_LARGO

  return (
    <div className="flex w-[266px] shrink-0 flex-col rounded-island border border-line bg-surface shadow-island">
      <div className="border-b border-line px-5 py-[22px]">
        <Link
          to="/"
          className="block text-[14px] leading-tight font-bold tracking-tight text-ink no-underline"
        >
          Sistema Integral de Gestión
        </Link>
      </div>

      {secciones.length > 0 && (
        <nav aria-label="Menú principal" className="flex-1 overflow-y-auto px-3 py-4">
          <div className="flex flex-col gap-5">
            {secciones.map((seccion) => {
              const Icono = ICONO_DE_SECCION[seccion.nombre]

              return (
                <div key={seccion.nombre}>
                  {/* Rótulo de sección en estilo eyebrow: 9,5 px Bold, mayúsculas, tracking ancho. */}
                  <h2 className="flex items-center gap-2 px-3 pb-2 text-[9.5px] font-bold tracking-[0.2em] text-faint uppercase">
                    <Icono aria-hidden="true" className="size-3.5" />
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
                              'block rounded-pastilla px-3 py-2 text-[13px] tracking-tight no-underline',
                              'transition-colors duration-200 ease-gt',
                              isActive
                                ? menuLargo
                                  ? 'bg-brand-soft font-bold text-brand-deep'
                                  : 'bg-ink font-bold text-white shadow-pill'
                                : 'font-medium text-ink-soft hover:bg-surface-mute',
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
      )}

      {/*
        El pie de la isla: quién está adentro y las dos acciones de su cuenta. Es lo que reemplaza a
        la barra superior que el Módulo 7 tenía (FR-010).
      */}
      <div className="mt-auto rounded-b-island border-t border-line bg-surface-mute px-4 py-4">
        <p className="m-0 truncate text-[13px] font-bold tracking-tight text-ink-soft">{username}</p>

        <div className="mt-2.5 flex flex-col gap-1">
          <Link
            to="/mi-cuenta/contrasena"
            className="inline-flex items-center gap-2 rounded-pastilla px-2 py-1 text-[12.5px] font-medium text-ink-soft no-underline transition-colors duration-200 ease-gt hover:bg-white/70"
          >
            <IconoContrasena aria-hidden="true" className="size-3.5" />
            Cambiar contraseña
          </Link>

          <button
            type="button"
            onClick={onCerrarSesion}
            className="inline-flex items-center gap-2 rounded-pastilla px-2 py-1 text-left text-[12.5px] font-medium text-ink-soft transition-colors duration-200 ease-gt hover:bg-white/70"
          >
            <IconoCerrarSesion aria-hidden="true" className="size-3.5" />
            Cerrar sesión
          </button>
        </div>
      </div>
    </div>
  )
}
