import { Aviso } from '../../../compartido/ui/Aviso'
import { EnlaceDeFila } from '../../../compartido/ui/EnlaceDeFila'
import { Estado } from '../../../compartido/ui/Estado'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { EncabezadoDeChevron, FilaNavegable } from '../../../compartido/ui/FilaNavegable'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { MenuDeFila } from '../../../compartido/ui/MenuDeFila'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { clasesDeBoton } from '../../../compartido/ui/clases'
import { IconoNuevo } from '../../../compartido/ui/iconos'
import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import type { UsuarioListado } from '../../../compartido/tipos'
import { DialogoConfirmacion } from '../../../compartido/ui/DialogoConfirmacion'
import { FiltrosUsuarios } from '../componentes/FiltrosUsuarios'
import {
  FILTROS_VACIOS,
  formatearFecha,
  formatearUltimoAcceso,
  NOMBRE_DE_ESTADO,
  type Filtros,
} from '../servicios/formato'
import { darDeBajaUsuario, listarUsuarios } from '../servicios/usuarios'

const MENSAJE_SIN_RESULTADOS = 'No hay usuarios que coincidan con los filtros aplicados.'

/**
 * Listado de usuarios (User Story 2 del Módulo 2).
 *
 * Muestra las seis columnas que exige FR-011 y, cuando ningún usuario coincide, un mensaje explícito
 * en vez de una tabla vacía sin explicación (FR-012).
 *
 * **La columna `Acciones` desaparece** (FR-046): *Editar*, *Roles* y *Dar de baja* pasan al menú
 * `···` del final de la fila, y *Ver* deja de existir como enlace aparte porque el nombre de
 * usuario **es** el enlace de la fila (FR-040). Los tres conservan su texto y su rol: lo único que
 * cambia es que hay que abrir el menú para llegar a ellos, y ése es el único cambio de esta clase
 * que una suite que consulta por rol y texto puede notar (FR-069).
 */
export function ListadoUsuarios() {
  const [filtros, setFiltros] = useState<Filtros>(FILTROS_VACIOS)
  const [usuarios, setUsuarios] = useState<UsuarioListado[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [aBajar, setABajar] = useState<UsuarioListado | null>(null)

  const traer = useCallback(() => {
    listarUsuarios(filtros)
      .then((lista) => {
        setUsuarios(lista)
        setError(null)
      })
      .catch(() => setError('No pudimos traer el listado. Volvé a intentar en unos minutos.'))
  }, [filtros])

  useEffect(() => {
    traer()
  }, [traer])

  /** Confirmada la baja, el usuario queda `inactivo` y sigue en el listado con ese estado. */
  async function confirmarBaja() {
    if (aBajar === null) {
      return
    }

    try {
      await darDeBajaUsuario(aBajar.id)
      setError(null)
      traer()
    } catch (fallo) {
      // Acá cae el rechazo de FR-019: sería el último administrador activo.
      setError(
        fallo instanceof ErrorHttp
          ? fallo.detalle.mensaje
          : 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
      )
    } finally {
      setABajar(null)
    }
  }

  return (
    <section>
      <EncabezadoDePantalla
        titulo="Gestión de usuarios"
        accionPrincipal={
          <Link to="/usuarios/nuevo" className={clasesDeBoton('primario')}>
            Nuevo usuario
            <span
              aria-hidden="true"
              className="flex size-8 shrink-0 items-center justify-center rounded-pastilla bg-white/[0.14] transition-transform duration-200 ease-gt group-hover:translate-x-0.5"
            >
              <IconoNuevo className="size-3" />
            </span>
          </Link>
        }
      />

      {error !== null && (
        <Aviso tono="error" rol="alert" className="mb-[18px]">
          {error}
        </Aviso>
      )}

      <Listado>
        <FiltrosUsuarios
          valor={filtros}
          onCambio={setFiltros}
          resumen={
            usuarios !== null && (
              <>
                <span>
                  {usuarios.length} {usuarios.length === 1 ? 'usuario' : 'usuarios'}
                </span>
                <span>Ordenado por nombre de usuario</span>
              </>
            )
          }
        />

        {usuarios === null && error === null && (
          <EstadoVacio caso="cargando" className="border-0 shadow-none">
            Cargando usuarios…
          </EstadoVacio>
        )}

        {usuarios !== null && usuarios.length === 0 && (
          <EstadoVacio caso="vacio" className="border-0 shadow-none">
            {MENSAJE_SIN_RESULTADOS}
          </EstadoVacio>
        )}

        {usuarios !== null && usuarios.length > 0 && (
          <TablaDesplazable>
            <table>
              <caption>Usuarios del sistema</caption>
              <thead>
                <tr>
                  <th scope="col">Nombre de usuario</th>
                  <th scope="col">Email</th>
                  <th scope="col">Estado</th>
                  <th scope="col">Roles</th>
                  <th scope="col">Fecha de alta</th>
                  <th scope="col">Último acceso</th>
                  <EncabezadoDeChevron />
                </tr>
              </thead>
              <tbody>
                {usuarios.map((usuario) => (
                  <FilaNavegable key={usuario.id} a={`/usuarios/${usuario.id}`}>
                    <td>
                      <EnlaceDeFila a={`/usuarios/${usuario.id}`}>{usuario.username}</EnlaceDeFila>
                    </td>
                    <td>{usuario.email}</td>
                    {/* Alta y baja: van como punto, no como pastilla (FR-055). */}
                    <td>
                      <Estado
                        valor={usuario.estado}
                        texto={NOMBRE_DE_ESTADO[usuario.estado]}
                        forma="punto"
                      />
                    </td>
                    <td>{usuario.roles.map((rol) => rol.nombre).join(', ')}</td>
                    <td>{formatearFecha(usuario.fechaAlta)}</td>
                    <td>{formatearUltimoAcceso(usuario.ultimoAcceso)}</td>
                    <td className="w-8">
                      {/* El nombre accesible nombra **la fila**, nunca sólo "Acciones" (FR-044). */}
                      <MenuDeFila
                        etiqueta={`Acciones de ${usuario.username}`}
                        items={[
                          { etiqueta: 'Editar', a: `/usuarios/${usuario.id}/editar` },
                          { etiqueta: 'Roles', a: `/usuarios/${usuario.id}/roles` },
                          ...(usuario.estado !== 'inactivo'
                            ? [
                                {
                                  etiqueta: 'Dar de baja',
                                  onSeleccionar: () => setABajar(usuario),
                                  destructivo: true,
                                },
                              ]
                            : []),
                        ]}
                      />
                    </td>
                  </FilaNavegable>
                ))}
              </tbody>
            </table>
          </TablaDesplazable>
        )}
      </Listado>

      {aBajar !== null && (
        <DialogoConfirmacion
          titulo="Dar de baja"
          mensaje={`¿Confirmás la baja de ${aBajar.username}? La cuenta va a quedar inactiva y no va a poder ingresar al sistema.`}
          onConfirmar={confirmarBaja}
          onCancelar={() => setABajar(null)}
        />
      )}
    </section>
  )
}
