import type { Sesion } from '../servicios/sesion'

interface Props {
  sesion: Sesion
}

/**
 * Primera pantalla después de un ingreso exitoso (FR-020).
 *
 * Muestra el usuario y sus roles; el menú y el botón de cerrar sesión viven en el layout, porque
 * son comunes a todas las pantallas con sesión abierta.
 */
export function PantallaInicio({ sesion }: Props) {
  const sinOpciones = sesion.opcionesMenu.length === 0

  return (
    <section className="inicio">
      <h1>Hola, {sesion.username}</h1>

      <p>
        {sesion.roles.length === 1 ? 'Tu rol: ' : 'Tus roles: '}
        {sesion.roles.map((rol) => rol.nombre).join(', ')}
      </p>

      {/* El menú vacío es un caso válido: un usuario cuyos roles todavía no habilitan ninguna
          funcionalidad implementada igual inicia sesión y llega acá (FR-020). */}
      {sinOpciones && (
        <p className="inicio__sin-opciones">
          Por ahora no tenés funcionalidades disponibles. Se irán agregando a medida que se
          implementen.
        </p>
      )}
    </section>
  )
}
