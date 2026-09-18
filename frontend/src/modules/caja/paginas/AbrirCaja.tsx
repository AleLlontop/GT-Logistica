import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { BarraDeAcciones, LEYENDA_DE_OBLIGATORIOS } from '../../../compartido/ui/BarraDeAcciones'
import { Boton } from '../../../compartido/ui/Boton'
import { clasesDeAvisoDePantalla, clasesDeFormularioAgrupado } from '../../../compartido/ui/clases'
import { cn } from '../../../compartido/ui/cn'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { IconoEnRegla } from '../../../compartido/ui/iconos'
import { InputImporte } from '../../../compartido/ui/InputImporte'
import { SeccionNumerada } from '../../../compartido/ui/SeccionNumerada'
import { CampoDeCaja } from '../componentes/CampoDeCaja'
import { leerImporte } from '../componentes/importe'
import {
  abrirCaja,
  CodigosErrorCaja,
  detalleDeError,
  esSinPermiso,
  MENSAJE_ACCION_SIN_PERMISO,
  MENSAJE_INESPERADO,
  obtenerMiCajaAbierta,
} from '../servicios/servicioCaja'

const TITULO = 'Abrir caja'

const VOLVER = { ruta: '/caja', etiqueta: 'Volver a cajas' }

export const MENSAJE_SIN_PERMISO_PARA_ABRIR =
  'No tenés permiso para abrir una caja. Si lo necesitás, pedíselo a quien administra los usuarios del sistema.'

export const MENSAJE_CAJA_ABIERTA = 'Caja abierta con éxito.'

interface Props {
  /**
   * `caja.gestionar`, de la sesión. Esta pantalla no carga nada al abrir, así que no tiene un `403` que la
   * decida: lo decide el permiso, y el `403` del guardado sigue siendo la restricción (convención [010]).
   */
  puedeGestionar: boolean
}

/**
 * Abrir la caja del día con su saldo inicial (User Story 1, FR-001 a FR-005). El responsable es quien está
 * en sesión y la fecha la pone el servidor: el único dato es el saldo.
 *
 * **Al guardar, el formulario no queda en pantalla**: se navega a la caja con la confirmación (convención
 * [005]).
 */
export function AbrirCaja({ puedeGestionar }: Props) {
  if (!puedeGestionar) {
    return (
      <section>
        <EncabezadoDePantalla titulo={TITULO} volverA={VOLVER} />
        <p role="alert" className={clasesDeAvisoDePantalla.advertencia}>
          {MENSAJE_SIN_PERMISO_PARA_ABRIR}
        </p>
      </section>
    )
  }

  return <FormularioDeApertura />
}

function FormularioDeApertura() {
  const navegar = useNavigate()

  const [saldoTexto, setSaldoTexto] = useState('')
  const [tocado, setTocado] = useState(false)
  const [intento, setIntento] = useState(false)
  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState<string | null>(null)
  // Con `caja_ya_abierta`, el enlace a la que ya tiene.
  const [yaAbierta, setYaAbierta] = useState<number | null>(null)

  const saldo = leerImporte(saldoTexto)

  const errorDelCampo =
    saldo.tipo === 'vacio'
      ? 'Escribí el saldo inicial.'
      : saldo.tipo === 'malEscrito'
        ? 'Escribí el importe con hasta dos decimales.'
        : saldo.valor < 0
          ? 'El saldo inicial no puede ser negativo.'
          : null

  // El error aparece después de tocar el campo o de intentar guardar, nunca al abrir.
  const visible = tocado || intento ? errorDelCampo : null

  async function guardar(evento: FormEvent) {
    evento.preventDefault()
    setIntento(true)

    if (errorDelCampo !== null || saldo.tipo !== 'valor') {
      return
    }

    setEnviando(true)
    setError(null)
    setYaAbierta(null)

    try {
      const caja = await abrirCaja(saldo.valor)

      navegar(`/caja/${caja.id}`, { state: { aviso: MENSAJE_CAJA_ABIERTA } })
    } catch (fallo) {
      const detalle = detalleDeError(fallo)

      if (esSinPermiso(fallo)) {
        setError(MENSAJE_ACCION_SIN_PERMISO)
      } else if (detalle?.codigo === CodigosErrorCaja.cajaYaAbierta) {
        setError(detalle.mensaje)
        obtenerMiCajaAbierta()
          .then((propia) => setYaAbierta(propia?.id ?? null))
          .catch(() => setYaAbierta(null))
      } else {
        setError(detalle?.mensaje ?? MENSAJE_INESPERADO)
      }

      setEnviando(false)
    }
  }

  return (
    <section>
      <EncabezadoDePantalla titulo={TITULO} volverA={VOLVER} />

      {error !== null && (
        <p role="alert" className={cn(clasesDeAvisoDePantalla.error, 'mb-[18px]')}>
          {error}
          {yaAbierta !== null && (
            <>
              {' '}
              <Link to={`/caja/${yaAbierta}`} className="underline underline-offset-2">
                Ir a mi caja abierta
              </Link>
            </>
          )}
        </p>
      )}

      <form onSubmit={guardar} noValidate className={clasesDeFormularioAgrupado}>
        <SeccionNumerada
          numero={1}
          titulo="Saldo inicial"
          explicacion="El efectivo con el que empezás el día. Quedás como responsable de la caja."
        >
          <CampoDeCaja id="saldoInicial" etiqueta="Saldo inicial" ancho="max-w-campo-corto" error={visible}>
            <InputImporte
              id="saldoInicial"
              required
              placeholder="0,00"
              value={saldoTexto}
              onChange={setSaldoTexto}
              onBlur={() => setTocado(true)}
              aria-invalid={visible !== null}
              aria-describedby={visible !== null ? 'error-saldoInicial' : undefined}
            />
          </CampoDeCaja>
        </SeccionNumerada>

        <BarraDeAcciones anclaje="viewport" leyenda={LEYENDA_DE_OBLIGATORIOS}>
          <Boton variante="secundario" onClick={() => navegar('/caja')} disabled={enviando}>
            Cancelar
          </Boton>
          <Boton type="submit" variante="primario" disabled={enviando} icono={<IconoEnRegla className="size-3" />}>
            Abrir caja
          </Boton>
        </BarraDeAcciones>
      </form>
    </section>
  )
}
