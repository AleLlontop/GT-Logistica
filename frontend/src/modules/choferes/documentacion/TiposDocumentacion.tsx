import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { DialogoConfirmacion } from '../../usuarios/componentes/DialogoConfirmacion'
import {
  crearTipo,
  darDeBajaTipo,
  listarTipos,
  modificarTipo,
  TEXTO_AMBITO,
  type DocumentacionAmbito,
  type TipoDocumentacion,
} from './servicioTipos'

const MENSAJE_CATALOGO_VACIO =
  'Todavía no hay tipos de documentación. Cargá el primero para poder registrar documentos.'

const AMBITOS: DocumentacionAmbito[] = ['chofer', 'vehiculo']

/**
 * Catálogo de tipos de documentación (User Story 6).
 *
 * El catálogo arranca vacío y no se precarga: sin al menos un tipo no se puede cargar ningún
 * documento, así que la pantalla lo dice explícitamente en vez de mostrar una tabla vacía.
 *
 * Los días de aviso son lo que decide desde cuándo un documento figura como próximo a vencer.
 * Cambiarlos recalcula el estado de los documentos existentes la próxima vez que se consultan, sin
 * tocar ninguna fila (FR-013, US6 esc. 4).
 *
 * **Desde el Módulo 4 el catálogo sirve a dos módulos** y cada tipo declara su ámbito: los de chofer
 * se ofrecen en la documentación de choferes y los de vehículo en la de flota (Módulo 4, FR-017,
 * FR-017a). El ABM no se duplica: sigue viviendo acá.
 */
export function TiposDocumentacion() {
  const [tipos, setTipos] = useState<TipoDocumentacion[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [aviso, setAviso] = useState<string | null>(null)

  const [enEdicion, setEnEdicion] = useState<TipoDocumentacion | null>(null)
  const [nombre, setNombre] = useState('')
  const [diasAviso, setDiasAviso] = useState('30')
  const [ambito, setAmbito] = useState<DocumentacionAmbito>('chofer')
  const [erroresDeCampo, setErroresDeCampo] = useState<Record<string, string>>({})
  const [guardando, setGuardando] = useState(false)

  /** Filtro del listado. Vacío muestra los dos ámbitos, que es lo que esta pantalla mantiene. */
  const [filtroAmbito, setFiltroAmbito] = useState<DocumentacionAmbito | ''>('')

  const [aBajar, setABajar] = useState<TipoDocumentacion | null>(null)

  const traer = useCallback(() => {
    listarTipos()
      .then((lista) => {
        setTipos(lista)
        setError(null)
      })
      .catch(() =>
        setError('No pudimos traer el catálogo de tipos. Volvé a intentar en unos minutos.'),
      )
  }, [])

  useEffect(() => {
    traer()
  }, [traer])

  function limpiarFormulario() {
    setEnEdicion(null)
    setNombre('')
    setDiasAviso('30')
    setAmbito('chofer')
    setErroresDeCampo({})
  }

  function editar(tipo: TipoDocumentacion) {
    setEnEdicion(tipo)
    setNombre(tipo.nombre)
    setDiasAviso(String(tipo.diasAvisoVencimiento))
    setAmbito(tipo.ambito)
    setErroresDeCampo({})
    setAviso(null)
  }

  async function guardar(evento: FormEvent) {
    evento.preventDefault()

    const dias = Number(diasAviso)
    const encontrados: Record<string, string> = {}

    if (!nombre.trim()) encontrados.nombre = 'Completá el nombre.'
    // Cero es válido: significa sin período de aviso intermedio (FR-013).
    if (diasAviso === '' || !Number.isInteger(dias) || dias < 0) {
      encontrados.diasAvisoVencimiento = 'Tiene que ser un número entero mayor o igual a cero.'
    }

    setErroresDeCampo(encontrados)
    setError(null)
    setAviso(null)

    if (Object.keys(encontrados).length > 0) {
      return
    }

    setGuardando(true)

    try {
      const peticion = { nombre: nombre.trim(), diasAvisoVencimiento: dias, ambito }

      if (enEdicion !== null) {
        await modificarTipo(enEdicion.id, peticion)
        setAviso('Los cambios se guardaron correctamente.')
      } else {
        await crearTipo(peticion)
        setAviso(`El tipo ${nombre.trim()} se cargó correctamente.`)
      }

      limpiarFormulario()
      traer()
    } catch (fallo) {
      if (fallo instanceof ErrorHttp) {
        if (fallo.detalle.campo !== undefined) {
          setErroresDeCampo({ [fallo.detalle.campo]: fallo.detalle.mensaje })
        } else {
          setError(fallo.detalle.mensaje)
        }
      } else {
        setError('Ocurrió un problema inesperado. Volvé a intentar en unos minutos.')
      }
    } finally {
      setGuardando(false)
    }
  }

  async function confirmarBaja() {
    if (aBajar === null) {
      return
    }

    try {
      await darDeBajaTipo(aBajar.id)
      setError(null)
      setAviso(`${aBajar.nombre} quedó inactivo.`)
      traer()
    } catch (fallo) {
      // Acá cae el rechazo de FR-014: el tipo tiene documentos. El mensaje dice cuántos.
      setError(
        fallo instanceof ErrorHttp
          ? fallo.detalle.mensaje
          : 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
      )
    } finally {
      setABajar(null)
    }
  }

  return (
    <main>
      <h1>Tipos de documentación</h1>

      {error !== null && <p role="alert">{error}</p>}
      {aviso !== null && <p role="status">{aviso}</p>}

      <form onSubmit={guardar} noValidate>
        <h2>{enEdicion !== null ? `Editar ${enEdicion.nombre}` : 'Nuevo tipo'}</h2>

        <div className="campo">
          <label htmlFor="nombre">Nombre</label>
          <input
            id="nombre"
            type="text"
            maxLength={100}
            value={nombre}
            onChange={(evento) => setNombre(evento.target.value)}
            required
            aria-invalid={erroresDeCampo.nombre !== undefined}
          />
          {erroresDeCampo.nombre !== undefined && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.nombre}
            </p>
          )}
        </div>

        <div className="campo">
          <label htmlFor="diasAvisoVencimiento">Días de aviso de vencimiento</label>
          <input
            id="diasAvisoVencimiento"
            type="number"
            min={0}
            step={1}
            value={diasAviso}
            onChange={(evento) => setDiasAviso(evento.target.value)}
            required
            aria-invalid={erroresDeCampo.diasAvisoVencimiento !== undefined}
            aria-describedby="ayuda-dias"
          />
          <small id="ayuda-dias">
            Con cuántos días de anticipación un documento de este tipo empieza a figurar como próximo
            a vencer. Cero significa sin aviso previo.
          </small>
          {erroresDeCampo.diasAvisoVencimiento !== undefined && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.diasAvisoVencimiento}
            </p>
          )}
        </div>

        <div className="campo">
          <label htmlFor="ambito">Ámbito</label>
          <select
            id="ambito"
            value={ambito}
            onChange={(evento) => setAmbito(evento.target.value as DocumentacionAmbito)}
            required
            aria-invalid={erroresDeCampo.ambito !== undefined}
            aria-describedby="ayuda-ambito"
          >
            {AMBITOS.map((valor) => (
              <option key={valor} value={valor}>
                {TEXTO_AMBITO[valor]}
              </option>
            ))}
          </select>
          <small id="ayuda-ambito">
            Decide en qué módulo se ofrece el tipo. Se puede corregir mientras el tipo no tenga
            ningún documento cargado.
          </small>
          {erroresDeCampo.ambito !== undefined && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.ambito}
            </p>
          )}
        </div>

        <div className="acciones">
          <button type="submit" disabled={guardando}>
            {enEdicion !== null ? 'Guardar cambios' : 'Cargar tipo'}
          </button>
          {enEdicion !== null && (
            <button type="button" onClick={limpiarFormulario} disabled={guardando}>
              Cancelar
            </button>
          )}
        </div>
      </form>

      {tipos === null && error === null && <p role="status">Cargando tipos…</p>}

      {tipos !== null && tipos.length === 0 && <p role="status">{MENSAJE_CATALOGO_VACIO}</p>}

      {tipos !== null && tipos.length > 0 && (
        <>
        <div className="campo">
          <label htmlFor="filtro-ambito">Filtrar por ámbito</label>
          <select
            id="filtro-ambito"
            value={filtroAmbito}
            onChange={(evento) => setFiltroAmbito(evento.target.value as DocumentacionAmbito | '')}
            aria-describedby="ayuda-filtro-ambito"
          >
            <option value="">Todos</option>
            {AMBITOS.map((valor) => (
              <option key={valor} value={valor}>
                {TEXTO_AMBITO[valor]}
              </option>
            ))}
          </select>
          {/* Ninguna fila queda oculta en silencio: el control dice qué está filtrando. */}
          <small id="ayuda-filtro-ambito" role="status">
            {filtroAmbito === ''
              ? 'Mostrando los tipos de los dos ámbitos.'
              : `Mostrando sólo los de ámbito ${TEXTO_AMBITO[filtroAmbito]}.`}
          </small>
        </div>

        <table>
          <caption>Catálogo de tipos de documentación</caption>
          <thead>
            <tr>
              <th scope="col">Nombre</th>
              <th scope="col">Días de aviso</th>
              <th scope="col">Ámbito</th>
              <th scope="col">Estado</th>
              <th scope="col">Documentos que lo usan</th>
              <th scope="col">Acciones</th>
            </tr>
          </thead>
          <tbody>
            {tipos
              .filter((tipo) => filtroAmbito === '' || tipo.ambito === filtroAmbito)
              .map((tipo) => (
              <tr key={tipo.id}>
                <td>{tipo.nombre}</td>
                <td>{tipo.diasAvisoVencimiento}</td>
                <td>{TEXTO_AMBITO[tipo.ambito]}</td>
                <td>{tipo.activo ? 'Activo' : 'Inactivo'}</td>
                {/* Es lo que explica por qué algunos no se pueden dar de baja ni cambiar de ámbito
                    (FR-014, FR-017d). Suma los de choferes y los de vehículos (FR-017b). */}
                <td>{tipo.documentosAsociados}</td>
                <td>
                  <button type="button" onClick={() => editar(tipo)}>
                    Editar
                  </button>
                  {tipo.activo && (
                    <button type="button" onClick={() => setABajar(tipo)}>
                      Dar de baja
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        </>
      )}

      {aBajar !== null && (
        <DialogoConfirmacion
          titulo="Dar de baja el tipo de documentación"
          mensaje={`¿Confirmás la baja de ${aBajar.nombre}? Va a dejar de ofrecerse al cargar documentación.`}
          onConfirmar={confirmarBaja}
          onCancelar={() => setABajar(null)}
        />
      )}
    </main>
  )
}
