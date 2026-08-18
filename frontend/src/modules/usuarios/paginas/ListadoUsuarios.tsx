import { Aviso } from '../../../compartido/ui/Aviso'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
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
 * Listado de usuarios (User Story 2).
 *
 * Muestra las seis columnas que exige FR-011 y, cuando ningún usuario coincide, un mensaje explícito
 * en vez de una tabla vacía sin explicación (FR-012).
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
          <>
            <Link to="/usuarios/nuevo">Nuevo usuario</Link>
          </>
        }
      />
      <FiltrosUsuarios valor={filtros} onCambio={setFiltros} />

      {error !== null && (
        <Aviso tono="error" rol="alert" className="mb-4">
          {error}
        </Aviso>
      )}

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
        <Listado>
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
              <th scope="col">Acciones</th>
            </tr>
          </thead>
          <tbody>
            {usuarios.map((usuario) => (
              <tr key={usuario.id}>
                <td>{usuario.username}</td>
                <td>{usuario.email}</td>
                <td>{NOMBRE_DE_ESTADO[usuario.estado]}</td>
                <td>{usuario.roles.map((rol) => rol.nombre).join(', ')}</td>
                <td>{formatearFecha(usuario.fechaAlta)}</td>
                <td>{formatearUltimoAcceso(usuario.ultimoAcceso)}</td>
                <td>
                  <Link to={`/usuarios/${usuario.id}`}>Ver</Link>
                  <Link to={`/usuarios/${usuario.id}/editar`}>Editar</Link>
                  <Link to={`/usuarios/${usuario.id}/roles`}>Roles</Link>
                  {usuario.estado !== 'inactivo' && (
                    <button type="button" onClick={() => setABajar(usuario)}>
                      Dar de baja
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
          </TablaDesplazable>
        </Listado>
      )}

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
