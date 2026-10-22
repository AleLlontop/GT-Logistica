import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import type { Persona } from '../../../compartido/tipos'
import { listarEmpleados, darDeBajaEmpleado } from '../servicios/servicioEmpleados'

interface Props {
  puedeGestionar: boolean
}

export function ListadoEmpleados({ puedeGestionar }: Props) {
  const [texto, setTexto] = useState('')
  const [empleados, setEmpleados] = useState<Persona[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [cargando, setCargando] = useState(true)

  const cargar = useCallback(() => {
    setCargando(true)
    listarEmpleados({ texto })
      .then((lista) => {
        setEmpleados(lista)
        setError(null)
      })
      .catch(() => setError('No pudimos consultar los empleados. Verificá tus permisos e intentá de nuevo.'))
      .finally(() => setCargando(false))
  }, [texto])

  useEffect(() => { cargar() }, [cargar])

  async function darDeBaja(empleado: Persona) {
    if (!puedeGestionar || !window.confirm(`¿Dar de baja a ${empleado.apellido}, ${empleado.nombre}?`)) return
    try {
      await darDeBajaEmpleado(empleado.id)
      cargar()
    } catch (fallo) {
      setError(fallo instanceof Error ? fallo.message : 'No pudimos dar de baja al empleado.')
    }
  }

  return (
    <section className="space-y-5">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Empleados</h1>
          <p className="text-sm text-ink-soft">Administrativos y operativos que no tienen ficha de chofer.</p>
        </div>
        {puedeGestionar && <Link className="rounded-md bg-primary px-4 py-2 text-white" to="/empleados/nuevo">Registrar empleado</Link>}
      </header>

      <div className="max-w-xl">
        <label className="mb-1 block text-sm font-medium" htmlFor="buscar-empleados">Buscar por nombre, apellido o DNI</label>
        <input id="buscar-empleados" className="w-full rounded-md border px-3 py-2" value={texto}
          onChange={(event) => setTexto(event.target.value)} placeholder="Ej.: Pérez o 30123456" />
      </div>

      {error && <p className="rounded-md border border-red-300 p-3 text-red-700" role="alert">{error}</p>}
      {cargando && <p role="status">Cargando empleados…</p>}
      {!cargando && empleados?.length === 0 && <p role="status">No hay empleados que coincidan con la búsqueda.</p>}

      {!cargando && empleados && empleados.length > 0 && (
        <div className="overflow-x-auto rounded-md border">
          <table className="w-full text-left text-sm">
            <thead><tr className="border-b bg-surface-secondary">
              <th className="p-3">Empleado</th><th className="p-3">DNI</th><th className="p-3">Teléfono</th><th className="p-3">Email</th><th className="p-3">Estado</th><th className="p-3">Acciones</th>
            </tr></thead>
            <tbody>
              {empleados.map((empleado) => (
                <tr key={empleado.id} className="border-b last:border-0">
                  <td className="p-3"><Link className="underline" to={`/empleados/${empleado.id}`}>{empleado.apellido}, {empleado.nombre}</Link></td>
                  <td className="p-3">{empleado.dni}</td><td className="p-3">{empleado.telefono}</td><td className="p-3">{empleado.email}</td>
                  <td className="p-3">{empleado.activa ? 'Activo' : 'Dado de baja'}</td>
                  <td className="p-3"><div className="flex gap-3">
                    {puedeGestionar && <Link className="underline" to={`/empleados/${empleado.id}/editar`}>Editar</Link>}
                    {puedeGestionar && empleado.activa && <button className="underline" type="button" onClick={() => darDeBaja(empleado)}>Dar de baja</button>}
                  </div></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}