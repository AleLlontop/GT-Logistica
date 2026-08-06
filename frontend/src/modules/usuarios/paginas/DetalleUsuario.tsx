import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import type { UsuarioDetalle } from '../../../compartido/tipos'
import { DialogoConfirmacion } from '../componentes/DialogoConfirmacion'
import { nombreCompleto } from '../personas/servicios/personas'
import {
  formatearFecha,
  formatearUltimoAcceso,
  NOMBRE_DE_ESTADO,
  NOMBRE_DE_TIPO_INTEGRANTE,
} from '../servicios/formato'
import { obtenerUsuario, restablecerPassword } from '../servicios/usuarios'

/**
 * Detalle de un usuario (FR-013).
 *
 * Muestra sus datos completos y la persona asociada si tiene una. **La contraseña no aparece de
 * ninguna forma**: ni el valor, ni un campo enmascarado, ni un botón de "ver".
 */
export function DetalleUsuario() {
  const { id } = useParams<{ id: string }>()

  const [usuario, setUsuario] = useState<UsuarioDetalle | null>(null)
  const [error, setError] = useState<string | null>(null)

  const [confirmando, setConfirmando] = useState(false)
  const [restableciendo, setRestableciendo] = useState(false)
  const [aviso, setAviso] = useState<string | null>(null)

  /**
   * Pide el restablecimiento. La respuesta trae el mensaje ya armado —incluido el de envío
   * fallido— y se muestra tal cual (FR-021).
   */
  async function restablecer() {
    setConfirmando(false)
    setRestableciendo(true)

    try {
      const resultado = await restablecerPassword(Number(id))
      setAviso(resultado.mensaje)
    } catch (fallo) {
      setAviso(
        fallo instanceof ErrorHttp
          ? fallo.detalle.mensaje
          : 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
      )
    } finally {
      setRestableciendo(false)
    }
  }

  useEffect(() => {
    let vigente = true

    obtenerUsuario(Number(id))
      .then((datos) => {
        if (vigente) {
          setUsuario(datos)
          setError(null)
        }
      })
      .catch((fallo) => {
        if (vigente) {
          setError(
            fallo instanceof ErrorHttp
              ? fallo.detalle.mensaje
              : 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
          )
        }
      })

    return () => {
      vigente = false
    }
  }, [id])

  if (error !== null) {
    return (
      <main>
        <p role="alert">{error}</p>
        <Link to="/usuarios">Volver al listado</Link>
      </main>
    )
  }

  if (usuario === null) {
    return (
      <main>
        <p role="status">Cargando usuario…</p>
      </main>
    )
  }

  return (
    <main>
      <h1>{usuario.username}</h1>

      <dl>
        <dt>Nombre de usuario</dt>
        <dd>{usuario.username}</dd>

        <dt>Email</dt>
        <dd>{usuario.email}</dd>

        <dt>Estado</dt>
        <dd>{NOMBRE_DE_ESTADO[usuario.estado]}</dd>

        <dt>Roles</dt>
        <dd>{usuario.roles.map((rol) => rol.nombre).join(', ')}</dd>

        <dt>Fecha de alta</dt>
        <dd>{formatearFecha(usuario.fechaAlta)}</dd>

        <dt>Último acceso</dt>
        <dd>{formatearUltimoAcceso(usuario.ultimoAcceso)}</dd>

        <dt>Persona asociada</dt>
        <dd>
          {/* Que no tenga ninguna es válido y habitual: se dice con texto, no con un espacio en
              blanco (FR-008). */}
          {usuario.persona === null ? (
            'Sin persona asociada'
          ) : (
            <>
              {nombreCompleto(usuario.persona)} — DNI {usuario.persona.dni} —{' '}
              {NOMBRE_DE_TIPO_INTEGRANTE[usuario.persona.tipo]}
            </>
          )}
        </dd>
      </dl>

      {aviso !== null && (
        <p className="detalle__aviso" role="status">
          {aviso}
        </p>
      )}

      <nav aria-label="Acciones sobre el usuario">
        <Link to={`/usuarios/${usuario.id}/editar`}>Editar</Link>
        <Link to={`/usuarios/${usuario.id}/roles`}>Roles</Link>

        {/* No hay campo de contraseña: el sistema la genera y se la manda al usuario. El responsable
            de sistemas no la elige ni la ve (FR-009). */}
        <button type="button" onClick={() => setConfirmando(true)} disabled={restableciendo}>
          {restableciendo ? 'Restableciendo…' : 'Restablecer contraseña'}
        </button>

        <Link to="/usuarios">Volver al listado</Link>
      </nav>

      {confirmando && (
        <DialogoConfirmacion
          titulo="Restablecer contraseña"
          mensaje={`Se va a generar una contraseña temporal y enviarla a ${usuario.email}. Si ${usuario.username} tiene una sesión abierta, se va a cerrar.`}
          onConfirmar={restablecer}
          onCancelar={() => setConfirmando(false)}
        />
      )}
    </main>
  )
}
