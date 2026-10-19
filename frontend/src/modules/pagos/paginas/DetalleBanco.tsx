import { useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Boton } from '../../../compartido/ui/Boton'
import { clasesDeBoton } from '../../../compartido/ui/clases'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { FichaEncabezado } from '../../../compartido/ui/Ficha'
import { Dialogo, DialogoAcciones } from '../../../compartido/ui/Dialogo'
import { useResultadoPago } from '../componentes/resultadoPago'
import { activarBanco, mensajePago, obtenerBanco } from '../servicios/pagos'
import type { Banco } from '../servicios/tipos'

export function DetalleBanco({ puedeGestionar = false }: { puedeGestionar?: boolean }) {
  const id = Number(useParams().id)
  const { banco: resultado, guardarBanco } = useResultadoPago()
  const [banco, setBanco] = useState<Banco | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')
  const [ocupado, setOcupado] = useState(false)
  const [desactivar, setDesactivar] = useState(false)
  const [mensaje, setMensaje] = useState('')
  const accionEstado = useRef<HTMLButtonElement>(null)
  function cerrarDialogo() {
    setDesactivar(false)
    // El disparador está fuera del Root de Radix; restaurar foco tras desmontar el modal.
    window.requestAnimationFrame(() => accionEstado.current?.focus())
  }
  useEffect(() => {
    let vigente = true
    setCargando(true); setError('')
    const carga = resultado?.id === id ? Promise.resolve(resultado) : obtenerBanco(id)
    carga.then(b => { if (vigente) setBanco(b) }).catch(e => { if (vigente) setError(mensajePago(e)) })
      .finally(() => { if (vigente) setCargando(false) })
    return () => { vigente = false }
  }, [id, resultado])
  async function cambiar(activo: boolean) {
    if (!banco || ocupado || !puedeGestionar) return
    setOcupado(true); setDesactivar(false); setError('')
    try {
      const actualizado = await activarBanco(banco.id, activo)
      guardarBanco(actualizado); setBanco(actualizado); setMensaje(activo ? 'Banco activado.' : 'Banco desactivado.')
    } catch (e) { setError(mensajePago(e)) } finally { setOcupado(false); window.requestAnimationFrame(() => accionEstado.current?.focus()) }
  }
  return <>
    <EncabezadoDePantalla titulo="Detalle de banco" volverA={{ ruta: '/pagos/bancos', etiqueta: 'Volver a bancos' }} />
    {error && <p role="alert">{error}</p>}{mensaje && <p role="status">{mensaje}</p>}
    {cargando ? <p role="status">Cargando banco…</p> : banco && <FichaEncabezado identidad={banco.nombre}
      estado={<span>{banco.activo ? 'Activo' : 'Inactivo'}</span>}
      acciones={puedeGestionar && <>
        <Link className={clasesDeBoton('secundario')} to={`/pagos/bancos/${banco.id}/editar`}>Editar</Link>
        {banco.activo ? <Boton ref={accionEstado} variante="destructivo" disabled={ocupado} onClick={() => setDesactivar(true)}>Desactivar</Boton>
          : <Boton ref={accionEstado} variante="secundario" disabled={ocupado} onClick={() => void cambiar(true)}>Activar</Boton>}
      </>} />}
    {desactivar && banco && <Dialogo titulo="Desactivar banco" onCerrar={cerrarDialogo}>
      <p className="mt-3 text-ink-soft">¿Querés desactivar a {banco.nombre}? Sus cheques e historia se conservan y podés volver a activarlo.</p>
      <DialogoAcciones><Boton variante="secundario" onClick={cerrarDialogo}>Cancelar</Boton>
        <Boton variante="destructivo" onClick={() => void cambiar(false)}>Confirmar desactivación</Boton></DialogoAcciones>
    </Dialogo>}
  </>
}
