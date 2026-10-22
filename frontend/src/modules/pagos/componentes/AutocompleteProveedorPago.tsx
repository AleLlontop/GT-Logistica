import { useEffect, useId, useState } from 'react'
import { Boton } from '../../../compartido/ui/Boton'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { formatearCuit } from '../../../compartido/cuit'
import { mensajePago, proveedoresElegibles, proveedoresHistoricos } from '../servicios/pagos'
import type { Pagina, ProveedorOpcion } from '../servicios/tipos'

/** Mismo comportamiento de 013, con referencia mínima propia de GP_01 e Inactivo informativo. */
export function AutocompleteProveedorPago({ onSeleccionar, disabled = false, error, historico = false }: {
  onSeleccionar: (proveedor: ProveedorOpcion | null) => void; disabled?: boolean; error?: string; historico?: boolean
}) {
  const id = useId()
  const [texto, setTexto] = useState(''); const [consulta, setConsulta] = useState('')
  const [pagina, setPagina] = useState(1); const [resultado, setResultado] = useState<Pagina<ProveedorOpcion> | null>(null)
  const [abierto, setAbierto] = useState(false); const [activo, setActivo] = useState(-1)
  const [cargando, setCargando] = useState(false); const [fallo, setFallo] = useState('')
  useEffect(() => {
    if (!abierto || disabled) return
    let vigente = true
    setCargando(true); setFallo(''); setResultado(null); setActivo(-1)
    const timer = setTimeout(() => {
      (historico ? proveedoresHistoricos : proveedoresElegibles)(consulta, pagina).then(r => { if (vigente) setResultado(r) })
        .catch(e => { if (vigente) setFallo(mensajePago(e)) }).finally(() => { if (vigente) setCargando(false) })
    }, 250)
    return () => { vigente = false; clearTimeout(timer) }
  }, [consulta, pagina, abierto, disabled, historico])
  function seleccionar(p: ProveedorOpcion) { setTexto(`${p.razonSocial}${p.activo ? '' : ' — Inactivo'}`); setAbierto(false); onSeleccionar(p) }
  return <div onBlur={e => { if (!e.currentTarget.contains(e.relatedTarget)) setAbierto(false) }}>
    <Campo id={id} etiqueta="Proveedor" obligatorio={!historico} error={error} ancho="largo">
      <input id={id} required={!historico} role="combobox" autoComplete="off" disabled={disabled} value={texto} className={clasesDeControl(!!error)}
        aria-expanded={abierto} aria-controls={`${id}-lista`} aria-autocomplete="list" aria-activedescendant={abierto && activo >= 0 ? `${id}-${activo}` : undefined}
        aria-invalid={!!error} aria-describedby={error ? `${id}-error` : undefined}
        onChange={e => { setTexto(e.target.value); setConsulta(e.target.value); setPagina(1); setResultado(null); setAbierto(true); onSeleccionar(null) }}
        onKeyDown={e => {
          if (e.key === 'Escape' || e.key === 'Tab') { setAbierto(false); return }
          if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
            e.preventDefault(); setAbierto(true)
            const cantidad = resultado?.items.length ?? 0
            if (cantidad) setActivo(a => e.key === 'ArrowDown' ? (a < 0 ? 0 : (a + 1) % cantidad) : (a < 0 ? cantidad - 1 : (a - 1 + cantidad) % cantidad))
          }
          if (e.key === 'Enter' && abierto) { e.preventDefault(); const p = resultado?.items[activo]; if (p) seleccionar(p) }
        }} />
    </Campo>
    {abierto && <div className="mt-2 rounded-card border border-line bg-surface p-3 shadow-card">
      {cargando ? <p role="status">Buscando proveedores…</p> : fallo ? <p role="alert">{fallo}</p> : <>
        {resultado?.items.length === 0 && <p role="status">No hay proveedores que coincidan.</p>}
        <ul id={`${id}-lista`} role="listbox" aria-label="Proveedores" className="m-0 list-none p-0">
          {resultado?.items.map((p, i) => <li id={`${id}-${i}`} key={p.id} role="option" aria-selected={activo === i}
            className={`cursor-pointer rounded-field px-3 py-2 text-ink ${activo === i ? 'bg-brand-soft' : ''}`}
            onMouseDown={e => e.preventDefault()} onClick={() => seleccionar(p)}>{p.razonSocial} · {formatearCuit(p.cuit)}{!p.activo && ' — Inactivo'}</li>)}
        </ul>
        {resultado && pagina * 20 < resultado.total && <Boton variante="secundario" onClick={() => setPagina(p => p + 1)}>Siguientes resultados</Boton>}
        {pagina > 1 && <Boton variante="terciario" onClick={() => setPagina(p => p - 1)}>Resultados anteriores</Boton>}
      </>}
    </div>}
  </div>
}
