import { clasesDeFormulario } from '../../../compartido/ui/clases'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useEffect, useState, type FormEvent } from 'react'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { CargaDeLogo } from '../componentes/CargaDeLogo'
import { CodigosErrorFacturas } from '../servicios/api'
import {
  guardarEmpresaEmisora,
  obtenerEmpresaEmisora,
  type EmpresaEmisora as Configuracion,
} from '../servicios/servicioEmpresaEmisora'

const MENSAJE_SIN_CONFIGURAR =
  'La empresa emisora todavía no está configurada. Completá al menos la razón social, el CUIT, el ' +
  'domicilio y la condición de IVA para poder emitir facturas.'

const MENSAJE_GUARDADA = 'Los datos de la empresa emisora quedaron guardados.'

/**
 * Configuración de la empresa emisora (User Story 1).
 *
 * **Una sola pantalla, un solo formulario, un solo guardado.** No hay listado ni alta: la
 * configuración es única para todo el sistema, así que el `PUT` la crea la primera vez y la actualiza
 * siempre después (FR-001).
 *
 * **El guardado no cambia de pantalla**, y ahí está la diferencia con el resto de los formularios del
 * sistema. La convención [005] manda que un alta no quede en pantalla después de guardar, porque el
 * botón habilitado invita a apretarlo de nuevo y el segundo intento choca contra el registro que creó
 * el primero. Acá no hay tal choque: el segundo guardado **actualiza la misma fila**, que es el
 * comportamiento correcto de una configuración. Y no hay adónde navegar: esta pantalla *es* el
 * recurso. Por eso la confirmación se anuncia acá con `role="status"` (convención [003]).
 *
 * Cada rechazo marca **el campo puntual** que lo produjo, no el formulario entero: el backend devuelve
 * `campo` en el cuerpo del error y acá se traduce a `aria-invalid` más el texto al lado del control.
 */
export function EmpresaEmisora() {
  const [empresa, setEmpresa] = useState<Configuracion | null>(null)

  const [razonSocial, setRazonSocial] = useState('')
  const [cuit, setCuit] = useState('')
  const [domicilio, setDomicilio] = useState('')
  const [condicionIva, setCondicionIva] = useState('')
  const [ingresosBrutos, setIngresosBrutos] = useState('')
  const [inicioActividades, setInicioActividades] = useState('')
  const [puntoDeVenta, setPuntoDeVenta] = useState('')
  const [cbu, setCbu] = useState('')
  const [telefono, setTelefono] = useState('')
  const [email, setEmail] = useState('')

  const [cargando, setCargando] = useState(true)
  const [guardando, setGuardando] = useState(false)
  const [aviso, setAviso] = useState<string | null>(null)
  const [errorGlobal, setErrorGlobal] = useState<string | null>(null)
  const [erroresDeCampo, setErroresDeCampo] = useState<Record<string, string>>({})

  function volcar(configuracion: Configuracion) {
    setEmpresa(configuracion)
    setRazonSocial(configuracion.razonSocial ?? '')
    setCuit(configuracion.cuit ?? '')
    setDomicilio(configuracion.domicilio ?? '')
    setCondicionIva(configuracion.condicionIva ?? '')
    setIngresosBrutos(configuracion.ingresosBrutos ?? '')
    setInicioActividades(configuracion.inicioActividades ?? '')
    setPuntoDeVenta(configuracion.puntoDeVenta ?? '')
    setCbu(configuracion.cbu ?? '')
    setTelefono(configuracion.telefono ?? '')
    setEmail(configuracion.email ?? '')
  }

  useEffect(() => {
    let vigente = true

    obtenerEmpresaEmisora()
      .then((configuracion) => {
        if (vigente) volcar(configuracion)
      })
      .catch(() => {
        if (vigente) {
          setErrorGlobal('No pudimos traer los datos de la empresa emisora.')
        }
      })
      .finally(() => {
        if (vigente) setCargando(false)
      })

    return () => {
      vigente = false
    }
  }, [])

  async function guardar(evento: FormEvent) {
    evento.preventDefault()

    setGuardando(true)
    setAviso(null)
    setErrorGlobal(null)
    setErroresDeCampo({})

    try {
      volcar(
        await guardarEmpresaEmisora({
          razonSocial,
          cuit,
          domicilio,
          condicionIva,
          ingresosBrutos: opcional(ingresosBrutos),
          inicioActividades: opcional(inicioActividades),
          puntoDeVenta: opcional(puntoDeVenta),
          cbu: opcional(cbu),
          telefono: opcional(telefono),
          email: opcional(email),
        }),
      )

      setAviso(MENSAJE_GUARDADA)
    } catch (fallo) {
      if (fallo instanceof ErrorHttp) {
        setErrorGlobal(fallo.detalle.mensaje)

        // El CUIT mal formado y el email mal formado tienen código propio, así que el texto al lado
        // del campo puede decir qué está mal en vez de "valor inválido" (contracts/README).
        if (fallo.detalle.codigo === CodigosErrorFacturas.cuitInvalido) {
          setErroresDeCampo({ cuit: 'Once dígitos con verificador válido.' })
        } else if (fallo.detalle.codigo === CodigosErrorFacturas.emailInvalido) {
          setErroresDeCampo({ email: 'Formato de email inválido.' })
        } else if (fallo.detalle.campo) {
          setErroresDeCampo({ [fallo.detalle.campo]: 'Valor inválido o requerido.' })
        }
      } else {
        setErrorGlobal('Ocurrió un problema inesperado. Volvé a intentar en unos minutos.')
      }
    } finally {
      setGuardando(false)
    }
  }

  function classNameCampo(campo: string) {
    return `campo ${erroresDeCampo[campo] ? 'con-error' : ''}`
  }

  if (cargando) {
    return (
      <section>
        <EncabezadoDePantalla titulo="Empresa emisora" />
        <p role="status">Cargando…</p>
      </section>
    )
  }

  return (
    <section>
      <EncabezadoDePantalla titulo="Empresa emisora" />

      {/* Arriba del formulario vacío, con las palabras exactas del contrato (US1 esc. 1). */}
      {empresa !== null && !empresa.configurada && <p role="status">{MENSAJE_SIN_CONFIGURAR}</p>}

      <form onSubmit={guardar} noValidate className={clasesDeFormulario}>
        {/* El guardado no cambia de pantalla, así que se anuncia acá (convención [003]). */}
        {aviso !== null && <p role="status">{aviso}</p>}
        {errorGlobal !== null && <p role="alert">{errorGlobal}</p>}

        <div className={classNameCampo('razonSocial')}>
          <label htmlFor="razonSocial">Razón social</label>
          <input
            id="razonSocial"
            type="text"
            required
            maxLength={200}
            value={razonSocial}
            onChange={(evento) => setRazonSocial(evento.target.value)}
            aria-invalid={erroresDeCampo.razonSocial !== undefined}
          />
          {erroresDeCampo.razonSocial && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.razonSocial}
            </p>
          )}
        </div>

        <div className={classNameCampo('cuit')}>
          {/* Se normaliza a sólo dígitos antes de validar: `30-71234567-1` es válido (FR-002). */}
          <label htmlFor="cuit">CUIT (con o sin guiones)</label>
          <input
            id="cuit"
            type="text"
            required
            maxLength={20}
            value={cuit}
            onChange={(evento) => setCuit(evento.target.value)}
            aria-invalid={erroresDeCampo.cuit !== undefined}
          />
          {erroresDeCampo.cuit && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.cuit}
            </p>
          )}
        </div>

        <div className={classNameCampo('domicilio')}>
          <label htmlFor="domicilio">Domicilio</label>
          <input
            id="domicilio"
            type="text"
            required
            maxLength={200}
            value={domicilio}
            onChange={(evento) => setDomicilio(evento.target.value)}
            aria-invalid={erroresDeCampo.domicilio !== undefined}
          />
          {erroresDeCampo.domicilio && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.domicilio}
            </p>
          )}
        </div>

        <div className={classNameCampo('condicionIva')}>
          {/* Texto libre: la spec no enumera opciones para el emisor, a diferencia del cliente. */}
          <label htmlFor="condicionIva">Condición de IVA</label>
          <input
            id="condicionIva"
            type="text"
            required
            maxLength={100}
            value={condicionIva}
            onChange={(evento) => setCondicionIva(evento.target.value)}
            aria-invalid={erroresDeCampo.condicionIva !== undefined}
          />
          {erroresDeCampo.condicionIva && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.condicionIva}
            </p>
          )}
        </div>

        {/* Los seis opcionales. Se piden porque los seis salen impresos en el comprobante, y ninguno
            "por las dudas" (Principio V). */}
        <div className={classNameCampo('ingresosBrutos')}>
          <label htmlFor="ingresosBrutos">Número de ingresos brutos (opcional)</label>
          <input
            id="ingresosBrutos"
            type="text"
            maxLength={50}
            value={ingresosBrutos}
            onChange={(evento) => setIngresosBrutos(evento.target.value)}
            aria-invalid={erroresDeCampo.ingresosBrutos !== undefined}
          />
        </div>

        <div className={classNameCampo('inicioActividades')}>
          <label htmlFor="inicioActividades">Inicio de actividades (opcional)</label>
          <input
            id="inicioActividades"
            type="date"
            value={inicioActividades}
            onChange={(evento) => setInicioActividades(evento.target.value)}
            aria-invalid={erroresDeCampo.inicioActividades !== undefined}
          />
        </div>

        <div className={classNameCampo('puntoDeVenta')}>
          {/* Se propone en el alta de factura para armar el número de comprobante (FR-027). */}
          <label htmlFor="puntoDeVenta">Punto de venta (opcional, 4 dígitos)</label>
          <input
            id="puntoDeVenta"
            type="text"
            maxLength={4}
            value={puntoDeVenta}
            onChange={(evento) => setPuntoDeVenta(evento.target.value)}
            aria-invalid={erroresDeCampo.puntoDeVenta !== undefined}
          />
          {erroresDeCampo.puntoDeVenta && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.puntoDeVenta}
            </p>
          )}
        </div>

        <div className={classNameCampo('cbu')}>
          {/* Vacío ⇒ la banda de CBU no sale impresa en el documento (FR-031). */}
          <label htmlFor="cbu">CBU (opcional)</label>
          <input
            id="cbu"
            type="text"
            maxLength={22}
            value={cbu}
            onChange={(evento) => setCbu(evento.target.value)}
            aria-invalid={erroresDeCampo.cbu !== undefined}
          />
        </div>

        <div className={classNameCampo('telefono')}>
          <label htmlFor="telefono">Teléfono (opcional)</label>
          <input
            id="telefono"
            type="tel"
            maxLength={50}
            value={telefono}
            onChange={(evento) => setTelefono(evento.target.value)}
            aria-invalid={erroresDeCampo.telefono !== undefined}
          />
        </div>

        <div className={classNameCampo('email')}>
          <label htmlFor="email">Email (opcional)</label>
          <input
            id="email"
            type="email"
            maxLength={254}
            value={email}
            onChange={(evento) => setEmail(evento.target.value)}
            aria-invalid={erroresDeCampo.email !== undefined}
          />
          {erroresDeCampo.email && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.email}
            </p>
          )}
        </div>

        <div className="acciones">
          <button type="submit" disabled={guardando}>
            Guardar
          </button>
        </div>
      </form>

      {empresa !== null && (
        <CargaDeLogo
          empresa={empresa}
          configuracionCargada={empresa.configurada}
          onCambio={volcar}
        />
      )}
    </section>
  )
}

/** Un opcional vacío viaja como `null`, no como cadena vacía: el backend lo guarda así (FR-031). */
function opcional(valor: string): string | null {
  return valor.trim() === '' ? null : valor.trim()
}
