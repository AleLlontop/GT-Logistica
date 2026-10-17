import { useEffect, useId, useRef, useState } from 'react'
import { Campo } from '../../../compartido/ui/Campo'
import { Boton } from '../../../compartido/ui/Boton'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { formatearCuit } from '../../../compartido/cuit'
import { mensajeProveedor } from '../servicios/proveedores'
import { buscarProveedores } from '../servicios/facturasProveedor'
import type { Pagina, ProveedorResumen } from '../servicios/tipos'

export function AutocompleteProveedor({ soloActivos, onSeleccionar, error, disabled = false }: {
  soloActivos: boolean; onSeleccionar: (proveedor: ProveedorResumen | null) => void; error?: string; disabled?: boolean
}) {
  const id = useId()
  const [texto, setTexto] = useState(''); const [consulta, setConsulta] = useState('')
  const [pagina, setPagina] = useState(1); const [resultados, setResultados] = useState<Pagina<ProveedorResumen> | null>(null)
  const [abierto, setAbierto] = useState(false); const [activo, setActivo] = useState(-1)
  const [cargando, setCargando] = useState(false); const [fallo, setFallo] = useState('')
  const secuencia = useRef(0)
  useEffect(() => {
    if (!abierto) return
    let vigente = true
    const turno = ++secuencia.current
    setCargando(true); setFallo(''); setResultados(null); setActivo(-1)
    const timer = setTimeout(() => {
      buscarProveedores(consulta, soloActivos, pagina).then(r => { if (vigente && turno === secuencia.current) setResultados(r) })
        .catch(e => { if (vigente && turno === secuencia.current) setFallo(mensajeProveedor(e)) })
        .finally(() => { if (vigente && turno === secuencia.current) setCargando(false) })
    }, 250)
    return () => { vigente = false; clearTimeout(timer) }
  }, [consulta, pagina, soloActivos, abierto])
  function seleccionar(p: ProveedorResumen) { secuencia.current++; setTexto(p.razonSocial); setAbierto(false); onSeleccionar(p) }
  return <div onBlur={e => { if (!e.currentTarget.contains(e.relatedTarget)) { secuencia.current++; setAbierto(false) } }}>
    <Campo id={id} etiqueta="Proveedor" obligatorio={soloActivos} error={error} ancho="largo">
      <input id={id} role="combobox" autoComplete="off" className={clasesDeControl(Boolean(error))} disabled={disabled} required={soloActivos}
        aria-expanded={abierto} aria-controls={`${id}-lista`} aria-autocomplete="list" aria-activedescendant={abierto && activo >= 0 ? `${id}-${activo}` : undefined}
        aria-invalid={Boolean(error)} aria-describedby={error ? `${id}-error` : undefined} value={texto}
        onChange={e => { secuencia.current++; setTexto(e.target.value); setConsulta(e.target.value); setPagina(1); setResultados(null); setActivo(-1); setAbierto(true); onSeleccionar(null) }}
        onKeyDown={e => {
          if (e.key === 'Escape' || e.key === 'Tab') { secuencia.current++; setAbierto(false); return }
          if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
            e.preventDefault(); setAbierto(true)
            const cantidad = resultados?.items.length ?? 0
            if (cantidad) setActivo(a => e.key === 'ArrowDown'
              ? (a < 0 ? 0 : (a + 1) % cantidad)
              : (a < 0 ? cantidad - 1 : (a - 1 + cantidad) % cantidad))
          }
          if (e.key === 'Enter' && abierto) { e.preventDefault(); const p = resultados?.items[activo]; if (p) seleccionar(p) }
        }} />
    </Campo>
    {abierto && <div className="mt-2 rounded-card border border-line bg-surface p-3 shadow-card">
      {cargando ? <p role="status">Buscando proveedores…</p> : fallo ? <p role="alert">{fallo}</p> : resultados?.items.length === 0 ? <p role="status">No hay proveedores que coincidan.</p> : <>
        <p role="status">{resultados?.total ?? 0} coincidencias.</p>
        <ul id={`${id}-lista`} role="listbox" aria-label="Proveedores" className="m-0 list-none p-0">
          {resultados?.items.map((p, i) => <li key={p.id} id={`${id}-${i}`} role="option" aria-selected={activo === i}
            className={`cursor-pointer rounded-field px-3 py-2 text-ink ${activo === i ? 'bg-brand-soft' : ''}`}
            onMouseDown={e => e.preventDefault()} onClick={() => seleccionar(p)}>{p.razonSocial}{p.nombreComercial ? ` · ${p.nombreComercial}` : ''} · {formatearCuit(p.cuit)}</li>)}
        </ul>
        {resultados && pagina * 20 < resultados.total && <Boton variante="secundario" onClick={() => { setResultados(null); setPagina(p => p + 1) }}>Siguientes resultados</Boton>}
        {pagina > 1 && <Boton variante="terciario" onClick={() => setPagina(p => p - 1)}>Resultados anteriores</Boton>}
      </>}
    </div>}
  </div>
}
