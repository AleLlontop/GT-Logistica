import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { clasesDeBoton } from '../../../compartido/ui/clases'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { FilaNavegable } from '../../../compartido/ui/FilaNavegable'
import { EnlaceDeFila } from '../../../compartido/ui/EnlaceDeFila'
import { Paginacion } from '../../../compartido/ui/Paginacion'
import { listarBancos, mensajePago } from '../servicios/pagos'
import type { Banco, Pagina } from '../servicios/tipos'

export function Bancos({ puedeGestionar = false }: { puedeGestionar?: boolean }) {
  const [pagina, setPagina] = useState(1)
  const [resultado, setResultado] = useState<Pagina<Banco> | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')
  useEffect(() => {
    let vigente = true
    setCargando(true); setError('')
    listarBancos(pagina).then(r => { if (vigente) setResultado(r) })
      .catch(e => { if (vigente) setError(mensajePago(e)) })
      .finally(() => { if (vigente) setCargando(false) })
    return () => { vigente = false }
  }, [pagina])
  return <>
    <EncabezadoDePantalla titulo="Bancos" accionPrincipal={puedeGestionar && <Link className={clasesDeBoton('primario')} to="/pagos/bancos/nuevo">Nuevo banco</Link>} />
    <Listado>
      {cargando ? <p role="status" className="p-5">Cargando bancos…</p> : error ? <p role="alert" className="p-5">{error}</p>
        : resultado?.items.length === 0 ? <p role="status" className="p-5">No hay bancos registrados.</p>
        : resultado && <TablaDesplazable><table><caption>Bancos registrados</caption><thead><tr><th scope="col">Nombre</th><th scope="col">Estado</th></tr></thead>
          <tbody>{resultado.items.map(b => <FilaNavegable key={b.id} a={`/pagos/bancos/${b.id}`}>
            <td><EnlaceDeFila a={`/pagos/bancos/${b.id}`}>{b.nombre}</EnlaceDeFila></td><td>{b.activo ? 'Activo' : 'Inactivo'}</td>
          </FilaNavegable>)}</tbody></table></TablaDesplazable>}
      {!cargando && !error && resultado && <Paginacion {...resultado} nombrePlural="bancos" onCambiarPagina={setPagina} />}
    </Listado>
  </>
}
