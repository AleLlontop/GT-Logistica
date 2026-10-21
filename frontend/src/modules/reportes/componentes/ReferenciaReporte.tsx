import { useId, useRef, useState } from 'react'
import { Campo } from '../../../compartido/ui/Campo'
import { Boton } from '../../../compartido/ui/Boton'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { consultarReferencias, type TipoReferenciaAnalisis } from '../servicios/servicioAnalisis'
import { useSolicitudAnalisis } from '../servicios/useSolicitudAnalisis'
import { EstadoConsultaAnalisis } from './EstadoConsultaAnalisis'
/** Autocomplete mínimo propio del reporte: nunca consulta un padrón de gestión. */
export function ReferenciaReporte({ etiqueta, tipo, sesionClave, onCambiar, seleccionId }: { etiqueta: string; tipo: TipoReferenciaAnalisis; sesionClave: string | null; onCambiar: (id: number | undefined) => void; seleccionId?: number }) {
  const id = useId(), [texto, setTexto] = useState(seleccionId === undefined ? '' : `Referencia ${seleccionId}`), [pagina, setPagina] = useState(1), [abierto, setAbierto] = useState(false), [activo, setActivo] = useState(-1)
  const control = useRef<HTMLInputElement>(null)
  const [error, setError] = useState<string | null>(null)
  const solicitud = useSolicitudAnalisis(abierto ? `${tipo}:${texto}:${pagina}` : null, sesionClave, () => consultarReferencias(tipo, texto, pagina))
  const items = solicitud.estado === 'listo' ? solicitud.datos.items : []
  function seleccionar(indice: number) { const item = items[indice]; if (!item) return; setTexto(item.nombre); control.current?.setCustomValidity(''); setError(null); onCambiar(item.id); setAbierto(false); setActivo(-1) }
  return <div className="min-w-0 max-w-full" onBlur={e => { if (!e.currentTarget.contains(e.relatedTarget)) { setAbierto(false); if (control.current?.validationMessage) setError(control.current.validationMessage) } }}>
    <Campo id={id} etiqueta={etiqueta} error={error} ancho="medio" ayuda="Seleccioná una referencia o dejá vacío para todas.">
      <input ref={control} id={id} aria-invalid={Boolean(error)} aria-describedby={`${id}-ayuda${error ? ` ${id}-error` : ''}`} role="combobox" autoComplete="off" className={clasesDeControl(Boolean(error))} value={texto} aria-expanded={abierto} aria-controls={`${id}-lista`} aria-autocomplete="list" aria-activedescendant={abierto && activo >= 0 ? `${id}-${activo}` : undefined}
        onChange={e => { setTexto(e.target.value); e.target.setCustomValidity(e.target.value ? 'Seleccioná una referencia de la lista o dejá el campo vacío.' : ''); setError(null); setPagina(1); setActivo(-1); setAbierto(true); onCambiar(undefined) }}
        onKeyDown={e => {
          if (e.key === 'Escape' || e.key === 'Tab') { setAbierto(false); return }
          if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); setAbierto(true); if (items.length) setActivo(a => e.key === 'ArrowDown' ? (a + 1) % items.length : (a - 1 + items.length) % items.length) }
          if (e.key === 'Enter' && abierto) { e.preventDefault(); seleccionar(activo) }
        }} />
    </Campo>
    {abierto && <div className="mt-2 rounded-card border border-line bg-surface p-3 shadow-card">
      {solicitud.estado !== 'listo' ? solicitud.estado !== 'inactivo' && <EstadoConsultaAnalisis solicitud={solicitud} /> : <>
        <ul id={`${id}-lista`} role="listbox" aria-label={etiqueta} className="m-0 list-none p-0">{items.map((item, i) => <li key={item.id} id={`${id}-${i}`} role="option" aria-selected={activo === i} className={`cursor-pointer rounded-field p-2 text-ink ${activo === i ? 'bg-brand-soft' : ''}`} onMouseDown={e => e.preventDefault()} onClick={() => seleccionar(i)}>{item.nombre}{item.activo === false && ' — Inactivo'}</li>)}</ul>
        {!items.length && <p role="status">Sin coincidencias.</p>}
        {pagina > 1 && <Boton variante="secundario" onClick={() => { setPagina(p => p - 1); setActivo(-1) }}>Resultados anteriores de {etiqueta}</Boton>}
        {pagina * solicitud.datos.tamanioPagina < solicitud.datos.total && <Boton variante="secundario" onClick={() => { setPagina(p => p + 1); setActivo(-1) }}>Siguientes resultados de {etiqueta}</Boton>}
      </>}
    </div>}
  </div>
}
