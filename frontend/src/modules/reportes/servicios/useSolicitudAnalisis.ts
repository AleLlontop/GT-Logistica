import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'

interface ResultadoSolicitud<T> { identidad: string; datos: T | null; error: unknown; correcto: boolean }
export type SolicitudAnalisis<T> =
  | { estado: 'inactivo' | 'cargando'; datos: null; error: null; reintentar: () => void }
  | { estado: 'listo'; datos: T; error: null; reintentar: () => void }
  | { estado: 'error'; datos: null; error: unknown; reintentar: () => void }

/** Identidad por consulta/sesión y secuencia; una respuesta anterior nunca recibe el contexto nuevo. */
export function useSolicitudAnalisis<T>(clave: string | null, sesionClave: string | null, cargar: () => Promise<T>): SolicitudAnalisis<T> {
  const [revision, setRevision] = useState(0)
  const [resultado, setResultado] = useState<ResultadoSolicitud<T> | null>(null)
  const secuencia = useRef(0)
  const carga = useRef(cargar)
  useLayoutEffect(() => { carga.current = cargar }, [cargar])
  const identidad = clave === null || sesionClave === null ? null : JSON.stringify([sesionClave, clave, revision])
  const reintentar = useCallback(() => setRevision(actual => actual + 1), [])
  useEffect(() => {
    setResultado(null)
    if (identidad === null) return
    let activa = true
    const numero = ++secuencia.current
    const solicitar = carga.current
    Promise.resolve().then(solicitar).then(
      datos => { if (activa && numero === secuencia.current) setResultado({ identidad, datos, error: null, correcto: true }) },
      error => { if (activa && numero === secuencia.current) setResultado({ identidad, datos: null, error, correcto: false }) },
    )
    return () => { activa = false }
  }, [identidad])

  if (identidad === null) return { estado: 'inactivo', datos: null, error: null, reintentar }
  // Esta comparación oculta lo anterior incluso en el render previo a ejecutar el efecto nuevo.
  if (resultado?.identidad !== identidad) return { estado: 'cargando', datos: null, error: null, reintentar }
  if (!resultado.correcto) return { estado: 'error', datos: null, error: resultado.error, reintentar }
  return { estado: 'listo', datos: resultado.datos as T, error: null, reintentar }
}

/** El borrador de campos no se confunde con el filtro que identifica la respuesta vigente. */
export function useFiltrosAnalisis<T extends object>(inicial: T) {
  const [borrador, setBorrador] = useState<T>(() => ({ ...inicial }))
  const [aplicados, setAplicados] = useState<T>(() => ({ ...inicial }))
  function aplicar() { setAplicados({ ...borrador }) }
  function restablecer(filtros: T) { setBorrador({ ...filtros }); setAplicados({ ...filtros }) }
  return { borrador, setBorrador, aplicados, aplicar, restablecer }
}
