import { useState, type FormEvent } from 'react'
import { formatearFecha } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { BarraDeAcciones, LEYENDA_DE_OBLIGATORIOS } from '../../../compartido/ui/BarraDeAcciones'
import { Boton } from '../../../compartido/ui/Boton'
import { clasesDeAvisoDePantalla, clasesDeFormulario } from '../../../compartido/ui/clases'
import { cn } from '../../../compartido/ui/cn'
import { Dialogo } from '../../../compartido/ui/Dialogo'
import { IconoEnRegla } from '../../../compartido/ui/iconos'
import { InputImporte } from '../../../compartido/ui/InputImporte'
import {
  detalleDeError,
  type ConfirmacionDePago,
  type LiquidacionDetalle,
  type ResultadoDeOrdenDePago,
} from '../servicios/servicioLiquidaciones'

/** Un importe como se escribe en el campo: `155000,00`, sin separador de miles. */
const PARA_EL_CAMPO = new Intl.NumberFormat('es-AR', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
  useGrouping: true,
})

const MENSAJE_INESPERADO = 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.'

interface Props {
  numero: string
  transportista: string
  restaPagar: number
  /** `yyyy-MM-dd`: el límite inferior de la fecha de pago (FR-038). */
  fechaGeneracion: string
  /** `yyyy-MM-dd`: el límite superior, y la fecha propuesta. */
  hoy: string
  onEnviar: (orden: { fechaPago: string; importe: number; confirmado: boolean }) => Promise<ResultadoDeOrdenDePago>
  onRegistrada: (liquidacion: LiquidacionDetalle) => void
  onCancelar: () => void
}

/** `155000,50` → `155000.5`; `null` si no es un importe con hasta dos decimales. */
function leerImporte(texto: string): number | null {
  const sinPuntos = texto.replace(/\./g, '')
  const normalizado = sinPuntos.trim().replace(',', '.')

  return /^\d+(\.\d{1,2})?$/.test(normalizado) ? Number(normalizado) : null
}

/**
 * Registrar una orden de pago, en dos pasos (User Story 4, FR-036 a FR-040).
 *
 * **Paso 1, los datos**: fecha propuesta en hoy e importe propuesto en lo que resta, los dos
 * modificables (FR-036). **Paso 2, la confirmación**: los importes que se muestran son **los que devolvió
 * el servidor** en el `409`, calculados sobre el saldo actual y no sobre la ficha que se cargó hace diez
 * minutos (research §7). *Volver* regresa al paso 1 con los datos.
 */
export function DialogoOrdenDePago({
  numero,
  transportista,
  restaPagar,
  fechaGeneracion,
  hoy,
  onEnviar,
  onRegistrada,
  onCancelar,
}: Props) {
  const [fechaPago, setFechaPago] = useState(hoy)
  const [importeTexto, setImporteTexto] = useState(PARA_EL_CAMPO.format(restaPagar))
  const [tocados, setTocados] = useState<Set<'fechaPago' | 'importe'>>(new Set())
  const [intento, setIntento] = useState(false)

  const [confirmacion, setConfirmacion] = useState<ConfirmacionDePago | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const importe = leerImporte(importeTexto)

  const errorDeFecha =
    fechaPago === ''
      ? 'Elegí la fecha en que se pagó.'
      : fechaPago < fechaGeneracion || fechaPago > hoy
        ? `La fecha de pago tiene que estar entre el ${formatearFecha(fechaGeneracion)} y hoy.`
        : null

  const errorDeImporte =
    importe === null || importe <= 0
      ? 'Escribí un importe mayor que cero. Ejemplo: 120000,00'
      : importe > restaPagar
        ? `El importe supera lo que resta pagar: ${formatearPesos(restaPagar)}.`
        : null

  /** El error aparece después de tocar el campo o de intentar registrar, nunca al abrir. */
  const visible = (campo: 'fechaPago' | 'importe', mensaje: string | null) =>
    tocados.has(campo) || intento ? mensaje : null

  async function enviar(confirmado: boolean) {
    if (importe === null) return

    setEnviando(true)
    setError(null)

    try {
      const resultado = await onEnviar({ fechaPago, importe, confirmado })

      if (resultado.tipo === 'requiereConfirmacion') {
        setConfirmacion(resultado.confirmacion)
      } else {
        onRegistrada(resultado.liquidacion)
      }
    } catch (fallo) {
      // Un rechazo del servidor —otro pago se adelantó, la liquidación ya no es pagable— se muestra
      // adentro del diálogo, de vuelta en el paso de los datos.
      setConfirmacion(null)
      setError(detalleDeError(fallo)?.mensaje ?? MENSAJE_INESPERADO)
    } finally {
      setEnviando(false)
    }
  }

  function registrar(evento: FormEvent) {
    evento.preventDefault()
    setIntento(true)

    if (errorDeFecha !== null || errorDeImporte !== null) {
      return
    }

    void enviar(false)
  }

  function tocar(campo: 'fechaPago' | 'importe') {
    setTocados((actuales) => new Set(actuales).add(campo))
  }

  return (
    <Dialogo titulo="Registrar orden de pago" onCerrar={onCancelar}>
      <p className="m-0 mt-1 text-[12.5px] text-faint">
        {numero} · {transportista} · Resta pagar {formatearPesos(restaPagar)}
      </p>

      {error !== null && (
        <p role="alert" className={cn(clasesDeAvisoDePantalla.error, 'mt-4')}>
          {error}
        </p>
      )}

      {confirmacion === null ? (
        <form onSubmit={registrar} noValidate className={cn(clasesDeFormulario, 'mt-5')}>
          <div className={cn('campo max-w-[200px]', visible('fechaPago', errorDeFecha) !== null && 'con-error')}>
            <label htmlFor="fechaPago">Fecha de pago</label>
            <p id="ayuda-fechaPago" className="m-0 text-[11.5px] text-faint">
              Entre el {formatearFecha(fechaGeneracion)} y hoy.
            </p>
            <input
              id="fechaPago"
              type="date"
              required
              value={fechaPago}
              onChange={(evento) => setFechaPago(evento.target.value)}
              onBlur={() => tocar('fechaPago')}
              aria-invalid={visible('fechaPago', errorDeFecha) !== null}
              aria-describedby="ayuda-fechaPago"
            />
            {visible('fechaPago', errorDeFecha) !== null && (
              <p className="campo__error">{errorDeFecha}</p>
            )}
          </div>

          <div className={cn('campo max-w-[200px]', visible('importe', errorDeImporte) !== null && 'con-error')}>
            <label htmlFor="importe">Importe</label>
            <p id="ayuda-importe" className="m-0 text-[11.5px] text-faint">
              Hasta {formatearPesos(restaPagar)}.
            </p>
            <InputImporte
              id="importe"
              required
              value={importeTexto}
              onChange={(valor) => setImporteTexto(valor)}
              onBlur={() => tocar('importe')}
              aria-invalid={visible('importe', errorDeImporte) !== null}
              aria-describedby="ayuda-importe"
            />
            {visible('importe', errorDeImporte) !== null && (
              <p className="campo__error">{errorDeImporte}</p>
            )}
          </div>

          <BarraDeAcciones anclaje="contenedor" leyenda={LEYENDA_DE_OBLIGATORIOS}>
            <Boton variante="secundario" onClick={onCancelar} disabled={enviando}>
              Cancelar
            </Boton>
            <Boton
              type="submit"
              variante="primario"
              disabled={enviando}
              icono={<IconoEnRegla className="size-3" />}
            >
              Registrar orden de pago
            </Boton>
          </BarraDeAcciones>
        </form>
      ) : (
        <>
          <p className="mt-5 mb-0 text-[13px] leading-5 text-ink-soft">
            Vas a registrar una orden de pago por <strong>{formatearPesos(confirmacion.importe)}</strong>.{' '}
            {confirmacion.quedaPagada ? (
              'Con ella la liquidación queda pagada y se cierra.'
            ) : (
              <>
                Después de ella resta pagar <strong>{formatearPesos(confirmacion.restaPagarDespues)}</strong>.
              </>
            )}{' '}
            Una orden de pago no se modifica ni se elimina.
          </p>

          <BarraDeAcciones anclaje="contenedor">
            <Boton variante="secundario" onClick={() => setConfirmacion(null)} disabled={enviando}>
              Volver
            </Boton>
            <Boton
              variante="primario"
              onClick={() => void enviar(true)}
              disabled={enviando}
              icono={<IconoEnRegla className="size-3" />}
            >
              Confirmar orden de pago
            </Boton>
          </BarraDeAcciones>
        </>
      )}
    </Dialogo>
  )
}
