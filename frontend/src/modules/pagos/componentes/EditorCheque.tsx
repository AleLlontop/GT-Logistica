import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Boton } from '../../../compartido/ui/Boton'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { Dialogo, DialogoAcciones } from '../../../compartido/ui/Dialogo'
import { InputImporte } from '../../../compartido/ui/InputImporte'
import { formatearParaCampo } from '../../../compartido/moneda'
import { importeCanonico } from '../servicios/importesPagos'
import { trimExterior } from '../servicios/normalizacionPagos'
import type { BancoReferencia, ChequeEntrada, TipoProveedor } from '../servicios/tipos'
import type { PropsEditor } from './tiposEditor'

interface Props extends PropsEditor<ChequeEntrada> {
  bancos: BancoReferencia[]
  tipoProveedor: TipoProveedor
  puedeGestionarBancos?: boolean
}
export function EditorCheque({ inicial, hoy, bancos, tipoProveedor, onGuardar, onCancelar, erroresServidor = {}, puedeGestionarBancos = false }: Props) {
  const [renglonId] = useState(() => inicial?.renglonId ?? crypto.randomUUID())
  const [bancoId, setBancoId] = useState(inicial?.bancoId ?? 0)
  const [numero, setNumero] = useState(inicial?.numeroCheque ?? '')
  const [emision, setEmision] = useState(inicial?.fechaEmision ?? hoy)
  const [vencimiento, setVencimiento] = useState(inicial?.fechaVencimiento ?? '')
  const [importe, setImporte] = useState(inicial ? formatearParaCampo(inicial.importe) : '')
  const [detalle, setDetalle] = useState(inicial?.detalle ?? '')
  const [errores, setErrores] = useState(erroresServidor)
  const activos = bancos.filter(b => b.activo)
  function validar() {
    const e: Record<string, string> = {}
    if (!activos.some(b => b.id === bancoId)) e.bancoId = 'Seleccioná un banco Activo.'
    const normalizado = trimExterior(numero)
    if (!normalizado) e.numeroCheque = 'Ingresá el número de cheque.'
    else if (normalizado.length > 200) e.numeroCheque = 'No puede superar 200 caracteres después de quitar los espacios exteriores'
    if (!emision || emision > hoy) e.fechaEmision = 'La emisión es obligatoria y no puede ser futura.'
    if (!vencimiento || (tipoProveedor === 'fletero' ? vencimiento <= emision : vencimiento < emision))
      e.fechaVencimiento = tipoProveedor === 'fletero' ? 'El cheque del Fletero debe ser diferido.' : 'El vencimiento no puede ser anterior a la emisión.'
    if (!importeCanonico(importe)) e.importe = 'Ingresá un importe positivo con hasta dos decimales dentro de capacidad.'
    return e
  }
  function cambiar(campo: string) { setErrores(e => ({ ...e, [campo]: '' })) }
  function alSalir(campo: string) { setErrores(e => ({ ...e, [campo]: validar()[campo] ?? '' })) }
  function enviar(evento: FormEvent) {
    evento.preventDefault(); evento.stopPropagation()
    const e = validar(); setErrores(e)
    if (Object.keys(e).length) return
    onGuardar({ tipo: 'cheque', renglonId, bancoId, numeroCheque: trimExterior(numero), fechaEmision: emision,
      fechaVencimiento: vencimiento, importe: importeCanonico(importe)!, detalle: detalle || null })
  }
  const acceso = (campo: string) => ({ 'aria-invalid': !!errores[campo], 'aria-describedby': errores[campo] ? `cheque-${campo}-error` : undefined })
  return <Dialogo titulo={inicial ? 'Editar cheque' : 'Agregar cheque'} onCerrar={onCancelar}>
    <form noValidate onSubmit={enviar} className="mt-4 flex flex-col gap-4">
      <Campo id="cheque-bancoId" etiqueta="Banco" obligatorio error={errores.bancoId} ancho="completo">
        <select id="cheque-bancoId" required value={bancoId || ''} className={clasesDeControl(!!errores.bancoId)} {...acceso('bancoId')}
          onBlur={() => alSalir('bancoId')} onChange={e => { setBancoId(Number(e.target.value)); cambiar('bancoId') }}>
          <option value="">Elegí un banco</option>{activos.map(b => <option key={b.id} value={b.id}>{b.nombre}</option>)}
        </select>
      </Campo>
      {activos.length === 0 && <p role="status">No hay bancos Activos disponibles.{puedeGestionarBancos && <> <Link to="/pagos/bancos/nuevo">Crear banco</Link></>}</p>}
      <Campo id="cheque-numeroCheque" etiqueta="Número de cheque" obligatorio error={errores.numeroCheque} ancho="completo">
        <input id="cheque-numeroCheque" required value={numero} className={clasesDeControl(!!errores.numeroCheque)} {...acceso('numeroCheque')}
          onBlur={() => alSalir('numeroCheque')} onChange={e => { setNumero(e.target.value); cambiar('numeroCheque') }} />
      </Campo>
      <Campo id="cheque-fechaEmision" etiqueta="Emisión" obligatorio error={errores.fechaEmision}>
        <input id="cheque-fechaEmision" type="date" required max={hoy} value={emision} className={clasesDeControl(!!errores.fechaEmision)} {...acceso('fechaEmision')}
          onBlur={() => alSalir('fechaEmision')} onChange={e => { setEmision(e.target.value); cambiar('fechaEmision') }} />
      </Campo>
      <Campo id="cheque-fechaVencimiento" etiqueta="Vencimiento" obligatorio error={errores.fechaVencimiento}>
        <input id="cheque-fechaVencimiento" type="date" required value={vencimiento} className={clasesDeControl(!!errores.fechaVencimiento)} {...acceso('fechaVencimiento')}
          onBlur={() => alSalir('fechaVencimiento')} onChange={e => { setVencimiento(e.target.value); cambiar('fechaVencimiento') }} />
      </Campo>
      <Campo id="cheque-importe" etiqueta="Importe" obligatorio error={errores.importe}>
        <InputImporte id="cheque-importe" required value={importe} className={clasesDeControl(!!errores.importe)} {...acceso('importe')}
          onBlur={() => alSalir('importe')} onChange={v => { setImporte(v); cambiar('importe') }} />
      </Campo>
      <Campo id="cheque-detalle" etiqueta="Detalle de cheque" ancho="completo">
        <textarea id="cheque-detalle" value={detalle} className={clasesDeControl()} onChange={e => setDetalle(e.target.value)} />
      </Campo>
      <DialogoAcciones><Boton variante="secundario" onClick={onCancelar}>Cancelar</Boton><Boton variante="primario" type="submit">{inicial ? 'Guardar medio' : 'Agregar medio'}</Boton></DialogoAcciones>
    </form>
  </Dialogo>
}
