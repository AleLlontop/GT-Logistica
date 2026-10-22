import { useState } from 'react'
import { Boton } from '../../../compartido/ui/Boton'
import { consultarGrupos, queryAnalisis } from '../servicios/servicioAnalisis'
import { useSolicitudAnalisis } from '../servicios/useSolicitudAnalisis'
import type { DimensionesAnalisis, Distribucion, FiltrosAnalisis, GrupoCantidad, RecursoTemporal } from '../servicios/tiposAnalisis'
import { GraficoAccesible } from './GraficoAccesible'
import { EstadoConsultaAnalisis } from './EstadoConsultaAnalisis'
import { DineroAnalisis, TablaAnalisis } from './TablaAnalisis'
export function DistribucionReporte<R extends RecursoTemporal>({ titulo, recurso, dimension, filtros, sesionClave, distribucion, monetario = false }: {
  titulo: string; recurso: R; dimension: DimensionesAnalisis[R]; filtros: FiltrosAnalisis[R]; sesionClave: string | null; distribucion: Distribucion<GrupoCantidad & { importe?: string }>; monetario?: boolean
}) {
  const [abierto, setAbierto] = useState(false), [pagina, setPagina] = useState(1)
  const solicitud = useSolicitudAnalisis(abierto ? `${recurso}:${dimension}:${queryAnalisis({ ...filtros, pagina })}` : null, sesionClave, () => consultarGrupos(recurso, dimension, { ...filtros, pagina, tamanioPagina: 20 }))
  const grupos = [...distribucion.top, ...(distribucion.otros ? [distribucion.otros] : [])]
  return <div className="space-y-3"><GraficoAccesible titulo={titulo} unidad={monetario ? 'ARS' : 'cantidad'} monetario={monetario}
    datos={grupos.map(g => ({ etiqueta: g.nombre, valores: { valor: monetario ? g.importe! : g.cantidad } }))} series={[{ clave: 'valor', nombre: monetario ? 'Importe' : 'Cantidad' }]}
    recorte={`Hasta 10 categorías y Otros. ${distribucion.totalCategorias} categorías en total. Nombres actuales, agrupados por identidad.`} onVerCategorias={() => setAbierto(true)} />
    {abierto && <section aria-label={`Categorías completas de ${titulo}`}><Boton variante="secundario" onClick={() => setAbierto(false)}>Cerrar categorías de {titulo}</Boton>
      <EstadoConsultaAnalisis solicitud={solicitud} />
      {solicitud.estado === 'listo' && <TablaAnalisis titulo={`Todas las categorías de ${titulo}`} pagina={solicitud.datos.detalle} clave={g => g.id ?? 'sin-identidad'} alPaginar={setPagina}
        columnas={[{ titulo: 'Categoría (nombre actual)', valor: g => g.nombre }, { titulo: 'Cantidad', valor: g => g.cantidad }, ...(monetario ? [{ titulo: 'Importe', monetaria: true, valor: (g: GrupoCantidad & { importe?: string }) => <DineroAnalisis valor={g.importe!} /> }] : [])]} />}
    </section>}
  </div>
}
