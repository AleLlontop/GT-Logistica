import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import type { Persona } from '../../../compartido/tipos'
import { crearEmpleado, modificarEmpleado, obtenerEmpleado, type DatosEmpleado } from '../servicios/servicioEmpleados'

const VACIO: DatosEmpleado = {
  nombre: '', apellido: '', dni: '', telefono: '', email: '', fechaNacimiento: '',
}

export function FormularioEmpleado() {
  const { id } = useParams<{ id: string }>()
  const navegar = useNavigate()
  const esEdicion = id !== undefined
  const [datos, setDatos] = useState<DatosEmpleado>(VACIO)
  const [cargando, setCargando] = useState(esEdicion)
  const [guardando, setGuardando] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!esEdicion || id === undefined) return
    let vigente = true
    obtenerEmpleado(Number(id)).then((persona: Persona) => {
      if (!vigente) return
      if (persona.tipo !== 'empleado') throw new Error('El registro no corresponde a un empleado.')
      setDatos({ nombre: persona.nombre, apellido: persona.apellido, dni: persona.dni,
        telefono: persona.telefono, email: persona.email, fechaNacimiento: persona.fechaNacimiento })
    }).catch(() => { if (vigente) setError('No pudimos cargar los datos del empleado.') })
      .finally(() => { if (vigente) setCargando(false) })
    return () => { vigente = false }
  }, [esEdicion, id])

  function cambiar(campo: keyof DatosEmpleado, valor: string) {
    setDatos((actual) => ({ ...actual, [campo]: valor }))
  }

  async function guardar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setGuardando(true)
    try {
      if (esEdicion && id !== undefined) await modificarEmpleado(Number(id), datos)
      else await crearEmpleado(datos)
      navegar('/empleados')
    } catch (fallo) {
      setError(fallo instanceof Error ? fallo.message : 'No pudimos guardar al empleado.')
    } finally {
      setGuardando(false)
    }
  }

  if (cargando) return <p role="status">Cargando empleado…</p>

  return (
    <section className="max-w-2xl space-y-5">
      <h1 className="text-2xl font-semibold">{esEdicion ? 'Modificar empleado' : 'Registrar empleado'}</h1>
      {error && <p role="alert" className="rounded-md border border-red-300 p-3 text-red-700">{error}</p>}
      <form className="space-y-4" onSubmit={guardar}>
        <div><label className="mb-1 block" htmlFor="nombre">Nombre *</label><input id="nombre" className="w-full rounded-md border px-3 py-2" required maxLength={100} value={datos.nombre} onChange={e => cambiar('nombre', e.target.value)} /></div>
        <div><label className="mb-1 block" htmlFor="apellido">Apellido *</label><input id="apellido" className="w-full rounded-md border px-3 py-2" required maxLength={100} value={datos.apellido} onChange={e => cambiar('apellido', e.target.value)} /></div>
        <div><label className="mb-1 block" htmlFor="dni">DNI *</label><input id="dni" className="w-full rounded-md border px-3 py-2" required minLength={7} maxLength={15} pattern="[0-9]+" inputMode="numeric" value={datos.dni} onChange={e => cambiar('dni', e.target.value)} /></div>
        <div><label className="mb-1 block" htmlFor="telefono">Teléfono *</label><input id="telefono" className="w-full rounded-md border px-3 py-2" required maxLength={30} value={datos.telefono} onChange={e => cambiar('telefono', e.target.value)} /></div>
        <div><label className="mb-1 block" htmlFor="email">Email *</label><input id="email" type="email" className="w-full rounded-md border px-3 py-2" required maxLength={254} value={datos.email} onChange={e => cambiar('email', e.target.value)} /></div>
        <div><label className="mb-1 block" htmlFor="fechaNacimiento">Fecha de nacimiento *</label><input id="fechaNacimiento" type="date" className="w-full rounded-md border px-3 py-2" required value={datos.fechaNacimiento} onChange={e => cambiar('fechaNacimiento', e.target.value)} /></div>
        <p className="text-sm text-ink-soft">El tipo queda fijado como Empleado en el servidor; no se puede convertir un chofer desde este formulario.</p>
        <div className="flex gap-3"><button className="rounded-md bg-primary px-4 py-2 text-white" disabled={guardando} type="submit">{guardando ? 'Guardando…' : 'Guardar'}</button><button className="rounded-md border px-4 py-2" type="button" onClick={() => navegar('/empleados')}>Cancelar</button></div>
      </form>
    </section>
  )
}