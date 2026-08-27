import { AsideDeFicha, BloqueDeAside } from '../../../compartido/ui/AsideDeFicha'
import { Boton } from '../../../compartido/ui/Boton'
import { Estado } from '../../../compartido/ui/Estado'
import { FichaCuerpo, FichaSeccion } from '../../../compartido/ui/Ficha'
import { clasesDeBoton } from '../../../compartido/ui/clases'
import { IconoEditar } from '../../../compartido/ui/iconos'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import type { UsuarioDetalle } from '../../../compartido/tipos'
import { DialogoConfirmacion } from '../../../compartido/ui/DialogoConfirmacion'
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
      <section>
        <p role="alert">{error}</p>
        <Link to="/usuarios">Volver al listado</Link>
      </section>
    )
  }

  if (usuario === null) {
    return (
      <section>
        <p role="status">Cargando usuario…</p>
      </section>
    )
  }

  return (
    <section>
      {/*
        **Esta ficha pasa a usar `EncabezadoDePantalla` como las otras cuatro** (FR-019, FR-022): era
        la única que armaba su propio `<nav aria-label="Acciones sobre el usuario">` al pie de la
        pantalla. Sus cuatro controles conservan texto y rol; lo que cambia es dónde viven — el
        *volver* arriba a la izquierda como salida, y las tres acciones en la línea de decisión, con
        *Editar* como principal (FR-066).
      */}
      <EncabezadoDePantalla
        titulo={usuario.username}
        volverA={{ ruta: '/usuarios', etiqueta: 'Volver al listado' }}
        resumen={
          <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <span>{usuario.email}</span>
            <span aria-hidden="true">
              ·
            </span>
            {/* Alta y baja van como punto (FR-055). El estado sube al encabezado (FR-052). */}
            <Estado
              valor={usuario.estado}
              texto={NOMBRE_DE_ESTADO[usuario.estado]}
              forma="punto"
            />
          </span>
        }
        accionPrincipal={
          <>
            <Link to={`/usuarios/${usuario.id}/roles`} className={clasesDeBoton('secundario')}>
              Roles
            </Link>

            {/* No hay campo de contraseña: el sistema la genera y se la manda al usuario. El
                responsable de sistemas no la elige ni la ve (FR-009 del Módulo 2). */}
            <Boton
              variante="secundario"
              onClick={() => setConfirmando(true)}
              disabled={restableciendo}
            >
              {restableciendo ? 'Restableciendo…' : 'Restablecer contraseña'}
            </Boton>

            <Link to={`/usuarios/${usuario.id}/editar`} className={clasesDeBoton('primario')}>
              Editar
              <span
                aria-hidden="true"
                className="flex size-8 shrink-0 items-center justify-center rounded-pastilla bg-white/[0.14] transition-transform duration-200 ease-gt group-hover:translate-x-0.5"
              >
                <IconoEditar className="size-3" />
              </span>
            </Link>
          </>
        }
      />

      {aviso !== null && (
        <p
          className="detalle__aviso mb-[18px] rounded-card border border-line bg-estado-rendido-bg px-[18px] py-4 text-[13px] leading-5 font-medium text-estado-rendido"
          role="status"
        >
          {aviso}
        </p>
      )}

      <FichaCuerpo
        aside={
          <AsideDeFicha
            /* El dato de más valor de un usuario son sus roles: es lo que responde qué puede hacer
               (data-model §8). */
            destacado={
              <div className="flex flex-col gap-2.5">
                <span className="text-[9.5px] font-bold tracking-[0.14em] text-encabezado uppercase">
                  Roles
                </span>
                {usuario.roles.length === 0 ? (
                  /* El vacío se escribe: dice qué falta, nunca un guión (FR-038). */
                  <p className="atenuada m-0 text-[12.5px]">Sin roles asignados</p>
                ) : (
                  <ul className="m-0 flex list-none flex-wrap gap-1.5 p-0">
                    {usuario.roles.map((rol) => (
                      <li
                        key={rol.codigo}
                        className="rounded-pastilla border border-line-strong bg-surface-mute px-2.5 py-1 text-[12.5px] font-semibold text-ink-soft"
                      >
                        {rol.nombre}
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            }
          >
            <BloqueDeAside titulo="Actividad">
              <p className="m-0">Alta el {formatearFecha(usuario.fechaAlta)}</p>
              <p className="m-0 text-[12.5px] text-faint">
                {formatearUltimoAcceso(usuario.ultimoAcceso)}
              </p>
            </BloqueDeAside>
          </AsideDeFicha>
        }
      >
        <FichaSeccion titulo="Datos de la cuenta" id="titulo-datos-usuario">
          <dl>
            <dt>Nombre de usuario</dt>
            <dd>{usuario.username}</dd>

            <dt>Email</dt>
            <dd>{usuario.email}</dd>

            <dt>Fecha de alta</dt>
            <dd>{formatearFecha(usuario.fechaAlta)}</dd>

            <dt>Último acceso</dt>
            <dd>{formatearUltimoAcceso(usuario.ultimoAcceso)}</dd>

            <dt>Persona asociada</dt>
            <dd>
              {/* Que no tenga ninguna es válido y habitual: se dice con texto, no con un espacio en
                  blanco (FR-008 del Módulo 2). */}
              {usuario.persona === null ? (
                'Sin persona asociada'
              ) : (
                <>
                  {nombreCompleto(usuario.persona)} — DNI{' '}
                  <span className="font-mono">{usuario.persona.dni}</span> —{' '}
                  {NOMBRE_DE_TIPO_INTEGRANTE[usuario.persona.tipo]}
                </>
              )}
            </dd>
          </dl>
        </FichaSeccion>
      </FichaCuerpo>

      {confirmando && (
        <DialogoConfirmacion
          titulo="Restablecer contraseña"
          mensaje={`Se va a generar una contraseña temporal y enviarla a ${usuario.email}. Si ${usuario.username} tiene una sesión abierta, se va a cerrar.`}
          onConfirmar={restablecer}
          onCancelar={() => setConfirmando(false)}
        />
      )}
    </section>
  )
}
