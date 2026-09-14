import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { formatearCuit } from '../../../compartido/cuit'
import { aniosDelPeriodo } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { BarraDeAcciones, LEYENDA_DE_OBLIGATORIOS } from '../../../compartido/ui/BarraDeAcciones'
import { Boton } from '../../../compartido/ui/Boton'
import { Callout } from '../../../compartido/ui/Callout'
import { clasesDeAvisoDePantalla, clasesDeFormularioAgrupado } from '../../../compartido/ui/clases'
import { cn } from '../../../compartido/ui/cn'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { IconoEnRegla } from '../../../compartido/ui/iconos'
import { SeccionNumerada } from '../../../compartido/ui/SeccionNumerada'
import { TablaDeViajes } from '../componentes/TablaDeViajes'
import {
  cantidadDeViajes,
  detalleDeError,
  formatearPeriodo,
  generarLiquidacion,
  listarDisponibles,
  listarTransportistas,
  MESES,
  type TransportistaResumen,
  type TransportistasLiquidables,
  type ViajeDisponible,
} from '../servicios/servicioLiquidaciones'

const VOLVER = { ruta: '/liquidaciones', etiqueta: 'Volver a liquidaciones' }

const MENSAJE_INESPERADO = 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.'

type CampoDeSeleccion = 'transportistaId' | 'mes' | 'anio'

const ERRORES_DE_CAMPO: Record<CampoDeSeleccion, string> = {
  transportistaId: 'Elegí el transportista al que le vas a liquidar.',
  mes: 'Elegí el mes del período.',
  anio: 'Elegí el año del período.',
}

interface Seleccion {
  transportistaId: number | ''
  mes: number | ''
  anio: number | ''
}

/** Lo que devolvió la última búsqueda, junto con la selección con que se hizo. */
interface Busqueda {
  transportista: TransportistaResumen
  mes: number
  anio: number
  viajes: ViajeDisponible[]
}

/**
 * Generar la liquidación de un fletero para un período (User Story 1).
 *
 * **Agrupa todos los viajes disponibles, sin casillas** (FR-008): la agrupación automática es lo que
 * impide olvidarse alguno, y quitar un viaje puntual es un paso explícito de la edición.
 *
 * **Se guarda exactamente lo que se revisó**: cambiar el transportista o el período vacía la lista y
 * obliga a buscar de nuevo (FR-010), y el cuerpo lleva los viajes que se mostraron, no los que haya al
 * guardar (FR-011).
 *
 * **Al guardar, el formulario no queda en pantalla**: se navega al detalle con la confirmación
 * (FR-019, convención [005]).
 */
export function GenerarLiquidacion() {
  const navegar = useNavigate()

  const [armado, setArmado] = useState<TransportistasLiquidables | null>(null)
  const [errorDeCarga, setErrorDeCarga] = useState<string | null>(null)

  // El año viene propuesto en el en curso, que es el que se liquida casi siempre.
  const [seleccion, setSeleccion] = useState<Seleccion>(() => ({
    transportistaId: '',
    mes: '',
    anio: new Date().getFullYear(),
  }))
  const [tocados, setTocados] = useState<Set<CampoDeSeleccion>>(new Set())
  const [intentoBuscar, setIntentoBuscar] = useState(false)

  const [busqueda, setBusqueda] = useState<Busqueda | null>(null)
  const [buscando, setBuscando] = useState(false)
  const [anuncio, setAnuncio] = useState('')

  const [guardando, setGuardando] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    listarTransportistas()
      .then(setArmado)
      .catch(() => setErrorDeCarga('No pudimos traer los transportistas. Volvé a intentar en unos minutos.'))
  }, [])

  /** El error aparece recién después de tocar el campo o de intentar buscar, nunca al cargar. */
  function errorDe(campo: CampoDeSeleccion): string | null {
    return (tocados.has(campo) || intentoBuscar) && seleccion[campo] === '' ? ERRORES_DE_CAMPO[campo] : null
  }

  function cambiar(campo: CampoDeSeleccion, texto: string) {
    setSeleccion((actual) => ({ ...actual, [campo]: texto === '' ? '' : Number(texto) }))
    setError(null)

    // FR-010: la lista que se había traído deja de corresponder a lo que está elegido.
    if (busqueda !== null) {
      setBusqueda(null)
      setAnuncio('Cambiaste la selección. Buscá los viajes de nuevo.')
    }
  }

  function tocar(campo: CampoDeSeleccion) {
    setTocados((actuales) => new Set(actuales).add(campo))
  }

  async function buscar() {
    setIntentoBuscar(true)

    const { transportistaId, mes, anio } = seleccion

    // FR-003: con algún campo vacío no se busca.
    if (transportistaId === '' || mes === '' || anio === '') {
      return
    }

    const transportista = armado?.transportistas.find((candidato) => candidato.id === transportistaId)

    if (transportista === undefined) {
      return
    }

    setBuscando(true)
    setError(null)
    setAnuncio('')

    try {
      const viajes = await listarDisponibles(transportistaId, mes, anio)
      const total = viajes.reduce((suma, viaje) => suma + viaje.importe, 0)

      setBusqueda({ transportista, mes, anio, viajes })

      if (viajes.length > 0) {
        setAnuncio(
          `${viajes.length === 1 ? 'Se encontró' : 'Se encontraron'} ${cantidadDeViajes(viajes.length)} ` +
            `por ${formatearPesos(total)}.`,
        )
      }
    } catch (fallo) {
      setError(detalleDeError(fallo)?.mensaje ?? MENSAJE_INESPERADO)
    } finally {
      setBuscando(false)
    }
  }

  const total = busqueda?.viajes.reduce((suma, viaje) => suma + viaje.importe, 0) ?? 0
  const puedeGuardar = busqueda !== null && busqueda.viajes.length > 0 && total > 0 && !guardando

  async function guardar(evento: FormEvent) {
    evento.preventDefault()

    if (!puedeGuardar || busqueda === null) {
      return
    }

    setGuardando(true)
    setError(null)

    try {
      const creada = await generarLiquidacion({
        transportistaId: busqueda.transportista.id,
        mes: busqueda.mes,
        anio: busqueda.anio,
        viajeIds: busqueda.viajes.map((viaje) => viaje.id),
      })

      navegar(`/liquidaciones/${creada.id}`, {
        state: {
          aviso:
            `Se generó la liquidación ${creada.numero} por ${formatearPesos(creada.importeTotal)} ` +
            `con ${cantidadDeViajes(creada.viajes.length)}.`,
        },
      })
    } catch (fallo) {
      setError(detalleDeError(fallo)?.mensaje ?? MENSAJE_INESPERADO)
      setGuardando(false)
    }
  }

  if (armado === null) {
    return (
      <section>
        <EncabezadoDePantalla titulo="Generar liquidación" volverA={VOLVER} />
        {errorDeCarga !== null ? (
          <p role="alert" className={clasesDeAvisoDePantalla.error}>
            {errorDeCarga}
          </p>
        ) : (
          <p role="status" className="m-0 text-[13px] text-ink-soft">
            Cargando…
          </p>
        )}
      </section>
    )
  }

  // FR-001a: sin empresa emisora no se distingue a G&T Logística de los externos. Se dice y se ofrece la
  // salida, sin formulario.
  if (!armado.empresaEmisoraConfigurada) {
    return (
      <section>
        <EncabezadoDePantalla titulo="Generar liquidación" volverA={VOLVER} />
        <Callout tono="pendiente" titulo="No se puede generar todavía.">
          Falta configurar la empresa emisora: sin su CUIT el sistema no distingue a G&amp;T Logística de
          los transportistas externos. Configurala en{' '}
          <Link to="/facturacion/empresa" className="text-brand underline underline-offset-2">
            Empresa emisora
          </Link>{' '}
          y volvé.
        </Callout>
      </section>
    )
  }

  if (armado.transportistas.length === 0) {
    return (
      <section>
        <EncabezadoDePantalla titulo="Generar liquidación" volverA={VOLVER} />
        <EstadoVacio caso="vacio">
          <strong>No hay transportistas externos activos.</strong> Cargá uno en{' '}
          <Link to="/transportistas" className="text-brand underline underline-offset-2">
            Transportistas
          </Link>{' '}
          para poder liquidarle.
        </EstadoVacio>
      </section>
    )
  }

  return (
    <section>
      <EncabezadoDePantalla titulo="Generar liquidación" volverA={VOLVER} />

      {error !== null && (
        <p role="alert" className={cn(clasesDeAvisoDePantalla.error, 'mb-[18px]')}>
          {error}
        </p>
      )}

      <form onSubmit={guardar} noValidate className={clasesDeFormularioAgrupado}>
        <SeccionNumerada
          numero={1}
          titulo="Transportista y período"
          explicacion="Se liquidan los viajes rendidos de ese transportista con fecha dentro del mes elegido."
        >
          <CampoDeSeleccion
            id="transportistaId"
            etiqueta="Transportista"
            ancho="max-w-campo-largo"
            valor={seleccion.transportistaId}
            error={errorDe('transportistaId')}
            onCambio={(texto) => cambiar('transportistaId', texto)}
            onTocar={() => tocar('transportistaId')}
          >
            <option value="">Seleccioná un transportista</option>
            {armado.transportistas.map((transportista) => (
              <option key={transportista.id} value={transportista.id}>
                {transportista.razonSocial} — {formatearCuit(transportista.cuit)}
              </option>
            ))}
          </CampoDeSeleccion>

          <div className="flex flex-wrap gap-3.5">
            <CampoDeSeleccion
              id="mes"
              etiqueta="Mes"
              ancho="w-[120px]"
              valor={seleccion.mes}
              error={errorDe('mes')}
              onCambio={(texto) => cambiar('mes', texto)}
              onTocar={() => tocar('mes')}
            >
              <option value="">Mes</option>
              {MESES.map((numero) => (
                <option key={numero} value={numero}>
                  {String(numero).padStart(2, '0')}
                </option>
              ))}
            </CampoDeSeleccion>

            <CampoDeSeleccion
              id="anio"
              etiqueta="Año"
              ancho="w-[120px]"
              valor={seleccion.anio}
              error={errorDe('anio')}
              onCambio={(texto) => cambiar('anio', texto)}
              onTocar={() => tocar('anio')}
            >
              <option value="">Año</option>
              {aniosDelPeriodo().map((numero) => (
                <option key={numero} value={numero}>
                  {numero}
                </option>
              ))}
            </CampoDeSeleccion>
          </div>

          <div>
            <Boton variante="secundario" onClick={buscar} disabled={buscando}>
              Buscar viajes
            </Boton>
          </div>
        </SeccionNumerada>

        <SeccionNumerada numero={2} titulo="Viajes a liquidar">
          {/* La región vive siempre: el anuncio sale cuando le entra el texto (convención [003]). */}
          <p role="status" className="sr-only">
            {anuncio}
          </p>

          {buscando ? (
            <p role="status" className="m-0 text-[13px] text-ink-soft">
              Buscando viajes rendidos…
            </p>
          ) : busqueda === null ? (
            <p className="m-0 text-[13px] text-ink-soft">
              Elegí el transportista y el período, y buscá sus viajes.
            </p>
          ) : busqueda.viajes.length === 0 ? (
            <p role="status" className="m-0 text-[13px] leading-5 text-ink-soft">
              {busqueda.transportista.razonSocial} no tiene viajes rendidos para liquidar en{' '}
              {formatearPeriodo(busqueda.mes, busqueda.anio)}. Los viajes pendientes, en curso, anulados o
              ya liquidados no se ofrecen.
            </p>
          ) : (
            <>
              <TablaDeViajes viajes={busqueda.viajes} titulo="Viajes a liquidar" conEstado />

              {total === 0 && (
                <Callout tono="pendiente" titulo="No hay importe a liquidar.">
                  Los viajes de este período suman $ 0,00, y no se genera una liquidación sin importe.
                </Callout>
              )}
            </>
          )}
        </SeccionNumerada>

        <BarraDeAcciones anclaje="viewport" leyenda={LEYENDA_DE_OBLIGATORIOS}>
          <Boton variante="secundario" onClick={() => navegar('/liquidaciones')} disabled={guardando}>
            Cancelar
          </Boton>
          <Boton
            type="submit"
            variante="primario"
            disabled={!puedeGuardar}
            icono={<IconoEnRegla className="size-3" />}
          >
            Guardar liquidación
          </Boton>
        </BarraDeAcciones>
      </form>
    </section>
  )
}

interface CampoDeSeleccionProps {
  id: string
  etiqueta: string
  /** El ancho comunica el dato: el transportista es largo, el mes y el año son cortos. */
  ancho: string
  valor: number | ''
  error: string | null
  onCambio: (texto: string) => void
  onTocar: () => void
  children: React.ReactNode
}

/** Un desplegable obligatorio con su error debajo, con la anatomía `.campo` de los formularios. */
function CampoDeSeleccion({
  id,
  etiqueta,
  ancho,
  valor,
  error,
  onCambio,
  onTocar,
  children,
}: CampoDeSeleccionProps) {
  return (
    <div className={cn('campo', error !== null && 'con-error', ancho)}>
      <label htmlFor={id}>{etiqueta}</label>
      <select
        id={id}
        required
        value={valor}
        onChange={(evento) => onCambio(evento.target.value)}
        onBlur={onTocar}
        aria-invalid={error !== null}
        aria-describedby={error !== null ? `error-${id}` : undefined}
      >
        {children}
      </select>
      {error !== null && (
        <p className="campo__error" id={`error-${id}`}>
          {error}
        </p>
      )}
    </div>
  )
}
