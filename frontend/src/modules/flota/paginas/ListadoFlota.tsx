import { Estado } from '../../../compartido/ui/Estado'
import { Aviso } from '../../../compartido/ui/Aviso'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  listarTransportistas,
  type Transportista,
} from '../../choferes/transportistas/servicioTransportistas'
import { FiltrosFlota } from '../componentes/FiltrosFlota'
import { Paginacion } from '../../../compartido/ui/Paginacion'
import { listarTiposVehiculo, type TipoVehiculo } from '../tiposVehiculo/servicioTiposVehiculo'
import {
  TEXTO_ESTADO_DOCUMENTACION,
  TEXTO_ESTADO_VEHICULO,
} from '../servicios/estados'
import {
  FILTROS_FLOTA_INICIALES,
  listarFlota,
  type FiltrosFlota as Filtros,
  type PaginaDe,
  type VehiculoListado,
} from '../servicios/servicioFlota'

// Dos mensajes distintos a propósito (FR-036): "todavía no hay ninguna" y "tus filtros no
// encontraron nada" son situaciones distintas y llevan a acciones distintas.
const MENSAJE_SIN_VEHICULOS = 'Todavía no hay unidades registradas. Registrá la primera para empezar.'
const MENSAJE_SIN_COINCIDENCIAS = 'Ningún vehículo coincide con los filtros aplicados.'

/**
 * Listado de la flota (User Story 4).
 *
 * Es la pantalla que responde, antes de asignar un viaje, qué unidad está en condiciones de salir a
 * la ruta.
 *
 * La columna **Estado** muestra el estado operativo **derivado**, no el guardado: una unidad guardada
 * como disponible cuyo seguro venció figura como "Fuera de servicio" sin que nadie la haya editado
 * (FR-014).
 */
export function ListadoFlota() {
  const [filtros, setFiltros] = useState<Filtros>(FILTROS_FLOTA_INICIALES)
  const [pagina, setPagina] = useState(1)
  const [resultado, setResultado] = useState<PaginaDe<VehiculoListado> | null>(null)
  const [transportistas, setTransportistas] = useState<Transportista[]>([])
  const [tipos, setTipos] = useState<TipoVehiculo[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    listarTransportistas(undefined, true).then(setTransportistas).catch(() => setTransportistas([]))
    listarTiposVehiculo(true).then(setTipos).catch(() => setTipos([]))
  }, [])

  const traer = useCallback(() => {
    listarFlota(filtros, pagina)
      .then((resultados) => {
        setResultado(resultados)
        setError(null)
      })
      .catch(() =>
        setError('No pudimos traer el listado de la flota. Volvé a intentar en unos minutos.'),
      )
  }, [filtros, pagina])

  useEffect(() => {
    traer()
  }, [traer])

  /** Cambiar cualquier filtro vuelve a la página 1: si no, se ve una página vacía (FR-032). */
  function actualizarFiltro<C extends keyof Filtros>(campo: C, valor: Filtros[C]) {
    setFiltros((previos) => ({ ...previos, [campo]: valor }))
    setPagina(1)
  }

  const filtrando =
    filtros.transportistaId !== '' ||
    filtros.tipoVehiculoId !== '' ||
    filtros.estado !== '' ||
    filtros.estadoDocumentacion !== ''

  return (
    <section>
      <EncabezadoDePantalla
        titulo="Flota"
        accionPrincipal={
          <>
            <Link to="/flota/nuevo">Registrar unidad</Link>
            <Link to="/flota/vencimientos">Ver vencimientos</Link>
          </>
        }
      />
      <FiltrosFlota
        filtros={filtros}
        transportistas={transportistas}
        tipos={tipos}
        onCambiar={actualizarFiltro}
        onLimpiar={() => {
          setFiltros(FILTROS_FLOTA_INICIALES)
          setPagina(1)
        }}
      />

      {error !== null && (
        <Aviso tono="error" rol="alert" className="mb-4">
          {error}
        </Aviso>
      )}

      {resultado === null && error === null && (
        <EstadoVacio caso="cargando" className="border-0 shadow-none">
          Cargando la flota…
        </EstadoVacio>
      )}

      {resultado !== null && resultado.items.length === 0 && (
        <EstadoVacio
          caso={filtrando ? 'sinCoincidencias' : 'vacio'}
          className="border-0 shadow-none"
        >
          {filtrando ? MENSAJE_SIN_COINCIDENCIAS : MENSAJE_SIN_VEHICULOS}
        </EstadoVacio>
      )}

      {resultado !== null && resultado.items.length > 0 && (
        <>
          <Listado>
          <TablaDesplazable>
            <table>
            <caption>Unidades de la flota</caption>
            <thead>
              <tr>
                <th scope="col">Patente</th>
                <th scope="col">Marca</th>
                <th scope="col">Modelo</th>
                <th scope="col">Tipo</th>
                <th scope="col">Transportista</th>
                <th scope="col">Estado</th>
                <th scope="col">Documentación</th>
                <th scope="col">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {resultado.items.map((vehiculo) => (
                <tr key={vehiculo.id}>
                  <td>{vehiculo.patente}</td>
                  <td>{vehiculo.marca}</td>
                  <td>{vehiculo.modelo}</td>
                  <td>{vehiculo.tipo.nombre}</td>
                  <td>{vehiculo.transportista.nombre}</td>
                  {/* El estado nunca se comunica sólo por color: el texto siempre acompaña. */}
                  <td>
                    <Estado valor={vehiculo.estado} texto={TEXTO_ESTADO_VEHICULO[vehiculo.estado]} />
                    {/* Una unidad dada de baja lleva la palabra que lo explica (convención [003]). */}
                    {!vehiculo.activo && ' — Dada de baja'}
                  </td>
                  <td>
                    <Estado valor={vehiculo.estadoDocumentacion} texto={TEXTO_ESTADO_DOCUMENTACION[vehiculo.estadoDocumentacion]} />
                  </td>
                  <td>
                    <Link to={`/flota/${vehiculo.id}`}>Ver ficha</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          </TablaDesplazable>
        </Listado>

          <Paginacion
            nombrePlural="vehículos"
            pagina={resultado.pagina}
            total={resultado.total}
            tamanioPagina={resultado.tamanioPagina}
            onCambiarPagina={setPagina}
          />
        </>
      )}
    </section>
  )
}
