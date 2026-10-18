import { useEffect, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { Boton } from '../../../compartido/ui/Boton'
import { clasesDeBoton } from '../../../compartido/ui/clases'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { FichaEncabezado, FichaCuerpo, FichaSeccion } from '../../../compartido/ui/Ficha'
import { AsideDeFicha } from '../../../compartido/ui/AsideDeFicha'
import { Estado } from '../../../compartido/ui/Estado'
import { DialogoConfirmacion } from '../../../compartido/ui/DialogoConfirmacion'
import { formatearCuit } from '../../../compartido/cuit'
import { obtenerProveedor, cambiarEstadoProveedor, mensajeProveedor } from '../servicios/proveedores'
import type { ProveedorDetalle } from '../servicios/tipos'

export function DetalleProveedor() {
  const { id } = useParams(); const ubicacion = useLocation()
  const [proveedor, setProveedor] = useState<ProveedorDetalle | null>(null)
  const [error, setError] = useState(''); const [cargando, setCargando] = useState(true)
  const [baja, setBaja] = useState(false); const [ocupado, setOcupado] = useState(false)
  const [mensaje, setMensaje] = useState<string>(ubicacion.state?.mensaje ?? '')
  useEffect(() => {
    let vigente = true; setCargando(true); setError('')
    obtenerProveedor(Number(id)).then(p => { if (vigente) setProveedor(p) })
      .catch(e => { if (vigente) setError(mensajeProveedor(e)) })
      .finally(() => { if (vigente) setCargando(false) })
    return () => { vigente = false }
  }, [id])
  async function cambiar(activo: boolean) {
    if (!proveedor || ocupado) return
    setOcupado(true); setBaja(false); setError('')
    try { setProveedor(await cambiarEstadoProveedor(proveedor.id, proveedor.version, activo)); setMensaje(activo ? 'Proveedor reactivado.' : 'Proveedor dado de baja.') }
    catch (e) { setError(mensajeProveedor(e)) } finally { setOcupado(false) }
  }
  return <>
    <EncabezadoDePantalla titulo="Detalle de proveedor" volverA={{ ruta: '/proveedores', etiqueta: 'Volver a proveedores' }} />
    {error && <p role="alert">{error}</p>}{mensaje && <p role="status">{mensaje}</p>}
    {cargando ? <p role="status">Cargando proveedor…</p> : proveedor && <>
      <FichaEncabezado identidad={proveedor.razonSocial} estado={<Estado forma="pastilla" valor={proveedor.estado} texto={proveedor.estado === 'activo' ? 'Activo' : 'Inactivo'} />} acciones={<><Link className={clasesDeBoton('secundario')} to={`/proveedores/${proveedor.id}/editar`}>Editar</Link>{proveedor.estado === 'activo' ? <Boton variante="destructivo" disabled={ocupado} onClick={() => setBaja(true)}>Dar de baja</Boton> : <Boton variante="secundario" disabled={ocupado} onClick={() => void cambiar(true)}>Reactivar</Boton>}</>} />
      <FichaCuerpo aside={<AsideDeFicha destacado={<><p className="text-faint">CUIT</p><p className="font-mono text-ink">{formatearCuit(proveedor.cuit)}</p></>} />}>
        <FichaSeccion id="identificacion" titulo="Identificación"><dl><dt>Tipo</dt><dd>{proveedor.tipo === 'fletero' ? 'Fletero' : 'Proveedor normal'}</dd><dt>Nombre comercial</dt><dd>{proveedor.nombreComercial ?? 'Sin informar'}</dd></dl></FichaSeccion>
        <FichaSeccion id="contacto" titulo="Contacto"><dl><dt>Teléfono</dt><dd>{proveedor.telefono ?? 'Sin informar'}</dd><dt>Email</dt><dd>{proveedor.email ?? 'Sin informar'}</dd><dt>Dirección</dt><dd>{proveedor.direccion ?? 'Sin informar'}</dd></dl>{proveedor.tipo === 'fletero' && <p className="px-5 pb-5 text-ink-soft">La razón social, el CUIT, el teléfono y el email se mantienen en Transportistas.</p>}</FichaSeccion>
      </FichaCuerpo>
    </>}
    {baja && proveedor && <DialogoConfirmacion titulo="Dar de baja proveedor" mensaje={`¿Querés dar de baja a ${proveedor.razonSocial}? Sus datos y facturas se conservan.`} etiquetaConfirmar="Confirmar baja" onCancelar={() => setBaja(false)} onConfirmar={() => void cambiar(false)} />}
  </>
}
