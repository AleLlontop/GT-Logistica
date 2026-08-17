import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { formatearFecha } from '../../../compartido/fechas'
import {
  listarClientes,
  type Cliente,
} from '../../viajes/clientes/servicioClientes'
import { ConfirmacionesDeEmision } from '../componentes/ConfirmacionesDeEmision'
import { ResumenDeImportes } from '../componentes/ResumenDeImportes'
import { SelectorDeViajes } from '../componentes/SelectorDeViajes'
import { VistaPreviaDocumento } from '../componentes/VistaPreviaDocumento'
import {
  CodigosErrorFacturas,
  detalleDeError,
  NOMBRES_DE_CONDICION_DE_VENTA,
  NOMBRES_DE_TIPO_COMPROBANTE,
  NOMBRES_DE_TIPO_FACTURACION,
  type CondicionDeVenta,
  type FacturaResumen,
  type MotivoConfirmacion,
  type TipoComprobante,
  type TipoFacturacion,
} from '../servicios/api'
import {
  emitirFactura,
  listarAnuladasSinReemplazo,
  listarFacturables,
  obtenerEmpresaEmisoraParaAlta,
  type EmisionPeticion,
  type ViajeFacturable,
} from '../servicios/servicioFacturas'

const MENSAJE_SIN_CLIENTES_ACTIVOS =
  'No hay clientes activos en el padrón. Registrá o reactivá un cliente en el Módulo de viajes para ' +
  'poder emitirle una factura.'

/** Los años que el sistema acepta hoy. La lista se amplía con el tiempo (FR-010). */
const ANIOS = [2025, 2026]

const MESES = Array.from({ length: 12 }, (_, indice) => indice + 1)

/** Nombre del mes para el mensaje de lista vacía, que dice "en agosto de 2026". */
const NOMBRES_DE_MES = [
  'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
  'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre',
]

/**
 * Alta de factura: la pantalla central del módulo (User Story 2).
 *
 * Cuatro bloques en orden: **datos del comprobante**, **selección de viajes**, **importes** y **vista
 * previa** (contracts/README §Alta de factura).
 *
 * **Al emitir el formulario no queda en pantalla**: se navega a la ficha de la factura recién creada y
 * la confirmación viaja con la navegación (FR-014, convención [005]). Un alta que sigue ahí con el botón
 * habilitado invita a apretarlo de nuevo, y el segundo intento choca contra el número que acaba de usar
 * el primero — con un mensaje que nombra como culpable a la factura propia.
 *
 * **Los tres importes son de sólo lectura y no hay campo donde escribirlos** (FR-024). El bloque los
 * muestra en vivo, pero el valor que se guarda es el que calcula el backend a partir de los viajes que
 * encontró en la base.
 */
export function AltaFactura() {
  const navegar = useNavigate()
  const hoy = new Date()

  // ── Bloque 1: datos del comprobante ────────────────────────────────────────────────────────────
  const [clientes, setClientes] = useState<Cliente[]>([])
  const [clienteId, setClienteId] = useState<number | ''>('')
  const [tipoComprobante, setTipoComprobante] = useState<TipoComprobante>('facturaA')
  const [tipoFacturacion, setTipoFacturacion] = useState<TipoFacturacion>('original')
  const [facturaReemplazadaId, setFacturaReemplazadaId] = useState<number | ''>('')
  const [anuladas, setAnuladas] = useState<FacturaResumen[]>([])
  const [condicionDeVenta, setCondicionDeVenta] = useState<CondicionDeVenta>('cuentaCorriente')
  const [mes, setMes] = useState(hoy.getMonth() + 1)
  const [anio, setAnio] = useState(
    ANIOS.includes(hoy.getFullYear()) ? hoy.getFullYear() : ANIOS[ANIOS.length - 1],
  )

  const fechaDeHoy = enIso(hoy)

  const [fecha, setFecha] = useState(fechaDeHoy)
  const [numeroComprobante, setNumeroComprobante] = useState('')
  const [cae, setCae] = useState('')
  const [caeVencimiento, setCaeVencimiento] = useState('')
  const [vencimientoPago, setVencimientoPago] = useState(sumarDias(fechaDeHoy, 30))
  const [detalle, setDetalle] = useState('')

  // ── Bloque 2: viajes ──────────────────────────────────────────────────────────────────────────
  const [viajes, setViajes] = useState<ViajeFacturable[]>([])
  const [seleccionados, setSeleccionados] = useState<Set<number>>(new Set())
  const [cargandoViajes, setCargandoViajes] = useState(false)

  // ── Estado de la pantalla ─────────────────────────────────────────────────────────────────────
  const [avisoDeEmisora, setAvisoDeEmisora] = useState<string | null>(null)
  const [emitiendo, setEmitiendo] = useState(false)
  const [errorGlobal, setErrorGlobal] = useState<string | null>(null)
  const [erroresDeCampo, setErroresDeCampo] = useState<Record<string, string>>({})
  const [confirmacion, setConfirmacion] = useState<
    { motivo: MotivoConfirmacion; mensaje: string } | null
  >(null)

  // Sólo los activos: no se le puede facturar a un cliente dado de baja (FR-011).
  useEffect(() => {
    listarClientes({ soloActivos: true, busqueda: '' }, 1)
      .then((pagina) => setClientes(pagina.items))
      .catch(() => setErrorGlobal('No pudimos traer el padrón de clientes.'))
  }, [])

  // La empresa emisora se consulta al entrar para avisar **antes** de completar todo el formulario. El
  // rechazo del servidor al guardar sigue existiendo: esto es una cortesía, no la validación (FR-006).
  useEffect(() => {
    obtenerEmpresaEmisoraParaAlta()
      .then((empresa) => {
        if (!empresa.configurada) {
          setAvisoDeEmisora(
            `Falta configurar la empresa emisora: ${empresa.faltantes.join(', ')}. Cargalos en ` +
              'Empresa emisora para poder emitir.',
          )
        }
      })
      .catch(() => {
        // Si la consulta falla, el alta sigue disponible y el rechazo llega al guardar.
      })
  }, [])

  const traerFacturables = useCallback(() => {
    if (clienteId === '') {
      setViajes([])
      setSeleccionados(new Set())

      return
    }

    setCargandoViajes(true)

    listarFacturables(clienteId, mes, anio)
      .then((encontrados) => {
        setViajes(encontrados)
        // La selección se descarta al cambiar de cliente o de período: conservarla dejaría marcados
        // viajes que ya no están en la lista.
        setSeleccionados(new Set())
      })
      .catch(() => setErrorGlobal('No pudimos traer los viajes facturables.'))
      .finally(() => setCargandoViajes(false))
  }, [clienteId, mes, anio])

  useEffect(() => {
    traerFacturables()
  }, [traerFacturables])

  // El desplegable de la Refacturación ofrece **sólo** las anuladas de ese cliente que nadie refacturó
  // (FR-049, FR-049a). Con `Original` no se pide nada.
  useEffect(() => {
    if (tipoFacturacion !== 'refacturacion' || clienteId === '') {
      setAnuladas([])
      setFacturaReemplazadaId('')

      return
    }

    listarAnuladasSinReemplazo(clienteId)
      .then(setAnuladas)
      .catch(() => setErrorGlobal('No pudimos traer las facturas anuladas del cliente.'))
  }, [tipoFacturacion, clienteId])

  const importesSeleccionados = useMemo(
    () => viajes.filter((viaje) => seleccionados.has(viaje.id)).map((viaje) => viaje.importe),
    [viajes, seleccionados],
  )

  const peticion: EmisionPeticion | null = useMemo(() => {
    if (clienteId === '' || seleccionados.size === 0) return null
    if (numeroComprobante.trim() === '' || cae.trim() === '') return null
    if (fecha === '' || caeVencimiento === '' || vencimientoPago === '') return null

    return {
      clienteId,
      tipoComprobante,
      tipoFacturacion,
      condicionDeVenta,
      mes,
      anio,
      fecha,
      numeroComprobante: numeroComprobante.trim(),
      detalle: detalle.trim() === '' ? null : detalle.trim(),
      cae: cae.trim(),
      caeVencimiento,
      vencimientoPago,
      facturaReemplazadaId: facturaReemplazadaId === '' ? null : facturaReemplazadaId,
      viajeIds: [...seleccionados],
    }
  }, [
    clienteId, seleccionados, numeroComprobante, cae, fecha, caeVencimiento, vencimientoPago,
    tipoComprobante, tipoFacturacion, condicionDeVenta, mes, anio, detalle, facturaReemplazadaId,
  ])

  async function emitir(confirmado: boolean) {
    if (peticion === null) return

    setEmitiendo(true)
    setErrorGlobal(null)
    setErroresDeCampo({})

    try {
      const emitida = await emitirFactura(confirmado ? { ...peticion, confirmado } : peticion)

      // FR-014: **el formulario no queda en pantalla.** La confirmación viaja con la navegación y se
      // anuncia en la ficha con `role="status"` (convención [005]).
      navegar(`/facturas/${emitida.id}`, {
        state: {
          aviso:
            `Se emitió la factura ${emitida.numeroComprobante}. Sus ${emitida.viajes.length} ` +
            'viajes quedaron en estado facturado.',
        },
      })
    } catch (fallo) {
      const detalleDelError = detalleDeError(fallo)

      if (detalleDelError === null) {
        setErrorGlobal('Ocurrió un problema inesperado. Volvé a intentar en unos minutos.')

        return
      }

      // FR-032: el servidor no creó nada y pide confirmación. Se abre el diálogo que corresponda y el
      // reintento lleva `confirmado: true`.
      if (
        detalleDelError.codigo === CodigosErrorFacturas.emisionRequiereConfirmacion &&
        detalleDelError.motivoConfirmacion
      ) {
        setConfirmacion({
          motivo: detalleDelError.motivoConfirmacion,
          mensaje: detalleDelError.mensaje,
        })

        return
      }

      setErrorGlobal(detalleDelError.mensaje)

      if (detalleDelError.campo) {
        setErroresDeCampo({ [detalleDelError.campo]: 'Valor inválido o requerido.' })
      }

      // La lista quedó desactualizada: otro operador facturó uno de los viajes elegidos (FR-053).
      if (detalleDelError.codigo === CodigosErrorFacturas.viajeYaFacturado) {
        traerFacturables()
      }
    } finally {
      setEmitiendo(false)
    }
  }

  function classNameCampo(campo: string) {
    return `campo ${erroresDeCampo[campo] ? 'con-error' : ''}`
  }

  const clienteElegido = clientes.find((cliente) => cliente.id === clienteId)

  if (clientes.length === 0 && errorGlobal === null) {
    return (
      <main>
        <h1>Nueva factura</h1>
        <p role="status">{MENSAJE_SIN_CLIENTES_ACTIVOS}</p>
      </main>
    )
  }

  return (
    <main>
      <h1>Nueva factura</h1>

      {avisoDeEmisora !== null && <p role="alert">{avisoDeEmisora}</p>}
      {errorGlobal !== null && <p role="alert">{errorGlobal}</p>}

      <form
        onSubmit={(evento: FormEvent) => {
          evento.preventDefault()
          emitir(false)
        }}
        noValidate
      >
        {/* ── Bloque 1 ────────────────────────────────────────────────────────────────────────── */}
        <section aria-labelledby="titulo-comprobante">
          <h2 id="titulo-comprobante">Datos del comprobante</h2>

          <div className={classNameCampo('clienteId')}>
            <label htmlFor="clienteId">Cliente</label>
            <select
              id="clienteId"
              required
              value={clienteId}
              onChange={(evento) =>
                setClienteId(evento.target.value === '' ? '' : Number(evento.target.value))
              }
              aria-invalid={erroresDeCampo.clienteId !== undefined}
            >
              <option value="">Elegí un cliente</option>
              {clientes.map((cliente) => (
                <option key={cliente.id} value={cliente.id}>
                  {cliente.razonSocial} — {cliente.cuit}
                </option>
              ))}
            </select>
          </div>

          <div className={classNameCampo('tipoComprobante')}>
            <label htmlFor="tipoComprobante">Tipo de comprobante</label>
            <select
              id="tipoComprobante"
              value={tipoComprobante}
              onChange={(evento) => setTipoComprobante(evento.target.value as TipoComprobante)}
            >
              {Object.entries(NOMBRES_DE_TIPO_COMPROBANTE).map(([valor, nombre]) => (
                <option key={valor} value={valor}>
                  {nombre}
                </option>
              ))}
            </select>
          </div>

          <div className={classNameCampo('tipoFacturacion')}>
            <label htmlFor="tipoFacturacion">Tipo de facturación</label>
            <select
              id="tipoFacturacion"
              value={tipoFacturacion}
              onChange={(evento) => setTipoFacturacion(evento.target.value as TipoFacturacion)}
            >
              {Object.entries(NOMBRES_DE_TIPO_FACTURACION).map(([valor, nombre]) => (
                <option key={valor} value={valor}>
                  {nombre}
                </option>
              ))}
            </select>
          </div>

          {/* Sólo con `Refacturación`. Con `Original` no aparece: una Original no reemplaza a nadie
              (FR-049). */}
          {tipoFacturacion === 'refacturacion' && (
            <div className={classNameCampo('facturaReemplazadaId')}>
              <label htmlFor="facturaReemplazadaId">Factura que reemplaza</label>
              <select
                id="facturaReemplazadaId"
                required
                value={facturaReemplazadaId}
                onChange={(evento) =>
                  setFacturaReemplazadaId(
                    evento.target.value === '' ? '' : Number(evento.target.value),
                  )
                }
                aria-invalid={erroresDeCampo.facturaReemplazadaId !== undefined}
              >
                <option value="">Elegí la factura anulada que reemplaza</option>
                {anuladas.map((anulada) => (
                  <option key={anulada.id} value={anulada.id}>
                    {anulada.numeroComprobante} — {formatearFecha(anulada.fecha)}
                  </option>
                ))}
              </select>

              {anuladas.length === 0 && clienteId !== '' && (
                <p role="status">
                  Ese cliente no tiene facturas anuladas sin refacturar. Una Refacturación reemplaza a
                  una factura anulada.
                </p>
              )}
            </div>
          )}

          <div className={classNameCampo('condicionDeVenta')}>
            <label htmlFor="condicionDeVenta">Condición de venta</label>
            <select
              id="condicionDeVenta"
              value={condicionDeVenta}
              onChange={(evento) => setCondicionDeVenta(evento.target.value as CondicionDeVenta)}
            >
              {Object.entries(NOMBRES_DE_CONDICION_DE_VENTA).map(([valor, nombre]) => (
                <option key={valor} value={valor}>
                  {nombre}
                </option>
              ))}
            </select>
          </div>

          <div className={classNameCampo('mes')}>
            <label htmlFor="mes">Mes</label>
            <select id="mes" value={mes} onChange={(evento) => setMes(Number(evento.target.value))}>
              {MESES.map((numero) => (
                <option key={numero} value={numero}>
                  {String(numero).padStart(2, '0')}
                </option>
              ))}
            </select>
          </div>

          <div className={classNameCampo('anio')}>
            <label htmlFor="anio">Año</label>
            <select
              id="anio"
              value={anio}
              onChange={(evento) => setAnio(Number(evento.target.value))}
            >
              {ANIOS.map((numero) => (
                <option key={numero} value={numero}>
                  {numero}
                </option>
              ))}
            </select>
          </div>

          <div className={classNameCampo('fecha')}>
            {/* Propuesta en hoy y modificable (FR-012). */}
            <label htmlFor="fecha">Fecha de facturación</label>
            <input
              id="fecha"
              type="date"
              required
              value={fecha}
              onChange={(evento) => {
                setFecha(evento.target.value)

                // El vencimiento de pago se propone en fecha + 30 días, y sigue a la fecha mientras no
                // se lo haya tocado a mano (spec §Assumptions).
                if (evento.target.value !== '') {
                  setVencimientoPago(sumarDias(evento.target.value, 30))
                }
              }}
              aria-invalid={erroresDeCampo.fecha !== undefined}
            />
          </div>

          <div className={classNameCampo('numeroComprobante')}>
            <label htmlFor="numeroComprobante">Número de comprobante</label>
            <input
              id="numeroComprobante"
              type="text"
              required
              maxLength={13}
              placeholder="0000-00000000"
              value={numeroComprobante}
              onChange={(evento) => setNumeroComprobante(evento.target.value)}
              aria-invalid={erroresDeCampo.numeroComprobante !== undefined}
              aria-describedby="ayuda-numero"
            />
            <p id="ayuda-numero">Formato 0000-00000000.</p>
            {erroresDeCampo.numeroComprobante && (
              <p className="campo__error" role="alert">
                {erroresDeCampo.numeroComprobante}
              </p>
            )}
          </div>

          <div className={classNameCampo('cae')}>
            <label htmlFor="cae">CAE</label>
            <input
              id="cae"
              type="text"
              required
              maxLength={20}
              value={cae}
              onChange={(evento) => setCae(evento.target.value)}
              aria-invalid={erroresDeCampo.cae !== undefined}
            />
          </div>

          <div className={classNameCampo('caeVencimiento')}>
            <label htmlFor="caeVencimiento">Vencimiento del CAE</label>
            <input
              id="caeVencimiento"
              type="date"
              required
              value={caeVencimiento}
              onChange={(evento) => setCaeVencimiento(evento.target.value)}
              aria-invalid={erroresDeCampo.caeVencimiento !== undefined}
            />
            {erroresDeCampo.caeVencimiento && (
              <p className="campo__error" role="alert">
                {erroresDeCampo.caeVencimiento}
              </p>
            )}
          </div>

          <div className={classNameCampo('vencimientoPago')}>
            <label htmlFor="vencimientoPago">Vencimiento de pago</label>
            <input
              id="vencimientoPago"
              type="date"
              required
              value={vencimientoPago}
              onChange={(evento) => setVencimientoPago(evento.target.value)}
              aria-invalid={erroresDeCampo.vencimientoPago !== undefined}
            />
            {erroresDeCampo.vencimientoPago && (
              <p className="campo__error" role="alert">
                {erroresDeCampo.vencimientoPago}
              </p>
            )}
          </div>

          <div className={classNameCampo('detalle')}>
            <label htmlFor="detalle">Detalle (opcional)</label>
            <textarea
              id="detalle"
              maxLength={500}
              value={detalle}
              onChange={(evento) => setDetalle(evento.target.value)}
            />
          </div>
        </section>

        {/* ── Bloque 2 ────────────────────────────────────────────────────────────────────────── */}
        <SelectorDeViajes
          viajes={viajes}
          seleccionados={seleccionados}
          cliente={clienteElegido?.razonSocial ?? 'el cliente elegido'}
          mes={NOMBRES_DE_MES[mes - 1]}
          anio={String(anio)}
          cargando={cargandoViajes}
          onCambiarSeleccion={setSeleccionados}
        />

        {/* ── Bloque 3 ────────────────────────────────────────────────────────────────────────── */}
        <ResumenDeImportes importes={importesSeleccionados} tipoComprobante={tipoComprobante} />

        {/* ── Bloque 4 ────────────────────────────────────────────────────────────────────────── */}
        <VistaPreviaDocumento peticion={peticion} />

        <div className="acciones">
          <button type="submit" disabled={emitiendo || peticion === null}>
            Emitir factura
          </button>
          <button type="button" onClick={() => navegar('/facturas')} disabled={emitiendo}>
            Cancelar
          </button>
        </div>
      </form>

      {confirmacion !== null && (
        <ConfirmacionesDeEmision
          motivo={confirmacion.motivo}
          mensaje={confirmacion.mensaje}
          trabajando={emitiendo}
          onCancelar={() => setConfirmacion(null)}
          onConfirmar={() => {
            setConfirmacion(null)
            emitir(true)
          }}
        />
      )}
    </main>
  )
}

/** Una fecha en `yyyy-MM-dd`, que es el formato con el que viaja al backend. */
function enIso(fecha: Date): string {
  return `${fecha.getFullYear()}-${String(fecha.getMonth() + 1).padStart(2, '0')}-${String(
    fecha.getDate(),
  ).padStart(2, '0')}`
}

/**
 * Suma días corridos a una fecha `yyyy-MM-dd`.
 *
 * Se construye con los tres números y no con `new Date(iso)`: eso interpreta la cadena como medianoche
 * UTC y en UTC−3 devuelve el día anterior (convención [003]).
 */
function sumarDias(iso: string, dias: number): string {
  const [anio, mes, dia] = iso.split('-').map(Number)
  const fecha = new Date(anio, mes - 1, dia)

  fecha.setDate(fecha.getDate() + dias)

  return enIso(fecha)
}
