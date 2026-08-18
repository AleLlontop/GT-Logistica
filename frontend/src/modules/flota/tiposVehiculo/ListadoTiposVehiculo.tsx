import { Aviso } from '../../../compartido/ui/Aviso'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { DialogoConfirmacion } from '../../../compartido/ui/DialogoConfirmacion'
import { FormularioTipoVehiculo } from './FormularioTipoVehiculo'
import { darDeBajaTipoVehiculo, listarTiposVehiculo, type TipoVehiculo } from './servicioTiposVehiculo'

const MENSAJE_CATALOGO_VACIO =
  'Todavía no hay tipos de vehículo cargados. Cargá el primero para poder registrar unidades.'

/**
 * Catálogo de tipos de vehículo (User Story 1).
 *
 * El catálogo arranca vacío y no se precarga: sin al menos un tipo activo no se puede registrar
 * ninguna unidad, así que la pantalla lo dice explícitamente en vez de mostrar una tabla vacía
 * (FR-036, US1 esc. 1).
 *
 * Sólo la ve el Administrador del sistema: es el primer módulo del proyecto con dos niveles de acceso
 * adentro, y el ABM del catálogo lleva su propio permiso (FR-039).
 */
export function ListadoTiposVehiculo() {
  const [tipos, setTipos] = useState<TipoVehiculo[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [aviso, setAviso] = useState<string | null>(null)

  const [enEdicion, setEnEdicion] = useState<TipoVehiculo | null>(null)
  const [aBajar, setABajar] = useState<TipoVehiculo | null>(null)

  const traer = useCallback(() => {
    listarTiposVehiculo()
      .then((lista) => {
        setTipos(lista)
        setError(null)
      })
      .catch(() =>
        setError('No pudimos traer el catálogo de tipos. Volvé a intentar en unos minutos.'),
      )
  }, [])

  useEffect(() => {
    traer()
  }, [traer])

  async function confirmarBaja() {
    if (aBajar === null) {
      return
    }

    try {
      await darDeBajaTipoVehiculo(aBajar.id)
      setError(null)
      setAviso(`El tipo ${aBajar.nombre} quedó inactivo. Deja de ofrecerse al registrar vehículos.`)
      traer()
    } catch (fallo) {
      // Acá cae el rechazo de FR-010: el tipo tiene vehículos. El mensaje dice cuántos.
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
        titulo="Tipos de vehículo"
        accionPrincipal={
          <>
            <Link to="/flota">Volver a la flota</Link>
          </>
        }
      />
      {error !== null && (
        <Aviso tono="error" rol="alert" className="mb-4">
          {error}
        </Aviso>
      )}
      {aviso !== null && <p role="status">{aviso}</p>}

      <FormularioTipoVehiculo
        enEdicion={enEdicion}
        onGuardado={(mensaje) => {
          setEnEdicion(null)
          setError(null)
          setAviso(mensaje)
          traer()
        }}
        onCancelar={() => setEnEdicion(null)}
      />

      {tipos === null && error === null && (
        <EstadoVacio caso="cargando" className="border-0 shadow-none">
          Cargando tipos de vehículo…
        </EstadoVacio>
      )}

      {tipos !== null && tipos.length === 0 && <EstadoVacio caso="vacio" className="border-0 shadow-none">
          {MENSAJE_CATALOGO_VACIO}
        </EstadoVacio>}

      {tipos !== null && tipos.length > 0 && (
        <Listado>
          <TablaDesplazable>
            <table>
          <caption>Catálogo de tipos de vehículo</caption>
          <thead>
            <tr>
              <th scope="col">Nombre</th>
              <th scope="col">Estado</th>
              <th scope="col">Vehículos que lo usan</th>
              <th scope="col">Acciones</th>
            </tr>
          </thead>
          <tbody>
            {tipos.map((tipo) => (
              <tr key={tipo.id}>
                <td>{tipo.nombre}</td>
                {/* El estado va con su palabra, nunca sólo con un color (convención [003]). */}
                <td>{tipo.activo ? 'Activo' : 'Inactivo'}</td>
                {/* Es lo que explica por qué algunos no se pueden dar de baja (FR-010). */}
                <td>{tipo.cantidadVehiculos}</td>
                <td>
                  <button type="button" onClick={() => setEnEdicion(tipo)}>
                    Editar
                  </button>
                  {tipo.activo && (
                    <button type="button" onClick={() => setABajar(tipo)}>
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
          titulo="Dar de baja el tipo de vehículo"
          mensaje={
            `¿Confirmás la baja de ${aBajar.nombre}? Va a dejar de ofrecerse al registrar unidades. ` +
            'Las que ya lo usan lo conservan.'
          }
          onConfirmar={confirmarBaja}
          onCancelar={() => setABajar(null)}
        />
      )}
    </section>
  )
}
