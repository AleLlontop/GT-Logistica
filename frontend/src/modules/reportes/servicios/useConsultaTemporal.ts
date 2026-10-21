import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { erroresPeriodo, mesActualArgentina } from './periodoAnalisis'
import { consultarAnalisis, queryAnalisis } from './servicioAnalisis'
import { useSolicitudAnalisis } from './useSolicitudAnalisis'
import type { FiltrosAnalisis, RecursoTemporal } from './tiposAnalisis'
import type { AccesoReportes } from '../paginas/Dashboard'

/** Estado local por perspectiva; los parámetros iniciales sólo transfieren días, nunca DTO. */
export function useConsultaTemporal<R extends RecursoTemporal>(recurso: R, permiso: string, acceso: AccesoReportes, iniciales?: Partial<FiltrosAnalisis[R]>) {
  const [query] = useSearchParams()
  const [inicial] = useState(() => {
    const periodo = { desde: query.get('desde') ?? '', hasta: query.get('hasta') ?? '' }
    return Object.keys(erroresPeriodo(periodo)).length ? mesActualArgentina() : periodo
  })
  const [borrador, setBorrador] = useState<FiltrosAnalisis[R]>(() => ({ ...inicial, pagina: 1, tamanioPagina: 20, ...iniciales } as FiltrosAnalisis[R]))
  const [aplicados, setAplicados] = useState(borrador)
  const [intento, setIntento] = useState(false)
  const [reinicios, setReinicios] = useState(0)
  const permitido = acceso.permisos.includes('reportes.consultar') && acceso.permisos.includes(permiso)
  const solicitud = useSolicitudAnalisis(permitido ? `${recurso}${queryAnalisis(aplicados)}` : null, acceso.sesionClave, () => consultarAnalisis(recurso, aplicados))
  return { recurso, acceso, borrador, setBorrador, aplicados, solicitud, intento, reinicios,
    aplicar: () => { setIntento(true); if (!Object.keys(erroresPeriodo(borrador)).length) setAplicados({ ...borrador, pagina: 1 }) },
    limpiar: () => { const nuevo = { ...mesActualArgentina(), pagina: 1, tamanioPagina: 20 } as FiltrosAnalisis[R]; setBorrador(nuevo); setAplicados(nuevo); setIntento(false); setReinicios(n => n + 1) },
    paginar: (pagina: number) => setAplicados(actual => ({ ...actual, pagina })),
  }
}
