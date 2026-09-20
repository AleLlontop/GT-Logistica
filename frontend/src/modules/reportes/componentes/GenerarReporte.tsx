import { useId, useRef, useState } from 'react'
import { Boton } from '../../../compartido/ui/Boton'
import { Dialogo, DialogoAcciones } from '../../../compartido/ui/Dialogo'
import { clasesDeAvisoDePantalla } from '../../../compartido/ui/clases'
import { cn } from '../../../compartido/ui/cn'
import {
  generarReporte,
  mensajeDelRechazo,
  type FiltrosDeReporte,
  type FormatoDeReporte,
  type TipoDeReporte,
} from '../servicios/servicioReportes'

interface Props {
  /** Cuál de los cinco. Decide la ruta que se pide. */
  reporte: TipoDeReporte
  /** Los filtros aplicados en la pantalla. Vacío en los tres paneles, que no tienen. */
  filtros?: FiltrosDeReporte
  /** Cuántas filas tiene el listado que se está mirando. Decide FR-003. */
  cantidadDeFilas: number
  /** `reportes.emitir`. Sin él no se dibuja nada (FR-013). */
  puedeEmitir: boolean
}

const TEXTO_SIN_FILAS = 'No hay filas para reportar.'
const TEXTO_GENERANDO = 'Generando el reporte…'
const TEXTO_ABIERTO = 'Se abrió el reporte en una pestaña nueva.'
const TEXTO_DESCARGADO = 'Se descargó el reporte.'

/**
 * La acción *Generar reporte*, una sola para las cinco pantallas que la ofrecen (FR-001).
 *
 * **Sin el permiso devuelve `null`**, no un botón deshabilitado: la acción no existe para ese usuario
 * (FR-013, SC-005). Ocultarla es una cortesía; la restricción sigue siendo el `403` del servidor.
 *
 * **Es una acción secundaria en las cinco** (FR-005), y en las de sólo lectura no convierte a la
 * pantalla en una con acción primaria: el propósito de un panel de vencimientos es resolver lo que
 * está por vencer, no exportarlo.
 *
 * **Dos clics** (SC-001): la acción y el formato. El botón del formato ejecuta; no hay un *Generar*
 * aparte.
 *
 * **Después de cualquier rechazo la acción vuelve a quedar accionable y el componente no toca ningún
 * estado de la pantalla** (FR-017): los filtros y la página siguen donde estaban y se puede
 * reintentar sin recargar.
 */
export function GenerarReporte({ reporte, filtros = {}, cantidadDeFilas, puedeEmitir }: Props) {
  const [abierto, setAbierto] = useState(false)
  const [generando, setGenerando] = useState(false)
  const [estado, setEstado] = useState<string | null>(null)
  const [rechazo, setRechazo] = useState<string | null>(null)

  const idDeAyuda = useId()
  const disparador = useRef<HTMLButtonElement>(null)

  if (!puedeEmitir) {
    return null
  }

  const sinFilas = cantidadDeFilas === 0

  /**
   * Cerrar sin elegir **no modifica nada** y devuelve el foco al disparador (FR-002).
   *
   * El foco se devuelve acá y no se deja en manos del diálogo porque el contenedor se desmonta entero
   * al cerrar —`{abierto && <Dialogo …>}`—, y con eso el restablecimiento automático se pierde.
   */
  function cerrarSinGenerar() {
    setAbierto(false)

    // En el turno siguiente: el diálogo se desmonta con el cambio de estado, y su limpieza mueve el
    // foco después. Enfocar antes de eso lo perdería.
    setTimeout(() => disparador.current?.focus(), 0)
  }

  async function generar(formato: FormatoDeReporte) {
    setAbierto(false)
    setGenerando(true)
    setRechazo(null)
    setEstado(TEXTO_GENERANDO)

    try {
      const resultado = await generarReporte(reporte, formato, filtros)

      if (resultado.estado === 'tope_superado') {
        // El mensaje lo armó el servidor y se muestra tal cual llega: acá no se compone ninguno.
        setEstado(null)
        setRechazo(resultado.mensaje)

        return
      }

      setEstado(resultado.entrega === 'abierto' ? TEXTO_ABIERTO : TEXTO_DESCARGADO)
    } catch (fallo) {
      setEstado(null)
      setRechazo(mensajeDelRechazo(fallo))
    } finally {
      // **Un rechazo no es un callejón sin salida**: la acción queda accionable otra vez.
      setGenerando(false)
    }
  }

  return (
    <div className="flex flex-wrap items-center gap-2.5">
      <Boton
        ref={disparador}
        variante="secundario"
        disabled={sinFilas || generando}
        aria-describedby={sinFilas ? idDeAyuda : undefined}
        onClick={() => setAbierto(true)}
      >
        Generar reporte
      </Boton>

      {/* El deshabilitado se explica al lado, no sólo con el gris (convención [003]). */}
      {sinFilas && (
        <span id={idDeAyuda} className="atenuada text-[12.5px]">
          {TEXTO_SIN_FILAS}
        </span>
      )}

      {/*
        La región viva: el `role` va **en el `<p>` del mensaje** y no en un contenedor que lo
        envuelva, porque una región viva anuncia el elemento que contiene el texto (convenciones
        [003] y [008]).
      */}
      {estado !== null && (
        <p role="status" className={cn(clasesDeAvisoDePantalla.nota, 'text-[12.5px]')}>
          {estado}
        </p>
      )}

      {/* El rechazo va en su propia región, aparte de la de estado. */}
      {rechazo !== null && (
        <p role="alert" className={cn(clasesDeAvisoDePantalla.error, 'text-[12.5px]')}>
          {rechazo}
        </p>
      )}

      {abierto && (
        <Dialogo titulo="Generar reporte" onCerrar={cerrarSinGenerar}>
          <p className="mt-2.5 text-[13px] leading-5 text-ink-soft">Elegí el formato del archivo.</p>

          {/*
            **El orden es PDF → Excel → Cancelar**, y no el de `DialogoConfirmacion` —cancelar primero,
            confirmar después—: FR-002 fija exactamente dos opciones y en ese orden, y ése es también
            el orden en que las recorre el tabulador. El primario se lo lleva el primero.

            **El botón del formato ejecuta**: no hay un *Generar* aparte, para que SC-001 se cumpla en
            dos clics.
          */}
          <DialogoAcciones>
            <Boton variante="primario" onClick={() => void generar('pdf')}>
              PDF
            </Boton>
            <Boton variante="secundario" onClick={() => void generar('excel')}>
              Excel
            </Boton>
            <Boton variante="secundario" onClick={cerrarSinGenerar}>
              Cancelar
            </Boton>
          </DialogoAcciones>
        </Dialogo>
      )}
    </div>
  )
}
