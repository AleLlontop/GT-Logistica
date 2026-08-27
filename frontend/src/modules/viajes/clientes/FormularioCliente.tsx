import { SeccionNumerada } from '../../../compartido/ui/SeccionNumerada'
import { BarraDeAcciones, LEYENDA_DE_OBLIGATORIOS } from '../../../compartido/ui/BarraDeAcciones'
import { Boton } from '../../../compartido/ui/Boton'
import { IconoEnRegla } from '../../../compartido/ui/iconos'
import { clasesDeFormularioAgrupado } from '../../../compartido/ui/clases'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { CodigosErrorViajes } from '../servicios/api'
import {
  crearCliente,
  modificarCliente,
  obtenerCliente,
  type ClientePeticion,
} from './servicioClientes'

/**
 * Alta y edición de un cliente (User Story 1).
 *
 * **El cuerpo no lleva `activo`**: dar de baja y dar de alta son recursos propios del listado, así
 * que corregir una razón social no puede reactivar en silencio a alguien dado de baja (FR-007).
 *
 * Cada rechazo del backend marca **el campo puntual** que lo produjo, no el formulario entero
 * (FR-004): el backend devuelve `campo` en el cuerpo del error y acá se traduce a `aria-invalid` más
 * el texto al lado del control.
 */
export function FormularioCliente() {
  const { id } = useParams()
  const navegar = useNavigate()
  const editando = id !== undefined
  const clienteId = Number(id)

  const [razonSocial, setRazonSocial] = useState('')
  const [cuit, setCuit] = useState('')
  const [telefono, setTelefono] = useState('')
  const [email, setEmail] = useState('')
  const [direccion, setDireccion] = useState('')

  const [cargando, setCargando] = useState(editando)
  const [guardando, setGuardando] = useState(false)
  const [errorGlobal, setErrorGlobal] = useState<string | null>(null)
  const [erroresDeCampo, setErroresDeCampo] = useState<Record<string, string>>({})

  useEffect(() => {
    if (!editando) return

    let vigente = true

    obtenerCliente(clienteId)
      .then((cliente) => {
        if (!vigente) return

        setRazonSocial(cliente.razonSocial)
        setCuit(cliente.cuit)
        setTelefono(cliente.telefono)
        setEmail(cliente.email)
        setDireccion(cliente.direccion ?? '')
      })
      .catch(() => {
        if (vigente) setErrorGlobal('No pudimos traer los datos del cliente.')
      })
      .finally(() => {
        if (vigente) setCargando(false)
      })

    return () => {
      vigente = false
    }
  }, [editando, clienteId])

  async function guardar(evento: FormEvent) {
    evento.preventDefault()

    setGuardando(true)
    setErrorGlobal(null)
    setErroresDeCampo({})

    const peticion: ClientePeticion = {
      razonSocial,
      cuit,
      telefono,
      email,
      direccion: direccion.trim() === '' ? null : direccion,
    }

    try {
      const guardado = editando
        ? await modificarCliente(clienteId, peticion)
        : await crearCliente(peticion)

      // La confirmación viaja al listado y se anuncia allá con `role="status"`: es un resultado que
      // aparece sin que la pantalla lo pida, y quien usa lector de pantalla tiene que enterarse
      // (convención [003]). Los textos son los de `contracts/README.md`.
      navegar('/clientes', {
        state: {
          aviso: editando
            ? `Los datos de ${guardado.razonSocial} quedaron actualizados.`
            : `El cliente ${guardado.razonSocial} quedó registrado y ya se puede elegir al cargar ` +
              'un viaje.',
        },
      })
    } catch (fallo) {
      if (fallo instanceof ErrorHttp) {
        setErrorGlobal(fallo.detalle.mensaje)

        // El CUIT de un cliente dado de baja no es un duplicado cualquiera: quien lo intenta no lo
        // encuentra en el listado por defecto y necesita saber que tiene que darlo de alta (FR-007).
        if (fallo.detalle.codigo === CodigosErrorViajes.cuitDeClienteDadoDeBaja) {
          setErroresDeCampo({ cuit: 'Pertenece a un cliente dado de baja.' })
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

  const titulo = editando ? 'Editar cliente' : 'Nuevo cliente'

  if (cargando) {
    return (
      <section>
        <EncabezadoDePantalla titulo={titulo} />
        <p role="status">Cargando…</p>
      </section>
    )
  }

  return (
    <section>
      <EncabezadoDePantalla titulo={titulo} />

      <form onSubmit={guardar} noValidate className={clasesDeFormularioAgrupado}>
        {errorGlobal && <p role="alert">{errorGlobal}</p>}

        <SeccionNumerada
          numero={1}
          titulo="Identidad fiscal"
          explicacion="Los dos salen impresos en el comprobante."
        >
          <div className={`${classNameCampo('razonSocial')} max-w-campo-largo`}>
            <label htmlFor="razonSocial">Razón social</label>
            <input
              id="razonSocial"
              placeholder="Distribuidora del Litoral"
              type="text"
              required
              maxLength={100}
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

          <div className={`${classNameCampo('cuit')} max-w-campo-corto`}>
            {/* Se normaliza a sólo dígitos antes de validar: `30-71234567-8` es válido (FR-004). */}
            <label htmlFor="cuit">CUIT (con o sin guiones)</label>
            <input
              id="cuit"
              placeholder="30-71234567-8"
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
        </SeccionNumerada>

        <SeccionNumerada
          numero={2}
          titulo="Contacto"
        >
          <div className={`${classNameCampo('telefono')} max-w-campo-medio`}>
            <label htmlFor="telefono">Teléfono</label>
            <input
              id="telefono"
              placeholder="+54 9 221 555-0148"
              type="tel"
              required
              maxLength={30}
              value={telefono}
              onChange={(evento) => setTelefono(evento.target.value)}
              aria-invalid={erroresDeCampo.telefono !== undefined}
            />
            {erroresDeCampo.telefono && (
              <p className="campo__error" role="alert">
                {erroresDeCampo.telefono}
              </p>
            )}
          </div>

          <div className={`${classNameCampo('email')} max-w-campo-medio`}>
            <label htmlFor="email">Email</label>
            <input
              id="email"
              placeholder="nombre@empresa.com.ar"
              type="email"
              required
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

          <div className={`${classNameCampo('direccion')} max-w-campo-largo`}>
            {/* Opcional: el módulo no la usa para operar y no se pide por las dudas (Principio V). */}
            <label htmlFor="direccion">Dirección (opcional)</label>
            <input
              id="direccion"
              type="text"
              maxLength={200}
              value={direccion}
              onChange={(evento) => setDireccion(evento.target.value)}
              aria-invalid={erroresDeCampo.direccion !== undefined}
            />
            {erroresDeCampo.direccion && (
              <p className="campo__error" role="alert">
                {erroresDeCampo.direccion}
              </p>
            )}
          </div>
        </SeccionNumerada>

        <BarraDeAcciones anclaje="viewport" leyenda={LEYENDA_DE_OBLIGATORIOS}>
          <Boton variante="secundario" onClick={() => navegar('/clientes')} disabled={guardando}>
            Cancelar
          </Boton>
          <Boton
            type="submit"
            variante="primario"
            disabled={guardando}
            icono={<IconoEnRegla className="size-3" />}
          >
            {editando ? 'Guardar cambios' : 'Guardar cliente'}
          </Boton>
        </BarraDeAcciones>
      </form>
    </section>
  )
}
