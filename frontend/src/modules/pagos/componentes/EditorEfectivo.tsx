import { useState, type FormEvent } from 'react'
import { Boton } from '../../../compartido/ui/Boton'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { Dialogo, DialogoAcciones } from '../../../compartido/ui/Dialogo'
import { InputImporte } from '../../../compartido/ui/InputImporte'
import { formatearParaCampo } from '../../../compartido/moneda'
import { formatearFecha } from '../../../compartido/fechas'
import { importeCanonico } from '../servicios/importesPagos'
import type { EfectivoEntrada } from '../servicios/tipos'
import type { PropsEditor } from './tiposEditor'

export function EditorEfectivo({ inicial, hoy, onGuardar, onCancelar, erroresServidor = {} }: PropsEditor<EfectivoEntrada>) {
  const [renglonId] = useState(() => inicial?.renglonId ?? crypto.randomUUID())
  const [importe, setImporte] = useState(inicial ? formatearParaCampo(inicial.importe) : '')
  const [error, setError] = useState(erroresServidor.importe ?? '')
  function validar() { return importeCanonico(importe) ? '' : 'Ingresá un importe positivo con hasta dos decimales dentro de capacidad.' }
  function enviar(evento: FormEvent) {
    evento.preventDefault(); evento.stopPropagation()
    const mensaje = validar(); setError(mensaje)
    if (mensaje) return
    onGuardar({ tipo: 'efectivo', renglonId, importe: importeCanonico(importe)! })
  }
  return <Dialogo titulo={inicial ? 'Editar efectivo' : 'Agregar efectivo'} onCerrar={onCancelar}>
    <form noValidate onSubmit={enviar} className="mt-4 flex flex-col gap-4">
      <p className="text-ink-soft">Fecha: <time dateTime={hoy}>{formatearFecha(hoy)}</time>. El efectivo se registra con el día argentino actual al Generar.</p>
      <Campo id="efectivo-importe" etiqueta="Importe" obligatorio error={error}>
        <InputImporte id="efectivo-importe" required value={importe} className={clasesDeControl(!!error)}
          aria-invalid={!!error} aria-describedby={error ? 'efectivo-importe-error' : undefined}
          onBlur={() => setError(validar())} onChange={v => { setImporte(v); setError('') }} />
      </Campo>
      <DialogoAcciones><Boton variante="secundario" onClick={onCancelar}>Cancelar</Boton><Boton variante="primario" type="submit">{inicial ? 'Guardar medio' : 'Agregar medio'}</Boton></DialogoAcciones>
    </form>
  </Dialogo>
}
