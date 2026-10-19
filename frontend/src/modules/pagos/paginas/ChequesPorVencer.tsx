import { useEffect, useState, type FormEvent } from 'react'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { Boton } from '../../../compartido/ui/Boton'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { FilaNavegable } from '../../../compartido/ui/FilaNavegable'
import { EnlaceDeFila } from '../../../compartido/ui/EnlaceDeFila'
import { Paginacion } from '../../../compartido/ui/Paginacion'
import { formatearFecha } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { bancosHistoricos, consultarCheques, mensajePago } from '../servicios/pagos'
import { nombresEstadoCheque, nombresSituacionCheque } from '../servicios/estadosCheque'
import type { BancoReferencia, ChequeResumen, EstadoCheque, FiltrosCheques, Pagina } from '../servicios/tipos'

export function ChequesPorVencer() {
  const [bancos, setBancos] = useState<BancoReferencia[]>([]); const [errorBancos, setErrorBancos] = useState('')
  const [bancoId, setBancoId] = useState(''); const [estado, setEstado] = useState<EstadoCheque | ''>('')
  const [desde, setDesde] = useState(''); const [hasta, setHasta] = useState(''); const [rango, setRango] = useState('')
  const [filtros, setFiltros] = useState<FiltrosCheques>({ pagina: 1 })
  const [resultado, setResultado] = useState<Pagina<ChequeResumen> | null>(null)
  const [cargando, setCargando] = useState(true); const [error, setError] = useState('')
  useEffect(() => {
    let vigente = true
    bancosHistoricos().then(r => { if (vigente) setBancos(r.items) }).catch(e => { if (vigente) setErrorBancos(mensajePago(e)) })
    return () => { vigente = false }
  }, [])
  useEffect(() => {
    let vigente = true; setCargando(true); setError('')
    consultarCheques(filtros).then(r => { if (vigente) setResultado(r) }).catch(e => { if (vigente) setError(mensajePago(e)) })
      .finally(() => { if (vigente) setCargando(false) })
    return () => { vigente = false }
  }, [filtros])
  function filtrar(e: FormEvent) {
    e.preventDefault(); if (desde && hasta && desde > hasta) { setRango('Desde no puede ser posterior a Hasta.'); return }
    setRango(''); setFiltros({ bancoId: bancoId ? Number(bancoId) : undefined, estado, desdeVencimiento: desde, hastaVencimiento: hasta, pagina: 1 })
  }
  function limpiar() { setBancoId(''); setEstado(''); setDesde(''); setHasta(''); setRango(''); setFiltros({ pagina: 1 }) }
  return <>
    <EncabezadoDePantalla titulo="Cheques por vencer" />
    <p>Incluye cheques vencidos y con vencimiento entre hoy y los próximos 30 días, inclusive, según la fecha argentina. Los filtros restringen este período.</p>
    <form onSubmit={filtrar} className="my-5 space-y-4">
      <Campo id="cheques-banco" etiqueta="Banco"><select id="cheques-banco" value={bancoId} onChange={e => setBancoId(e.target.value)} className={clasesDeControl()}><option value="">Todos</option>{bancos.map(b => <option key={b.id} value={b.id}>{b.nombre}{!b.activo && ' — Inactivo'}</option>)}</select></Campo>
      {errorBancos && <p role="alert">{errorBancos}</p>}
      <Campo id="cheques-estado" etiqueta="Estado"><select id="cheques-estado" value={estado} onChange={e => setEstado(e.target.value as EstadoCheque | '')} className={clasesDeControl()}><option value="">Todos</option>{Object.entries(nombresEstadoCheque).map(([e, nombre]) => <option key={e} value={e}>{nombre}</option>)}</select></Campo>
      <Campo id="cheques-desde" etiqueta="Vencimiento desde" error={rango}><input id="cheques-desde" type="date" value={desde} onChange={e => setDesde(e.target.value)} className={clasesDeControl(!!rango)} aria-invalid={!!rango} aria-describedby={rango ? 'cheques-desde-error' : undefined} /></Campo>
      <Campo id="cheques-hasta" etiqueta="Vencimiento hasta"><input id="cheques-hasta" type="date" value={hasta} onChange={e => setHasta(e.target.value)} className={clasesDeControl()} /></Campo>
      <Boton type="submit" variante="secundario">Filtrar</Boton> <Boton variante="terciario" onClick={limpiar}>Limpiar filtros</Boton>
    </form>
    <Listado>{cargando ? <p role="status" className="p-5">Cargando cheques…</p> : error ? <p role="alert" className="p-5">{error}</p> : resultado?.items.length === 0 ? <p role="status" className="p-5">No hay cheques que coincidan con los filtros.</p> : resultado && <TablaDesplazable><table><caption>Cheques vencidos y próximos a vencer</caption><thead><tr><th scope="col">Número</th><th scope="col">Banco</th><th scope="col">Vencimiento</th><th scope="col">Importe</th><th scope="col">Estado</th><th scope="col">Situación</th></tr></thead><tbody>{resultado.items.map(c => <FilaNavegable key={c.id} a={`/pagos/cheques/${c.id}`}><td><EnlaceDeFila a={`/pagos/cheques/${c.id}`}><span className="whitespace-pre-wrap break-all">{c.numeroCheque}</span></EnlaceDeFila></td><td>{c.banco.nombre}</td><td>{formatearFecha(c.fechaVencimiento)}</td><td>{formatearPesos(c.importe)}</td><td>{nombresEstadoCheque[c.estado]}</td><td>{nombresSituacionCheque[c.situacionVencimiento]}</td></FilaNavegable>)}</tbody></table></TablaDesplazable>}
      {!cargando && !error && resultado && <Paginacion {...resultado} nombrePlural="cheques" onCambiarPagina={pagina => setFiltros(f => ({ ...f, pagina }))} />}
    </Listado>
  </>
}
