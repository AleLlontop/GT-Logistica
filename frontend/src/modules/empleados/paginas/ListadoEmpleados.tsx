import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
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
import { darDeBajaEmpleado, listarEmpleados } from '../servicios/servicioEmpleados'

interface Props {
  puedeGestionar: boolean
}

const MENSAJE_ERROR_LISTADO =
  'No pudimos consultar los empleados. Verificá tus permisos e intentá de nuevo.'

export function ListadoEmpleados({ puedeGestionar }: Props) {
  const [texto, setTexto] = useState('')
  const [empleados, setEmpleados] = useState<Persona[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [cargando, setCargando] = useState(true)
  const [empleadoPendienteDeBaja, setEmpleadoPendienteDeBaja] = useState<Persona | null>(null)
  const [dandoDeBaja, setDandoDeBaja] = useState(false)

  const cargar = useCallback(async () => {
    setCargando(true)
    setError(null)

    try {
      const resultado = await listarEmpleados({ texto: texto.trim() })
      setEmpleados(resultado)
    } catch {
      setError(MENSAJE_ERROR_LISTADO)
    } finally {
      setCargando(false)
    }
  }, [texto])

  useEffect(() => {
    void cargar()
  }, [cargar])

  async function confirmarBaja() {
    if (!puedeGestionar || empleadoPendienteDeBaja === null || dandoDeBaja) return

    setDandoDeBaja(true)
    setError(null)

    try {
      await darDeBajaEmpleado(empleadoPendienteDeBaja.id)
      setEmpleadoPendienteDeBaja(null)
      await cargar()
    } catch (fallo) {
      // ErrorHttp hereda de Error y preserva el mensaje de negocio del servidor (por ejemplo,
      // empleado_con_caja_abierta). No convertir un 409 en una baja exitosa ni ocultarlo.
      setError(fallo instanceof Error ? fallo.message : 'No pudimos dar de baja al empleado.')
      setEmpleadoPendienteDeBaja(null)
    } finally {
      setDandoDeBaja(false)
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
                className="flex size-8 shrink-0 items-center justify-center rounded-pastilla bg-white/[0.14] transition-transform duration-200 ease-gt group-hover:translate-x-0.5"
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
                          {empleado.activa && (
                            <Boton
                              variante="destructivo"
                              tamanio="chico"
                              onClick={() => setEmpleadoPendienteDeBaja(empleado)}
                            >
                              Dar de baja
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
    </section>
  )
}
