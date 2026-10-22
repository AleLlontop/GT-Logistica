import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import type { Persona } from '../../../compartido/tipos'
import { formatearFecha, formatearInstante } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import type { AdelantoListado } from '../../adelantos/servicios/servicioAdelantos'
import type { CajaListado } from '../../caja/servicios/servicioCaja'
import { historialDeAdelantos, historialDeCajas, obtenerEmpleado } from '../servicios/servicioEmpleados'

export function DetalleEmpleado() {
  const { id } = useParams<{ id: string }>()
  const [empleado, setEmpleado] = useState<Persona | null>(null)
  const [adelantos, setAdelantos] = useState<AdelantoListado[]>([])
  const [cajas, setCajas] = useState<CajaListado[]>([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) return
    let vigente = true
    Promise.all([obtenerEmpleado(Number(id)), historialDeAdelantos(Number(id)), historialDeCajas(Number(id))])
      .then(([persona, historialAdelantos, historialCajas]) => {
        if (!vigente) return
        setEmpleado(persona)
        setAdelantos(historialAdelantos)
        setCajas(historialCajas)
      })
      .catch(() => { if (vigente) setError('No pudimos cargar el detalle o sus historiales. Verificá tus permisos.') })
      .finally(() => { if (vigente) setCargando(false) })
    return () => { vigente = false }
  }, [id])

  if (cargando) return <p role="status">Cargando ficha…</p>
  if (error || !empleado) return <p role="alert">{error ?? 'No encontramos ese empleado.'}</p>

  return (
    <section className="space-y-6">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div><h1 className="text-2xl font-semibold">{empleado.apellido}, {empleado.nombre}</h1><p className="text-sm text-ink-soft">Ficha de empleado</p></div>
        <Link className="underline" to={`/empleados/${empleado.id}/editar`}>Modificar empleado</Link>
      </header>

      <section className="rounded-md border p-4"><h2 className="mb-3 text-lg font-semibold">Datos personales</h2>
        <dl className="grid gap-3 sm:grid-cols-2">
          <div><dt className="text-sm text-ink-soft">DNI</dt><dd>{empleado.dni}</dd></div>
          <div><dt className="text-sm text-ink-soft">Estado</dt><dd>{empleado.activa ? 'Activo' : 'Dado de baja'}</dd></div>
          <div><dt className="text-sm text-ink-soft">Teléfono</dt><dd>{empleado.telefono}</dd></div>
          <div><dt className="text-sm text-ink-soft">Email</dt><dd>{empleado.email}</dd></div>
          <div><dt className="text-sm text-ink-soft">Fecha de nacimiento</dt><dd>{formatearFecha(empleado.fechaNacimiento)}</dd></div>
        </dl>
      </section>

      <section className="rounded-md border p-4"><h2 className="mb-3 text-lg font-semibold">Historial de adelantos</h2>
        {adelantos.length === 0 ? <p>No hay adelantos registrados para este empleado.</p> : <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr><th className="p-2">Fecha</th><th className="p-2">Motivo</th><th className="p-2">Importe</th><th className="p-2">Estado</th></tr></thead><tbody>{adelantos.map(a => <tr className="border-t" key={a.id}><td className="p-2">{formatearFecha(a.fecha)}</td><td className="p-2">{a.motivo}</td><td className="p-2">{formatearPesos(a.importe)}</td><td className="p-2">{a.estado}</td></tr>)}</tbody></table></div>}
        <p className="mt-3 text-sm"><Link className="underline" to="/adelantos">Ir al módulo de adelantos</Link></p>
      </section>

      <section className="rounded-md border p-4"><h2 className="mb-3 text-lg font-semibold">Cajas a cargo</h2>
        {cajas.length === 0 ? <p>No hay cajas asociadas al usuario de este empleado.</p> : <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr><th className="p-2">Apertura</th><th className="p-2">Estado</th><th className="p-2">Cierre</th><th className="p-2">Saldo final</th></tr></thead><tbody>{cajas.map(c => <tr className="border-t" key={c.id}><td className="p-2">{formatearInstante(c.fechaApertura)}</td><td className="p-2">{c.estado}</td><td className="p-2">{c.fechaCierre ? formatearInstante(c.fechaCierre) : '—'}</td><td className="p-2">{c.saldoFinal === null ? '—' : formatearPesos(c.saldoFinal)}</td></tr>)}</tbody></table></div>}
      </section>
    </section>
  )
}