import { useRef, useState, type FormEvent, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { formatearFecha, hoyEnIso } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { BarraDeAcciones, LEYENDA_DE_OBLIGATORIOS } from '../../../compartido/ui/BarraDeAcciones'
import { Boton } from '../../../compartido/ui/Boton'
import { Callout } from '../../../compartido/ui/Callout'
import { clasesDeAvisoDePantalla, clasesDeFormularioAgrupado } from '../../../compartido/ui/clases'
import { cn } from '../../../compartido/ui/cn'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { IconoEnRegla } from '../../../compartido/ui/iconos'
import { SeccionNumerada } from '../../../compartido/ui/SeccionNumerada'
import {
  CodigosErrorAdelantos,
  detalleDeError,
  esSinPermiso,
  formatearPersona,
  listarBeneficiarios,
  MENSAJE_ACCION_SIN_PERMISO,
  mensajeDeRechazo,
  primeraFechaAdmitida,
  registrarAdelanto,
  type Beneficiarios,
  type TipoBeneficiario,
} from '../servicios/servicioAdelantos'

const TITULO = 'Registrar adelanto'

const VOLVER = { ruta: '/adelantos', etiqueta: 'Volver a adelantos' }

const LARGO_MAXIMO_DEL_MOTIVO = 200

export const MENSAJE_SIN_PERMISO_PARA_REGISTRAR =
  'No tenés permiso para registrar adelantos. Si lo necesitás, pedíselo a quien administra los usuarios del sistema.'

const IMPORTE_REQUERIDO = 'Escribí un importe mayor que cero. Ejemplo: 150000,00'
const IMPORTE_MAL_ESCRITO = 'Escribí el importe con hasta dos decimales. Ejemplo: 150000,50'

type Campo = 'tipo' | 'personaId' | 'fecha' | 'motivo' | 'importe'

/**
 * `150000,50` → `150000.5`, o qué está mal: vacío, cero o negativo piden un importe mayor que cero; más de
 * dos decimales o mal escrito, el formato (contracts/README §Errores de campo).
 */
function leerImporte(texto: string): { valor: number } | { error: string } {
  const normalizado = texto.trim().replace(',', '.')

  if (normalizado === '') return { error: IMPORTE_REQUERIDO }
  if (!/^-?\d+(\.\d+)?$/.test(normalizado)) return { error: IMPORTE_MAL_ESCRITO }

  const valor = Number(normalizado)

  if (valor <= 0) return { error: IMPORTE_REQUERIDO }
  if (!/^\d+(\.\d{1,2})?$/.test(normalizado)) return { error: IMPORTE_MAL_ESCRITO }

  return { valor }
}

/** Cuántas personas hay para elegir, o que no hay ninguna (FR-004). Sin empresa emisora habla el callout. */
function anuncioDe(tipo: TipoBeneficiario, beneficiarios: Beneficiarios): string {
  const cantidad = beneficiarios.personas.length

  if (tipo === 'chofer') {
    if (!beneficiarios.empresaEmisoraConfigurada) return ''

    return cantidad === 0
      ? 'No hay choferes de G&T Logística activos para elegir. Los choferes se cargan desde Choferes.'
      : `Hay ${cantidad} ${cantidad === 1 ? 'chofer' : 'choferes'} de G&T Logística para elegir.`
  }

  return cantidad === 0
    ? 'No hay empleados activos para elegir. Las personas se cargan desde Personas.'
    : `Hay ${cantidad} ${cantidad === 1 ? 'empleado' : 'empleados'} para elegir.`
}

interface Props {
  /**
   * `adelantos.gestionar`, de la sesión. Esta pantalla no carga nada al abrir, así que no tiene un `403`
   * que la decida: lo decide el permiso, y el `403` del guardado sigue siendo la restricción (research §7).
   */
  puedeGestionar: boolean
}

/**
 * Registrar un adelanto (User Story 1, FR-001 a FR-012).
 *
 * **Las personas que se ofrecen salen de la misma regla con la que el servidor valida el guardado**
 * (research §1): si alguien se da de baja mientras el formulario está abierto, el rechazo lo dice y no se
 * pierde nada de lo cargado.
 *
 * **Al guardar, el formulario no queda en pantalla**: se navega al detalle con la confirmación (FR-011,
 * convención [005]).
 */
export function RegistrarAdelanto({ puedeGestionar }: Props) {
  if (!puedeGestionar) {
    return (
      <section>
        <EncabezadoDePantalla titulo={TITULO} volverA={VOLVER} />
        <p role="alert" className={clasesDeAvisoDePantalla.advertencia}>
          {MENSAJE_SIN_PERMISO_PARA_REGISTRAR}
        </p>
      </section>
    )
  }

  return <FormularioDeAdelanto />
}

function FormularioDeAdelanto() {
  const navegar = useNavigate()

  // Derivados del día en curso y nunca escritos literales (FR-005, convención [009]).
  const hoy = hoyEnIso()
  const piso = primeraFechaAdmitida()

  const [tipo, setTipo] = useState<TipoBeneficiario | ''>('')
  const [personaId, setPersonaId] = useState<number | ''>('')
  const [fecha, setFecha] = useState(hoy)
  const [motivo, setMotivo] = useState('')
  const [importeTexto, setImporteTexto] = useState('')

  const [beneficiarios, setBeneficiarios] = useState<Beneficiarios | null>(null)
  const [anuncio, setAnuncio] = useState('')
  // Cambiar de tipo dos veces seguidas no deja que la respuesta vieja pise a la nueva.
  const pedidoVigente = useRef(0)

  const [tocados, setTocados] = useState<Set<Campo>>(new Set())
  const [intento, setIntento] = useState(false)
  // Si el servidor rechaza la fecha, manda su rango: el día de la pantalla puede no ser el de Argentina.
  const [desdeDelServidor, setDesdeDelServidor] = useState<string | null>(null)

  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const importe = leerImporte(importeTexto)

  const errores: Record<Campo, string | null> = {
    tipo: tipo === '' ? 'Elegí si el adelanto es para un chofer o un empleado.' : null,
    personaId:
      personaId !== ''
        ? null
        : tipo === ''
          ? 'Elegí primero el tipo de persona.'
          : 'Elegí la persona que recibe el adelanto.',
    fecha:
      fecha === ''
        ? 'Elegí la fecha en que se otorgó el adelanto.'
        : desdeDelServidor !== null
          ? `La fecha tiene que estar entre el ${formatearFecha(desdeDelServidor)} y hoy.`
          : fecha < piso || fecha > hoy
            ? `La fecha tiene que estar entre el ${formatearFecha(piso)} y hoy.`
            : null,
    motivo: motivo.trim() === '' ? 'Escribí el motivo del adelanto. Ejemplo: gastos médicos' : null,
    importe: 'error' in importe ? importe.error : null,
  }

  /** El error aparece después de tocar el campo o de intentar guardar, nunca al abrir. */
  function visible(campo: Campo): string | null {
    return tocados.has(campo) || intento ? errores[campo] : null
  }

  function tocar(campo: Campo) {
    setTocados((actuales) => new Set(actuales).add(campo))
  }

  /** FR-003: la persona se vacía y se buscan las del tipo nuevo; fecha, motivo e importe quedan. */
  function cambiarTipo(texto: string) {
    const nuevo = texto as TipoBeneficiario | ''
    const pedido = ++pedidoVigente.current

    setTipo(nuevo)
    setPersonaId('')
    setBeneficiarios(null)
    setError(null)

    if (nuevo === '') {
      setAnuncio('')
      return
    }

    setAnuncio(nuevo === 'chofer' ? 'Buscando choferes…' : 'Buscando empleados…')

    listarBeneficiarios(nuevo)
      .then((traidos) => {
        if (pedido !== pedidoVigente.current) return

        setBeneficiarios(traidos)
        setAnuncio(anuncioDe(nuevo, traidos))
      })
      .catch((fallo) => {
        if (pedido !== pedidoVigente.current) return

        setAnuncio('')
        setError(
          esSinPermiso(fallo)
            ? MENSAJE_ACCION_SIN_PERMISO
            : 'No pudimos traer las personas. Volvé a intentar en unos minutos.',
        )
      })
  }

  async function guardar(evento: FormEvent) {
    evento.preventDefault()
    setIntento(true)

    if (Object.values(errores).some((mensaje) => mensaje !== null)) {
      return
    }

    if (tipo === '' || personaId === '' || !('valor' in importe)) {
      return
    }

    setEnviando(true)
    setError(null)

    try {
      const creado = await registrarAdelanto({
        tipo,
        personaId,
        fecha,
        motivo: motivo.trim(),
        importe: importe.valor,
      })

      navegar(`/adelantos/${creado.id}`, {
        state: {
          aviso:
            `Se registró el adelanto de ${formatearPesos(creado.importe)} para ${formatearPersona(creado.persona)}. ` +
            'Queda pendiente de aprobación.',
        },
      })
    } catch (fallo) {
      const detalle = detalleDeError(fallo)

      if (detalle?.codigo === CodigosErrorAdelantos.fechaFueraDeRango && detalle.desde !== undefined) {
        setDesdeDelServidor(detalle.desde)
      } else {
        setError(mensajeDeRechazo(fallo))
      }

      setEnviando(false)
    }
  }

  const sinEmpresaEmisora = tipo === 'chofer' && beneficiarios !== null && !beneficiarios.empresaEmisoraConfigurada

  return (
    <section>
      <EncabezadoDePantalla titulo={TITULO} volverA={VOLVER} />

      {error !== null && (
        <p role="alert" className={cn(clasesDeAvisoDePantalla.error, 'mb-[18px]')}>
          {error}
        </p>
      )}

      <form onSubmit={guardar} noValidate className={clasesDeFormularioAgrupado}>
        <SeccionNumerada
          numero={1}
          titulo="Beneficiario"
          explicacion="Se ofrecen los choferes de G&T Logística y los empleados activos."
        >
          <CampoDeAdelanto id="tipo" etiqueta="Tipo de persona" ancho="max-w-campo-corto" error={visible('tipo')}>
            <select
              id="tipo"
              required
              value={tipo}
              onChange={(evento) => cambiarTipo(evento.target.value)}
              onBlur={() => tocar('tipo')}
              aria-invalid={visible('tipo') !== null}
              aria-describedby={visible('tipo') !== null ? 'error-tipo' : undefined}
            >
              <option value="">Seleccioná el tipo</option>
              <option value="chofer">Chofer</option>
              <option value="empleado">Empleado</option>
            </select>
          </CampoDeAdelanto>

          {sinEmpresaEmisora && (
            <Callout tono="pendiente" titulo="No se pueden elegir choferes todavía.">
              Falta configurar la empresa emisora: sin su CUIT el sistema no distingue a los choferes de G&amp;T
              Logística de los de transportistas externos. Configurala en{' '}
              <Link to="/facturacion/empresa" className="text-brand underline underline-offset-2">
                Empresa emisora
              </Link>{' '}
              y volvé. Para un empleado, elegí el tipo <em>Empleado</em>.
            </Callout>
          )}

          {/* No se deshabilita sin tipo: sigue alcanzable con el teclado y puede marcarse con su error. */}
          <CampoDeAdelanto id="personaId" etiqueta="Persona" ancho="max-w-campo-largo" error={visible('personaId')}>
            <select
              id="personaId"
              required
              value={personaId}
              onChange={(evento) =>
                setPersonaId(evento.target.value === '' ? '' : Number(evento.target.value))
              }
              onBlur={() => tocar('personaId')}
              aria-invalid={visible('personaId') !== null}
              aria-describedby={visible('personaId') !== null ? 'error-personaId' : 'anuncio-personas'}
            >
              <option value="">{tipo === '' ? 'Elegí primero el tipo de persona' : 'Seleccioná una persona'}</option>
              {beneficiarios?.personas.map((persona) => (
                <option key={persona.id} value={persona.id}>
                  {formatearPersona(persona)} — {persona.dni}
                </option>
              ))}
            </select>
            {/* La región vive siempre: el anuncio sale cuando le entra el texto (FR-040). */}
            <p id="anuncio-personas" role="status" className="m-0 text-[12px] text-ink-soft">
              {anuncio}
            </p>
          </CampoDeAdelanto>
        </SeccionNumerada>

        <SeccionNumerada numero={2} titulo="Adelanto">
          <CampoDeAdelanto
            id="fecha"
            etiqueta="Fecha"
            ancho="max-w-campo-corto"
            error={visible('fecha')}
            ayuda={`Desde el ${formatearFecha(piso)} hasta hoy.`}
          >
            <input
              id="fecha"
              type="date"
              required
              min={piso}
              max={hoy}
              value={fecha}
              onChange={(evento) => {
                setFecha(evento.target.value)
                setDesdeDelServidor(null)
              }}
              onBlur={() => tocar('fecha')}
              aria-invalid={visible('fecha') !== null}
              aria-describedby="ayuda-fecha"
            />
          </CampoDeAdelanto>

          <CampoDeAdelanto id="motivo" etiqueta="Motivo" ancho="max-w-campo-largo" error={visible('motivo')}>
            <input
              id="motivo"
              type="text"
              required
              maxLength={LARGO_MAXIMO_DEL_MOTIVO}
              placeholder="Gastos médicos"
              value={motivo}
              onChange={(evento) => setMotivo(evento.target.value)}
              onBlur={() => tocar('motivo')}
              aria-invalid={visible('motivo') !== null}
              aria-describedby={visible('motivo') !== null ? 'error-motivo' : undefined}
            />
          </CampoDeAdelanto>

          <CampoDeAdelanto id="importe" etiqueta="Importe" ancho="max-w-campo-corto" error={visible('importe')}>
            <input
              id="importe"
              type="text"
              inputMode="decimal"
              required
              className="text-right"
              placeholder="150000,00"
              value={importeTexto}
              onChange={(evento) => setImporteTexto(evento.target.value)}
              onBlur={() => tocar('importe')}
              aria-invalid={visible('importe') !== null}
              aria-describedby={visible('importe') !== null ? 'error-importe' : undefined}
            />
          </CampoDeAdelanto>
        </SeccionNumerada>

        <BarraDeAcciones anclaje="viewport" leyenda={LEYENDA_DE_OBLIGATORIOS}>
          <Boton variante="secundario" onClick={() => navegar('/adelantos')} disabled={enviando}>
            Cancelar
          </Boton>
          <Boton type="submit" variante="primario" disabled={enviando} icono={<IconoEnRegla className="size-3" />}>
            Guardar adelanto
          </Boton>
        </BarraDeAcciones>
      </form>
    </section>
  )
}

interface CampoProps {
  id: string
  etiqueta: string
  /** El ancho comunica el dato: el tipo, la fecha y el importe son cortos; la persona y el motivo, largos. */
  ancho: string
  error: string | null
  ayuda?: string
  children: ReactNode
}

/** Un campo con la anatomía `.campo` de los formularios: etiqueta, ayuda, control y error debajo. */
function CampoDeAdelanto({ id, etiqueta, ancho, error, ayuda, children }: CampoProps) {
  return (
    <div className={cn('campo', error !== null && 'con-error', ancho)}>
      <label htmlFor={id}>{etiqueta}</label>
      {ayuda !== undefined && (
        <p id={`ayuda-${id}`} className="m-0 text-[11.5px] text-faint">
          {ayuda}
        </p>
      )}
      {children}
      {error !== null && (
        <p className="campo__error" id={`error-${id}`}>
          {error}
        </p>
      )}
    </div>
  )
}
