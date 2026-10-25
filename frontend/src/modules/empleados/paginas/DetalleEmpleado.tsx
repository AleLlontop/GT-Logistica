import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import type { Persona } from '../../../compartido/tipos'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { formatearFecha, formatearInstante } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { clasesDeAvisoDePantalla, clasesDeBoton } from '../../../compartido/ui/clases'
import {
  NOMBRES_DE_ESTADO,
  type AdelantoListado,
} from '../../adelantos/servicios/servicioAdelantos'
import type { CajaListado } from '../../caja/servicios/servicioCaja'
import {
  historialDeAdelantos,
  historialDeCajas,
  obtenerEmpleado,
} from '../servicios/servicioEmpleados'

interface Props {
  puedeGestionar: boolean
}

export function DetalleEmpleado({ puedeGestionar }: Props) {
  const { id } = useParams<{ id: string }>()
  const empleadoId = Number(id)
  const [empleado, setEmpleado] = useState<Persona | null>(null)
  const [adelantos, setAdelantos] = useState<AdelantoListado[]>([])
  const [cajas, setCajas] = useState<CajaListado[]>([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!Number.isSafeInteger(empleadoId) || empleadoId <= 0) {
      setError('No encontramos ese empleado.')
      setCargando(false)
      return
    }

    let vigente = true

    // Se mantienen las tres consultas juntas: si un historial falla por permisos, no se presenta
    // falsamente como una colección vacía.
    Promise.all([
      obtenerEmpleado(empleadoId),
      historialDeAdelantos(empleadoId),
      historialDeCajas(empleadoId),
    ])
      .then(([persona, historialAdelantos, historialCajas]) => {
        if (!vigente) return
        if (persona.tipo !== 'empleado') {
          setError('El registro no corresponde a un empleado.')
          return
        }
        setEmpleado(persona)
        setAdelantos(historialAdelantos)
        setCajas(historialCajas)
      })
      .catch((fallo: unknown) => {
        if (!vigente) return
        setError(
          fallo instanceof ErrorHttp && fallo.estado === 403
            ? 'No tenés permiso para ver este empleado o alguno de sus historiales. Si lo necesitás, pedíselo a quien administra los usuarios del sistema.'
            : fallo instanceof ErrorHttp && fallo.estado === 404
              ? 'No encontramos ese empleado.'
              : 'No pudimos cargar la ficha y sus historiales. Volvé a intentar en unos minutos.',
        )
      })
      .finally(() => {
        if (vigente) setCargando(false)
      })

    return () => {
      vigente = false
    }
  }, [empleadoId])

  if (cargando) {
    return (
      <section>
        <EncabezadoDePantalla titulo="Ficha de empleado" volverA={{ ruta: '/empleados', etiqueta: 'Volver a empleados' }} />
        <EstadoVacio caso="cargando">Cargando ficha de empleado…</EstadoVacio>
      </section>
    )
  }

  if (error !== null || empleado === null) {
    return (
      <section>
        <EncabezadoDePantalla titulo="Ficha de empleado" volverA={{ ruta: '/empleados', etiqueta: 'Volver a empleados' }} />
        <p role="alert" className={clasesDeAvisoDePantalla.error}>
          {error ?? 'No encontramos ese empleado.'}
        </p>
      </section>
    )
  }

  return (
    <section>
      <EncabezadoDePantalla
        titulo={`${empleado.apellido}, ${empleado.nombre}`}
        resumen="Ficha de empleado"
        volverA={{ ruta: '/empleados', etiqueta: 'Volver a empleados' }}
        accionPrincipal={
          puedeGestionar ? (
            <Link to={`/empleados/${empleado.id}/editar`} className={clasesDeBoton('primario')}>
              Modificar empleado
            </Link>
          ) : undefined
        }
      />

      <section className="mb-6 rounded-card border border-line bg-surface p-6 shadow-card" aria-labelledby="datos-empleado-titulo">
        <h2 id="datos-empleado-titulo" className="mb-4 text-[16px] font-bold tracking-tight text-ink">Datos personales</h2>
        <dl className="grid gap-x-8 gap-y-4 sm:grid-cols-2 lg:grid-cols-3">
          <div><dt className="text-[11px] font-bold uppercase tracking-[0.12em] text-encabezado">DNI</dt><dd className="mt-1 text-[13px] text-ink">{empleado.dni}</dd></div>
          <div><dt className="text-[11px] font-bold uppercase tracking-[0.12em] text-encabezado">Estado</dt><dd className="mt-1 text-[13px] text-ink">{empleado.activa ? 'Activo' : 'Dado de baja'}</dd></div>
          <div><dt className="text-[11px] font-bold uppercase tracking-[0.12em] text-encabezado">Fecha de nacimiento</dt><dd className="mt-1 text-[13px] text-ink">{formatearFecha(empleado.fechaNacimiento)}</dd></div>
          <div><dt className="text-[11px] font-bold uppercase tracking-[0.12em] text-encabezado">Teléfono</dt><dd className="mt-1 text-[13px] text-ink">{empleado.telefono || '—'}</dd></div>
          <div><dt className="text-[11px] font-bold uppercase tracking-[0.12em] text-encabezado">Email</dt><dd className="mt-1 break-words text-[13px] text-ink">{empleado.email || '—'}</dd></div>
        </dl>
      </section>

      <section className="mb-6" aria-labelledby="historial-adelantos-titulo">
        <div className="mb-3 flex flex-wrap items-baseline justify-between gap-2">
          <h2 id="historial-adelantos-titulo" className="text-[16px] font-bold tracking-tight text-ink">Historial de adelantos</h2>
          <Link to="/adelantos" className="text-[12px] font-semibold text-ink-soft underline underline-offset-4">Ir al módulo de adelantos</Link>
        </div>
        {adelantos.length === 0 ? (
          <EstadoVacio caso="vacio">No hay adelantos registrados para este empleado.</EstadoVacio>
        ) : (
          <Listado>
            <TablaDesplazable>
              <table>
                <caption>Historial de adelantos del empleado</caption>
                <thead><tr><th scope="col">Fecha</th><th scope="col">Motivo</th><th scope="col" className="text-right">Importe</th><th scope="col">Estado</th></tr></thead>
                <tbody>{adelantos.map((adelanto) => (
                  <tr key={adelanto.id}>
                    <td className="whitespace-nowrap">{formatearFecha(adelanto.fecha)}</td>
                    <td>{adelanto.motivo}</td>
                    <td className="whitespace-nowrap text-right font-bold">{formatearPesos(adelanto.importe)}</td>
                    <td>{NOMBRES_DE_ESTADO[adelanto.estado]}</td>
                  </tr>
                ))}</tbody>
              </table>
            </TablaDesplazable>
          </Listado>
        )}
      </section>

      <section aria-labelledby="historial-cajas-titulo">
        <h2 id="historial-cajas-titulo" className="mb-3 text-[16px] font-bold tracking-tight text-ink">Cajas a cargo</h2>
        {cajas.length === 0 ? (
          <EstadoVacio caso="vacio">No hay cajas asociadas al usuario de este empleado.</EstadoVacio>
        ) : (
          <Listado>
            <TablaDesplazable>
              <table>
                <caption>Cajas asociadas al usuario del empleado</caption>
                <thead><tr><th scope="col">Apertura</th><th scope="col">Responsable</th><th scope="col">Estado</th><th scope="col">Cierre</th><th scope="col" className="text-right">Saldo final</th></tr></thead>
                <tbody>{cajas.map((caja) => (
                  <tr key={caja.id}>
                    <td className="whitespace-nowrap">{formatearInstante(caja.fechaApertura)}</td>
                    <td>{caja.responsable.nombre}</td>
                    <td>{caja.estado === 'abierta' ? 'Abierta' : 'Cerrada'}</td>
                    <td className="whitespace-nowrap">{caja.fechaCierre ? formatearInstante(caja.fechaCierre) : '—'}</td>
                    <td className="whitespace-nowrap text-right font-bold">{caja.saldoFinal === null ? '—' : formatearPesos(caja.saldoFinal)}</td>
                  </tr>
                ))}</tbody>
              </table>
            </TablaDesplazable>
          </Listado>
        )}
      </section>
    </section>
  )
}
