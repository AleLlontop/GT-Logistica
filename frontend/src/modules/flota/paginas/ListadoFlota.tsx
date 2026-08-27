import { Estado } from '../../../compartido/ui/Estado'
import { Aviso } from '../../../compartido/ui/Aviso'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { EncabezadoDeChevron, FilaNavegable } from '../../../compartido/ui/FilaNavegable'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { TokenDeIdentificador } from '../../../compartido/ui/TokenDeIdentificador'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { clasesDeBoton } from '../../../compartido/ui/clases'
import { IconoNuevo } from '../../../compartido/ui/iconos'
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
            {/* La segunda de las dos acciones secundarias reales del sistema (FR-019). */}
            <Link to="/flota/vencimientos" className={clasesDeBoton('secundario')}>
              Ver vencimientos
            </Link>
            <Link to="/flota/nuevo" className={clasesDeBoton('primario')}>
              Registrar unidad
              <span
                aria-hidden="true"
                className="flex size-8 shrink-0 items-center justify-center rounded-pastilla bg-white/[0.14] transition-transform duration-200 ease-gt group-hover:translate-x-0.5"
              >
                <IconoNuevo className="size-3" />
              </span>
            </Link>
          </>
        }
      />

      {error !== null && (
        <Aviso tono="error" rol="alert" className="mb-[18px]">
          {error}
        </Aviso>
      )}

      <Listado>
        <FiltrosFlota
          filtros={filtros}
          transportistas={transportistas}
          tipos={tipos}
          onCambiar={actualizarFiltro}
          onLimpiar={() => {
            setFiltros(FILTROS_FLOTA_INICIALES)
            setPagina(1)
          }}
          resumen={
            resultado !== null && (
              <>
                <span>
                  {resultado.total} {resultado.total === 1 ? 'unidad' : 'unidades'}
                </span>
                <span>Ordenado por patente</span>
              </>
            )
          }
        />

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
          <TablaDesplazable>
            <table>
              <caption>Unidades de la flota</caption>
              <thead>
                <tr>
                  <th scope="col">Patente</th>
                  {/* `Marca` + `Modelo` nombran un solo concepto: el vehículo (FR-047). */}
                  <th scope="col">Vehículo</th>
                  <th scope="col">Tipo</th>
                  <th scope="col">Transportista</th>
                  <th scope="col">Estado</th>
                  <th scope="col">Documentación</th>
                  {/*
                    La columna `Acciones` **desaparece y no deja ningún `···`**: contenía sólo
                    *Ver ficha*, que es ahora la patente como enlace de fila (FR-046).
                  */}
                  <EncabezadoDeChevron />
                </tr>
              </thead>
              <tbody>
                {resultado.items.map((vehiculo) => (
                  <FilaNavegable key={vehiculo.id} a={`/flota/${vehiculo.id}`}>
                    {/* La patente es el identificador con el que se busca una unidad: va en mono
                        y es el enlace de la fila (FR-040, FR-041). */}
                    <td>
                      <TokenDeIdentificador
                        a={`/flota/${vehiculo.id}`}
                        numero={vehiculo.patente}
                      />
                    </td>
                    <td>
                      {vehiculo.marca} {vehiculo.modelo}
                    </td>
                    <td>{vehiculo.tipo.nombre}</td>
                    <td>{vehiculo.transportista.nombre}</td>
                    {/* El estado nunca se comunica sólo por color: el texto siempre acompaña. */}
                    <td>
                      <Estado
                        valor={vehiculo.estado}
                        texto={TEXTO_ESTADO_VEHICULO[vehiculo.estado]}
                        forma="pastilla"
                      />
                      {/* Una unidad dada de baja lleva la palabra que lo explica ([003], FR-060). */}
                      {!vehiculo.activo && (
                        <span className="atenuada ml-1.5 text-[11.5px]">— Dada de baja</span>
                      )}
                    </td>
                    <td>
                      <Estado
                        valor={vehiculo.estadoDocumentacion}
                        texto={TEXTO_ESTADO_DOCUMENTACION[vehiculo.estadoDocumentacion]}
                        forma="pastilla"
                      />
                    </td>
                  </FilaNavegable>
                ))}
              </tbody>
            </table>
          </TablaDesplazable>
        )}
      </Listado>

      {resultado !== null && resultado.items.length > 0 && (
        <Paginacion
          nombrePlural="vehículos"
          pagina={resultado.pagina}
          total={resultado.total}
          tamanioPagina={resultado.tamanioPagina}
          onCambiarPagina={setPagina}
        />
      )}
    </section>
  )
}
