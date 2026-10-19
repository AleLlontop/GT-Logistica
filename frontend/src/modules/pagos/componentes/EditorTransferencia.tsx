import { useState, type FormEvent } from 'react'
import { Boton } from '../../../compartido/ui/Boton'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { Dialogo, DialogoAcciones } from '../../../compartido/ui/Dialogo'
import { InputImporte } from '../../../compartido/ui/InputImporte'
import { formatearParaCampo } from '../../../compartido/moneda'
import { importeCanonico } from '../servicios/importesPagos'
import { trimExterior } from '../servicios/normalizacionPagos'
import type { TransferenciaEntrada } from '../servicios/tipos'
import type { PropsEditor } from './tiposEditor'

export function EditorTransferencia({ inicial, hoy, onGuardar, onCancelar, erroresServidor = {} }: PropsEditor<TransferenciaEntrada>) {
  const [renglonId] = useState(() => inicial?.renglonId ?? crypto.randomUUID())
  const [fecha, setFecha] = useState(inicial?.fecha ?? hoy)
  const [cbuOrigen, setCbuOrigen] = useState(inicial?.cbuOrigen ?? '')
  const [cbuDestino, setCbuDestino] = useState(inicial?.cbuDestino ?? '')
  const [comprobante, setComprobante] = useState(inicial?.numeroComprobante ?? '')
  const [observaciones, setObservaciones] = useState(inicial?.observaciones ?? '')
  const [importe, setImporte] = useState(inicial ? formatearParaCampo(inicial.importe) : '')
  const [errores, setErrores] = useState(erroresServidor)
  function validar() {
    const e: Record<string, string> = {}
    if (!fecha || fecha > hoy) e.fecha = 'La fecha es obligatoria y no puede ser futura.'
    if (!/^[0-9]{22}$/.test(cbuOrigen)) e.cbuOrigen = 'Ingresá exactamente 22 dígitos.'
    if (!/^[0-9]{22}$/.test(cbuDestino)) e.cbuDestino = 'Ingresá exactamente 22 dígitos.'
    if (!trimExterior(comprobante)) e.numeroComprobante = 'Ingresá el número de comprobante.'
    if (!importeCanonico(importe)) e.importe = 'Ingresá un importe positivo con hasta dos decimales dentro de capacidad.'
    return e
  }
  function cambiar(campo: string) { setErrores(e => ({ ...e, [campo]: '' })) }
  function alSalir(campo: string) { setErrores(e => ({ ...e, [campo]: validar()[campo] ?? '' })) }
  function enviar(evento: FormEvent) {
    evento.preventDefault(); evento.stopPropagation()
    const e = validar(); setErrores(e)
    if (Object.keys(e).length) return
    onGuardar({ tipo: 'transferencia', renglonId, fecha, cbuOrigen, cbuDestino, numeroComprobante: comprobante,
      observaciones: observaciones || null, importe: importeCanonico(importe)! })
  }
  const acceso = (campo: string) => ({ 'aria-invalid': !!errores[campo], 'aria-describedby': errores[campo] ? `transferencia-${campo}-error` : undefined })
  return <Dialogo titulo={inicial ? 'Editar transferencia' : 'Agregar transferencia'} onCerrar={onCancelar}>
    <form noValidate onSubmit={enviar} className="mt-4 flex flex-col gap-4">
      <Campo id="transferencia-fecha" etiqueta="Fecha" obligatorio error={errores.fecha}>
        <input id="transferencia-fecha" type="date" required max={hoy} value={fecha} className={clasesDeControl(!!errores.fecha)} {...acceso('fecha')}
          onBlur={() => alSalir('fecha')} onChange={e => { setFecha(e.target.value); cambiar('fecha') }} />
      </Campo>
      <Campo id="transferencia-cbuOrigen" etiqueta="CBU origen" obligatorio error={errores.cbuOrigen} ancho="completo">
        <input id="transferencia-cbuOrigen" type="text" inputMode="numeric" required value={cbuOrigen} className={clasesDeControl(!!errores.cbuOrigen)} {...acceso('cbuOrigen')}
          onBlur={() => alSalir('cbuOrigen')} onChange={e => { setCbuOrigen(e.target.value); cambiar('cbuOrigen') }} />
      </Campo>
      <Campo id="transferencia-cbuDestino" etiqueta="CBU destino" obligatorio error={errores.cbuDestino} ancho="completo">
        <input id="transferencia-cbuDestino" type="text" inputMode="numeric" required value={cbuDestino} className={clasesDeControl(!!errores.cbuDestino)} {...acceso('cbuDestino')}
          onBlur={() => alSalir('cbuDestino')} onChange={e => { setCbuDestino(e.target.value); cambiar('cbuDestino') }} />
      </Campo>
      <Campo id="transferencia-numeroComprobante" etiqueta="Número de comprobante" obligatorio error={errores.numeroComprobante} ancho="completo">
        <input id="transferencia-numeroComprobante" required value={comprobante} className={clasesDeControl(!!errores.numeroComprobante)} {...acceso('numeroComprobante')}
          onBlur={() => alSalir('numeroComprobante')} onChange={e => { setComprobante(e.target.value); cambiar('numeroComprobante') }} />
      </Campo>
      <Campo id="transferencia-importe" etiqueta="Importe" obligatorio error={errores.importe}>
        <InputImporte id="transferencia-importe" required value={importe} className={clasesDeControl(!!errores.importe)} {...acceso('importe')}
          onBlur={() => alSalir('importe')} onChange={v => { setImporte(v); cambiar('importe') }} />
      </Campo>
      <Campo id="transferencia-observaciones" etiqueta="Observaciones" ancho="completo">
        <textarea id="transferencia-observaciones" value={observaciones} className={clasesDeControl()} onChange={e => setObservaciones(e.target.value)} />
      </Campo>
      <DialogoAcciones><Boton variante="secundario" onClick={onCancelar}>Cancelar</Boton><Boton variante="primario" type="submit">{inicial ? 'Guardar medio' : 'Agregar medio'}</Boton></DialogoAcciones>
    </form>
  </Dialogo>
}
