import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { formatearCuit } from '../../../compartido/cuit'
import { formatearPesos } from '../../../compartido/moneda'
import { BarraDeAcciones } from '../../../compartido/ui/BarraDeAcciones'
import { Boton } from '../../../compartido/ui/Boton'
import { Callout } from '../../../compartido/ui/Callout'
import { clasesDeAvisoDePantalla, clasesDeFormularioAgrupado } from '../../../compartido/ui/clases'
import { cn } from '../../../compartido/ui/cn'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { IconoEnRegla } from '../../../compartido/ui/iconos'
import { SeccionNumerada } from '../../../compartido/ui/SeccionNumerada'
import { TablaDeViajes, type FilaDeViaje } from '../componentes/TablaDeViajes'
import {
  cantidadDeViajes,
  detalleDeError,
  editarLiquidacion,
  formatearPeriodo,
  listarDisponibles,
  obtenerLiquidacion,
  type LiquidacionDetalle,
} from '../servicios/servicioLiquidaciones'

const MENSAJE_INESPERADO = 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.'

/** Por fecha y número, el mismo orden que los disponibles y que el detalle. */
function ordenar(viajes: FilaDeViaje[]): FilaDeViaje[] {
  return [...viajes].sort((a, b) => a.fecha.localeCompare(b.fecha) || a.numero - b.numero)
}

/** El callout de bloqueo al abrir, con el motivo que el detalle permite reconstruir (FR-045). */
function motivoDeBloqueo(liquidacion: LiquidacionDetalle): string {
  if (liquidacion.estado === 'pagada') return `La liquidación ${liquidacion.numero} está pagada: no se puede editar.`
  if (liquidacion.estado === 'anulada') return `La liquidación ${liquidacion.numero} está anulada: no se puede editar.`
  if (liquidacion.importePagado > 0) {
    return `La liquidación ${liquidacion.numero} ya tiene órdenes de pago: no se puede editar.`
  }

  return (
    `El transportista de la liquidación ${liquidacion.numero} se dio de baja o dejó de ser externo: no se ` +
    'puede editar.'
  )
}

/**
 * Editar los viajes de una liquidación (User Story 5, FR-045 a FR-052).
 *
 * **Sin campos de entrada**: transportista y período son de sólo lectura, y la edición se hace moviendo
 * filas entre *incluidos* y *disponibles* **sin llamar al servidor**. Lo que se guarda es el conjunto
 * final, con la `version` con que se abrió: si otro usuario guardó en el medio, la edición se rechaza
 * (FR-048).
 */
export function EditarLiquidacion() {
  const { id } = useParams()
  const liquidacionId = Number(id)
  const navegar = useNavigate()

  const [liquidacion, setLiquidacion] = useState<LiquidacionDetalle | null>(null)
  const [incluidos, setIncluidos] = useState<FilaDeViaje[]>([])
  const [disponibles, setDisponibles] = useState<FilaDeViaje[]>([])
  const [cargando, setCargando] = useState(true)
  const [anuncio, setAnuncio] = useState('')
  const [guardando, setGuardando] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let vigente = true

    async function traer() {
      try {
        const traida = await obtenerLiquidacion(liquidacionId)

        if (!vigente) return

        setLiquidacion(traida)
        setIncluidos(traida.viajes)

        if (traida.puedeEditarse) {
          const libres = await listarDisponibles(traida.transportista.id, traida.mes, traida.anio)

          if (vigente) setDisponibles(libres)
        }
      } catch (fallo) {
        if (vigente) setError(detalleDeError(fallo)?.mensaje ?? MENSAJE_INESPERADO)
      } finally {
        if (vigente) setCargando(false)
      }
    }

    void traer()

    return () => {
      vigente = false
    }
  }, [liquidacionId])

  const total = incluidos.reduce((suma, viaje) => suma + viaje.importe, 0)

  const hayCambios = useMemo(() => {
    if (liquidacion === null) return false

    const originales = new Set(liquidacion.viajes.map((viaje) => viaje.id))

    return incluidos.length !== originales.size || incluidos.some((viaje) => !originales.has(viaje.id))
  }, [liquidacion, incluidos])

  function anunciar(nuevos: FilaDeViaje[]) {
    const suma = nuevos.reduce((acumulado, viaje) => acumulado + viaje.importe, 0)

    setAnuncio(`Importe total: ${formatearPesos(suma)} con ${cantidadDeViajes(nuevos.length)}.`)
  }

  function quitar(viaje: FilaDeViaje) {
    const nuevos = incluidos.filter((incluido) => incluido.id !== viaje.id)

    setIncluidos(nuevos)
    setDisponibles((actuales) => ordenar([...actuales, viaje]))
    anunciar(nuevos)
  }

  function agregar(viaje: FilaDeViaje) {
    const nuevos = ordenar([...incluidos, viaje])

    setIncluidos(nuevos)
    setDisponibles((actuales) => actuales.filter((disponible) => disponible.id !== viaje.id))
    anunciar(nuevos)
  }

  async function guardar(evento: FormEvent) {
    evento.preventDefault()

    if (liquidacion === null || incluidos.length === 0 || total === 0 || !hayCambios) {
      return
    }

    setGuardando(true)
    setError(null)

    try {
      const guardada = await editarLiquidacion(
        liquidacion.id,
        incluidos.map((viaje) => viaje.id),
        liquidacion.version,
      )

      navegar(`/liquidaciones/${guardada.id}`, {
        state: {
          aviso:
            `Se guardaron los cambios de la liquidación ${guardada.numero}: ahora suma ` +
            `${formatearPesos(guardada.importeTotal)} con ${cantidadDeViajes(guardada.viajes.length)}.`,
        },
      })
    } catch (fallo) {
      setError(detalleDeError(fallo)?.mensaje ?? MENSAJE_INESPERADO)
      setGuardando(false)
    }
  }

  const volver = { ruta: `/liquidaciones/${liquidacionId}`, etiqueta: 'Volver a la liquidación' }
  const titulo = liquidacion === null ? 'Editar liquidación' : `Editar liquidación ${liquidacion.numero}`

  if (cargando || liquidacion === null) {
    return (
      <section>
        <EncabezadoDePantalla titulo={titulo} volverA={volver} />
        {error !== null ? (
          <p role="alert" className={clasesDeAvisoDePantalla.error}>
            {error}
          </p>
        ) : (
          <p role="status" className="m-0 text-[13px] text-ink-soft">
            Cargando liquidación…
          </p>
        )}
      </section>
    )
  }

  if (!liquidacion.puedeEditarse) {
    return (
      <section>
        <EncabezadoDePantalla titulo={titulo} volverA={volver} />
        <Callout tono="pendiente" titulo="No se puede editar.">
          {motivoDeBloqueo(liquidacion)}
        </Callout>
      </section>
    )
  }

  const periodo = formatearPeriodo(liquidacion.mes, liquidacion.anio)
  const razonSocial = liquidacion.transportista.razonSocial

  return (
    <section>
      <EncabezadoDePantalla titulo={titulo} volverA={volver} />

      {error !== null && (
        <p role="alert" className={cn(clasesDeAvisoDePantalla.error, 'mb-[18px]')}>
          {error}
        </p>
      )}

      <form onSubmit={guardar} noValidate className={clasesDeFormularioAgrupado}>
        <SeccionNumerada
          numero={1}
          titulo="Transportista y período"
          explicacion="No se cambian. Si están mal, anulá la liquidación y generá una nueva."
        >
          <p className="m-0 text-[13.5px] font-medium text-ink">
            {razonSocial} — <span className="font-mono">{formatearCuit(liquidacion.transportista.cuit)}</span> ·{' '}
            {periodo}
          </p>
        </SeccionNumerada>

        <SeccionNumerada numero={2} titulo="Viajes incluidos">
          {/* El total recalculado se anuncia sin que la pantalla cambie de lugar (FR-047, FR-062). */}
          <p role="status" className="sr-only">
            {anuncio}
          </p>

          {incluidos.length === 0 ? (
            <p className="m-0 text-[13px] text-ink-soft">
              La liquidación se quedó sin viajes. Agregá al menos uno o volvé sin guardar.
            </p>
          ) : (
            <>
              <TablaDeViajes
                viajes={incluidos}
                titulo="Viajes incluidos"
                accion={{ etiqueta: 'Quitar', onAccion: quitar }}
              />
              {total === 0 && (
                <p className="m-0 text-[13px] text-ink-soft">
                  Los viajes incluidos suman $ 0,00. Una liquidación necesita importe a liquidar.
                </p>
              )}
            </>
          )}
        </SeccionNumerada>

        <SeccionNumerada numero={3} titulo="Viajes disponibles del período">
          {disponibles.length === 0 ? (
            <p className="m-0 text-[13px] text-ink-soft">
              No hay otros viajes rendidos de {razonSocial} en {periodo} para agregar.
            </p>
          ) : (
            <TablaDeViajes
              viajes={disponibles}
              titulo="Viajes disponibles del período"
              accion={{ etiqueta: 'Agregar', onAccion: agregar }}
              sinTotal
            />
          )}
        </SeccionNumerada>

        <BarraDeAcciones anclaje="viewport">
          <Boton
            variante="secundario"
            onClick={() => navegar(`/liquidaciones/${liquidacion.id}`)}
            disabled={guardando}
          >
            Cancelar
          </Boton>
          <Boton
            type="submit"
            variante="primario"
            disabled={guardando || incluidos.length === 0 || total === 0 || !hayCambios}
            icono={<IconoEnRegla className="size-3" />}
          >
            Guardar cambios
          </Boton>
        </BarraDeAcciones>
      </form>
    </section>
  )
}
