import { useEffect, useState, type FormEvent } from 'react'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import {
  crearTipoVehiculo,
  modificarTipoVehiculo,
  reactivarTipoVehiculo,
  type TipoVehiculo,
} from './servicioTiposVehiculo'

interface Props {
  /** Cuando viene, el formulario edita ese tipo en vez de crear uno nuevo. */
  enEdicion: TipoVehiculo | null
  onGuardado: (mensaje: string) => void
  onCancelar: () => void
}

/**
 * Alta y edición de un tipo de vehículo (US1).
 *
 * Sólo tiene un campo, y por eso vive junto al listado en vez de en una pantalla propia. La baja no
 * está acá: se dispara desde la fila y pide confirmación (FR-007).
 *
 * **El alta de un tipo dado de baja sí está acá**, y es deliberado: la fila de un tipo inactivo no
 * ofrece ninguna acción de estado, así que reactivarlo se pide al editarlo, que es donde ya se ve
 * cuál es el tipo elegido. Es una acción propia y no un campo del formulario: guardar el nombre
 * nunca cambia de paso el estado (FR-009).
 */
export function FormularioTipoVehiculo({ enEdicion, onGuardado, onCancelar }: Props) {
  const [nombre, setNombre] = useState(enEdicion?.nombre ?? '')
  const [errores, setErrores] = useState<Record<string, string>>({})
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null)
  const [guardando, setGuardando] = useState(false)

  useEffect(() => {
    setNombre(enEdicion?.nombre ?? '')
    setErrores({})
    setErrorGeneral(null)
  }, [enEdicion])

  async function guardar(evento: FormEvent) {
    evento.preventDefault()

    if (!nombre.trim()) {
      setErrores({ nombre: 'Completá el nombre.' })
      return
    }

    setErrores({})
    setErrorGeneral(null)
    setGuardando(true)

    try {
      if (enEdicion !== null) {
        await modificarTipoVehiculo(enEdicion.id, { nombre: nombre.trim() })
        onGuardado('Los cambios se guardaron correctamente.')
      } else {
        await crearTipoVehiculo({ nombre: nombre.trim() })
        onGuardado(`El tipo ${nombre.trim()} quedó disponible para registrar vehículos.`)
      }

      setNombre('')
    } catch (fallo) {
      // Acá cae `nombre_duplicado`, que el backend marca sobre el campo (FR-009).
      mostrarFallo(fallo)
    } finally {
      setGuardando(false)
    }
  }

  /**
   * Vuelve a poner activo el tipo que se está editando. No pide confirmación aparte: no destruye
   * nada y se deshace con la baja, que sí la pide.
   */
  async function darDeAlta() {
    if (enEdicion === null) {
      return
    }

    setErrores({})
    setErrorGeneral(null)
    setGuardando(true)

    try {
      await reactivarTipoVehiculo(enEdicion.id)
      onGuardado(
        `El tipo ${enEdicion.nombre} volvió a estar activo. Se ofrece de nuevo al registrar vehículos.`,
      )
      setNombre('')
    } catch (fallo) {
      mostrarFallo(fallo)
    } finally {
      setGuardando(false)
    }
  }

  function mostrarFallo(fallo: unknown) {
    if (fallo instanceof ErrorHttp) {
      if (fallo.detalle.campo !== undefined) {
        setErrores({ [fallo.detalle.campo]: fallo.detalle.mensaje })
      } else {
        setErrorGeneral(fallo.detalle.mensaje)
      }
    } else {
      setErrorGeneral('Ocurrió un problema inesperado. Volvé a intentar en unos minutos.')
    }
  }

  return (
    <form onSubmit={guardar} noValidate>
      <h2>{enEdicion !== null ? `Editar ${enEdicion.nombre}` : 'Nuevo tipo de vehículo'}</h2>

      {errorGeneral !== null && <p role="alert">{errorGeneral}</p>}

      {/* El estado va con su palabra y no sólo con el botón que aparece (convención [003]). */}
      {enEdicion !== null && !enEdicion.activo && (
        <p role="status">
          Este tipo está inactivo: no se ofrece al registrar vehículos. Podés darlo de alta de nuevo.
        </p>
      )}

      <div className="campo">
        <label htmlFor="nombre">Nombre</label>
        <input
          id="nombre"
          type="text"
          maxLength={100}
          value={nombre}
          onChange={(evento) => setNombre(evento.target.value)}
          required
          aria-invalid={errores.nombre !== undefined}
        />
        {errores.nombre !== undefined && (
          <p className="campo__error" role="alert">
            {errores.nombre}
          </p>
        )}
      </div>

      <div className="acciones">
        <button type="submit" disabled={guardando}>
          {enEdicion !== null ? 'Guardar cambios' : 'Cargar tipo'}
        </button>
        {enEdicion !== null && !enEdicion.activo && (
          <button type="button" onClick={darDeAlta} disabled={guardando}>
            Dar de alta
          </button>
        )}
        {enEdicion !== null && (
          <button type="button" onClick={onCancelar} disabled={guardando}>
            Cancelar
          </button>
        )}
      </div>
    </form>
  )
}
