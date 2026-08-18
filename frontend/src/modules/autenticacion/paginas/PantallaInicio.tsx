import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { Link } from 'react-router-dom'
import { agruparEnSecciones } from '../../../compartido/seccionesDeMenu'
import type { Sesion } from '../servicios/sesion'

interface Props {
  sesion: Sesion
}

/**
 * Primera pantalla después de un ingreso exitoso (FR-020 del Módulo 1).
 *
 * Muestra el usuario y sus roles; el menú y el botón de cerrar sesión viven en el layout, porque son
 * comunes a todas las pantallas con sesión abierta.
 *
 * **Lo que el Módulo 7 le agregó**: los accesos a lo que esa persona puede usar (FR-015). Antes era
 * un saludo y nada más, y después de ingresar había que ir a buscar a la barra por dónde empezar.
 *
 * Los accesos se arman con **lo que la sesión ya trae** —las mismas opciones que dibuja el menú, con
 * los mismos rótulos que manda el servidor—: no se le pide al servidor ningún dato nuevo y no se
 * inventa ningún nombre.
 */
export function PantallaInicio({ sesion }: Props) {
  const secciones = agruparEnSecciones(sesion.opcionesMenu)
  const sinOpciones = sesion.opcionesMenu.length === 0

  return (
    <section>
      <EncabezadoDePantalla
        titulo={`Hola, ${sesion.username}`}
        resumen={
          <>
            {sesion.roles.length === 1 ? 'Tu rol: ' : 'Tus roles: '}
            {sesion.roles.map((rol) => rol.nombre).join(', ')}
          </>
        }
      />

      {/* El menú vacío es un caso válido: un usuario cuyos roles todavía no habilitan ninguna
          funcionalidad implementada igual inicia sesión y llega acá (FR-020). */}
      {sinOpciones && (
        <p className="max-w-prose text-sm text-texto-suave">
          Por ahora no tenés funcionalidades disponibles. Se irán agregando a medida que se
          implementen.
        </p>
      )}

      <div className="flex flex-col gap-8">
        {secciones.map((seccion) => (
          <div key={seccion.nombre}>
            <h2 className="mb-3 text-xs font-semibold tracking-wide text-texto-suave uppercase">
              {seccion.nombre}
            </h2>

            <ul className="m-0 grid list-none grid-cols-[repeat(auto-fill,minmax(14rem,1fr))] gap-3 p-0">
              {seccion.opciones.map((opcion) => (
                <li key={opcion.codigo}>
                  <Link
                    to={opcion.ruta}
                    className="block rounded-medio border border-borde bg-superficie px-4 py-3 text-sm font-medium text-texto no-underline shadow-tarjeta hover:border-acento hover:text-acento"
                  >
                    {opcion.etiqueta}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </section>
  )
}
