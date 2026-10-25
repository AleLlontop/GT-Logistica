import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import type { Persona } from '../../../compartido/tipos'
import { Aviso } from '../../../compartido/ui/Aviso'
import { Boton } from '../../../compartido/ui/Boton'
import { DialogoConfirmacion } from '../../../compartido/ui/DialogoConfirmacion'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { Filtros, FranjaDeBusqueda } from '../../../compartido/ui/Filtros'
import { IconoNuevo } from '../../../compartido/ui/iconos'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import {
  clasesDeBoton,
  clasesDeEtiquetaDeFiltro,
  clasesDeFiltro,
} from '../../../compartido/ui/clases'
import {
  darDeBajaEmpleado,
  listarEmpleados,
  reactivarEmpleado,
} from '../servicios/servicioEmpleados'

interface Props {
  puedeGestionar: boolean
}

const MENSAJE_ERROR_LISTADO =
  'No pudimos consultar los empleados. Volvé a intentar en unos minutos.'

// [010]: un 403 dice que falta permiso y a quién pedírselo; "volvé a intentar" sería falso.
const MENSAJE_SIN_PERMISO =
  'No tenés permiso para ver los empleados. Si lo necesitás, pedíselo a quien administra los usuarios del sistema.'

export function ListadoEmpleados({ puedeGestionar }: Props) {
  const [texto, setTexto] = useState('')
  const [empleados, setEmpleados] = useState<Persona[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [cargando, setCargando] = useState(true)
  const [empleadoPendienteDeBaja, setEmpleadoPendienteDeBaja] = useState<Persona | null>(null)
  const [dandoDeBaja, setDandoDeBaja] = useState(false)
  const [empleadoPendienteDeReactivar, setEmpleadoPendienteDeReactivar] = useState<Persona | null>(
    null,
  )
  const [reactivando, setReactivando] = useState(false)
  const [confirmacion, setConfirmacion] = useState<string | null>(null)

  // [015]: cada tecla lanza una consulta, y sólo la última puede mostrar su resultado o su error.
  // Sin esto, una respuesta lenta de "Pé" pisaría la de "Pérez" que llegó antes.
  const ultimaConsulta = useRef(0)

  const cargar = useCallback(async () => {
    const consulta = ++ultimaConsulta.current
    setCargando(true)
    setError(null)

    try {
      const resultado = await listarEmpleados({ texto: texto.trim() })
      if (consulta !== ultimaConsulta.current) return
      setEmpleados(resultado)
    } catch (fallo) {
      if (consulta !== ultimaConsulta.current) return
      setError(
        fallo instanceof ErrorHttp && fallo.estado === 403
          ? MENSAJE_SIN_PERMISO
          : MENSAJE_ERROR_LISTADO,
      )
    } finally {
      if (consulta === ultimaConsulta.current) setCargando(false)
    }
  }, [texto])

  useEffect(() => {
    void cargar()
  }, [cargar])

  async function confirmarBaja() {
    if (!puedeGestionar || empleadoPendienteDeBaja === null || dandoDeBaja) return

    setDandoDeBaja(true)
    setError(null)
    setConfirmacion(null)

    try {
      await darDeBajaEmpleado(empleadoPendienteDeBaja.id)
      setEmpleadoPendienteDeBaja(null)
      await cargar()
    } catch (fallo) {
      // ErrorHttp hereda de Error y preserva el mensaje de negocio del servidor (por ejemplo,
      // persona_vinculada). No convertir un rechazo en una baja exitosa ni ocultarlo.
      setError(fallo instanceof Error ? fallo.message : 'No pudimos dar de baja al empleado.')
      setEmpleadoPendienteDeBaja(null)
    } finally {
      setDandoDeBaja(false)
    }
  }

  // La confirmación la pide la pantalla porque se deshace con la baja ([005]).
  async function confirmarReactivacion() {
    if (!puedeGestionar || empleadoPendienteDeReactivar === null || reactivando) return

    const aReactivar = empleadoPendienteDeReactivar
    setReactivando(true)
    setError(null)
    setConfirmacion(null)

    try {
      await reactivarEmpleado(aReactivar.id)
      setConfirmacion(`${aReactivar.apellido}, ${aReactivar.nombre} volvió a estar activo.`)
      await cargar()
    } catch (fallo) {
      setError(fallo instanceof Error ? fallo.message : 'No pudimos reactivar al empleado.')
    } finally {
      setEmpleadoPendienteDeReactivar(null)
      setReactivando(false)
    }
  }

  const filtrando = texto.trim().length > 0

  return (
    <section>
      <EncabezadoDePantalla
        titulo="Empleados"
        resumen="Personal administrativo y operativo que no tiene ficha de chofer."
        accionPrincipal={
          puedeGestionar ? (
            <Link to="/empleados/nuevo" className={clasesDeBoton('primario')}>
              Registrar empleado
              <span
                aria-hidden="true"
                className="flex size-8 shrink-0 items-center justify-center rounded-pastilla bg-icon-overlay transition-transform duration-200 ease-gt group-hover:translate-x-0.5"
              >
                <IconoNuevo className="size-3" />
              </span>
            </Link>
          ) : undefined
        }
      />

      <Aviso tono="error" rol="alert" className="mb-[18px]">
        {error}
      </Aviso>

      <Aviso tono="exito" rol="status" className="mb-[18px]">
        {confirmacion}
      </Aviso>

      <Listado>
        <Filtros
          resumen={
            empleados !== null && error === null ? (
              <>
                <span>
                  {empleados.length} {empleados.length === 1 ? 'empleado' : 'empleados'}
                </span>
                <span>Ordenados por apellido y nombre</span>
              </>
            ) : undefined
          }
        >
          <FranjaDeBusqueda>
            <div className="flex flex-col gap-1.5">
              <label htmlFor="buscar-empleados" className={clasesDeEtiquetaDeFiltro}>
                Nombre, apellido o DNI
              </label>
              <input
                id="buscar-empleados"
                type="search"
                placeholder="Gómez o 30123456"
                value={texto}
                onChange={(evento) => setTexto(evento.target.value)}
                className={clasesDeFiltro(filtrando)}
              />
            </div>
          </FranjaDeBusqueda>
        </Filtros>

        {cargando && empleados === null && (
          <EstadoVacio caso="cargando" className="border-0 shadow-none">
            Cargando empleados…
          </EstadoVacio>
        )}

        {!cargando && error === null && empleados !== null && empleados.length === 0 && (
          <EstadoVacio
            caso={filtrando ? 'sinCoincidencias' : 'vacio'}
            className="border-0 shadow-none"
          >
            {filtrando
              ? 'No hay empleados que coincidan con la búsqueda.'
              : 'Todavía no hay empleados registrados.'}
          </EstadoVacio>
        )}

        {empleados !== null && empleados.length > 0 && (
          <TablaDesplazable>
            <table>
              <caption>Empleados registrados</caption>
              <thead>
                <tr>
                  <th scope="col">Empleado</th>
                  <th scope="col">DNI</th>
                  <th scope="col">Teléfono</th>
                  <th scope="col">Email</th>
                  <th scope="col">Estado</th>
                  {puedeGestionar && <th scope="col">Acciones</th>}
                </tr>
              </thead>
              <tbody>
                {empleados.map((empleado) => (
                  <tr key={empleado.id}>
                    <td>
                      <Link
                        to={`/empleados/${empleado.id}`}
                        className="font-semibold text-ink underline decoration-line-strong underline-offset-4 hover:decoration-ink"
                      >
                        {empleado.apellido}, {empleado.nombre}
                      </Link>
                    </td>
                    <td className="whitespace-nowrap">{empleado.dni}</td>
                    <td>{empleado.telefono}</td>
                    <td className="max-w-[18rem] truncate">{empleado.email}</td>
                    <td>
                      <span
                        className={
                          empleado.activa
                            ? 'inline-flex rounded-pastilla bg-estado-rendido-bg px-2.5 py-1 text-[11px] font-bold text-estado-rendido'
                            : 'inline-flex rounded-pastilla bg-surface-mute px-2.5 py-1 text-[11px] font-bold text-ink-soft'
                        }
                      >
                        {empleado.activa ? 'Activo' : 'Dado de baja'}
                      </span>
                    </td>
                    {puedeGestionar && (
                      <td>
                        <div className="flex flex-wrap items-center gap-2">
                          <Link
                            to={`/empleados/${empleado.id}/editar`}
                            className={clasesDeBoton('secundario', 'chico')}
                          >
                            Editar
                          </Link>
                          {/* Dar de baja y Reactivar no conviven. */}
                          {empleado.activa ? (
                            <Boton
                              variante="destructivo"
                              tamanio="chico"
                              onClick={() => setEmpleadoPendienteDeBaja(empleado)}
                            >
                              Dar de baja
                            </Boton>
                          ) : (
                            <Boton
                              variante="secundario"
                              tamanio="chico"
                              onClick={() => setEmpleadoPendienteDeReactivar(empleado)}
                            >
                              Reactivar
                            </Boton>
                          )}
                        </div>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </TablaDesplazable>
        )}
      </Listado>

      {empleadoPendienteDeBaja !== null && (
        <DialogoConfirmacion
          titulo="Dar de baja el empleado"
          mensaje={`¿Confirmás la baja de ${empleadoPendienteDeBaja.apellido}, ${empleadoPendienteDeBaja.nombre}? El registro se conservará y quedará inactivo.`}
          etiquetaConfirmar={dandoDeBaja ? 'Dando de baja…' : 'Dar de baja'}
          onConfirmar={() => void confirmarBaja()}
          onCancelar={() => {
            if (!dandoDeBaja) setEmpleadoPendienteDeBaja(null)
          }}
        />
      )}

      {empleadoPendienteDeReactivar !== null && (
        <DialogoConfirmacion
          titulo="Reactivar el empleado"
          mensaje={`¿Confirmás la reactivación de ${empleadoPendienteDeReactivar.apellido}, ${empleadoPendienteDeReactivar.nombre}?`}
          etiquetaConfirmar={reactivando ? 'Reactivando…' : 'Reactivar'}
          onConfirmar={() => void confirmarReactivacion()}
          onCancelar={() => {
            if (!reactivando) setEmpleadoPendienteDeReactivar(null)
          }}
        />
      )}
    </section>
  )
}
