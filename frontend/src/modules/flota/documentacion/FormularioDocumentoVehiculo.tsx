import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { listarTipos, type TipoDocumentacion } from '../../choferes/documentacion/servicioTipos'
import { AdjuntoFlota } from '../servicios/api'
import type { DocumentoVehiculo } from '../servicios/servicioFlota'
import { cargarDocumentoVehiculo, corregirDocumentoVehiculo } from './servicioDocumentacionVehiculo'

const MENSAJE_SIN_TIPOS_DE_VEHICULO =
  'No hay tipos de documentación de vehículo activos. Cargá uno desde la pantalla Tipos de ' +
  'documentación, con ámbito Vehículo.'

interface Props {
  vehiculoId: number
  /** Cuando viene, el formulario corrige ese documento en vez de cargar uno nuevo (FR-026). */
  documento?: DocumentoVehiculo
  /** Documentos que la unidad ya tiene, para avisar si esta carga es una renovación. */
  documentosDelVehiculo: DocumentoVehiculo[]
  onGuardado: (mensaje: string) => void
  onCancelar: () => void
}

/**
 * Carga y corrección de un documento de la unidad (User Story 3).
 *
 * **No hay campo de estado, a propósito** (FR-021, SC-004): lo calcula el sistema a partir de la
 * fecha de vencimiento y de los días de aviso de su tipo. El formulario tampoco lo muestra como valor
 * previsto, para no dar a entender que se puede elegir.
 *
 * El selector de tipo ofrece **únicamente los tipos activos de ámbito vehículo**: los de chofer no
 * aparecen, y el servidor los rechaza igual si alguien manda el identificador a mano (FR-017a).
 *
 * Si el archivo no llega a guardarse, el documento **no se crea ni se modifica** y el formulario
 * conserva todo lo tipeado para reintentar sin volver a completarlo (FR-029).
 */
export function FormularioDocumentoVehiculo({
  vehiculoId,
  documento,
  documentosDelVehiculo,
  onGuardado,
  onCancelar,
}: Props) {
  const corrigiendo = documento !== undefined

  const [tipos, setTipos] = useState<TipoDocumentacion[] | null>(null)
  const [tipoId, setTipoId] = useState<number | ''>(documento?.tipo.id ?? '')
  const [numero, setNumero] = useState(documento?.numero ?? '')
  const [fechaEmision, setFechaEmision] = useState(documento?.fechaEmision ?? '')
  const [fechaVencimiento, setFechaVencimiento] = useState(documento?.fechaVencimiento ?? '')
  const [archivo, setArchivo] = useState<File | null>(null)

  const [errores, setErrores] = useState<Record<string, string>>({})
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null)
  const [guardando, setGuardando] = useState(false)

  useEffect(() => {
    let vigente = true

    // Sólo los activos y **sólo los de ámbito vehículo** (FR-017a).
    listarTipos(true, 'vehiculo')
      .then((lista) => {
        if (!vigente) return

        setTipos(lista)
        if (documento === undefined && lista.length > 0) {
          setTipoId((previo) => (previo === '' ? lista[0].id : previo))
        }
      })
      .catch(() => {
        if (vigente) {
          setTipos([])
          setErrorGeneral('No pudimos traer el catálogo de tipos. Volvé a intentar en unos minutos.')
        }
      })

    return () => {
      vigente = false
    }
  }, [documento])

  function validar() {
    const encontrados: Record<string, string> = {}

    if (tipoId === '') encontrados.documentacionTipoId = 'Elegí un tipo de documentación.'
    if (!numero.trim()) encontrados.numero = 'Completá el número.'
    if (!fechaEmision) encontrados.fechaEmision = 'Completá la fecha de emisión.'
    if (!fechaVencimiento) {
      encontrados.fechaVencimiento = 'Completá la fecha de vencimiento.'
    } else if (fechaEmision && fechaVencimiento <= fechaEmision) {
      encontrados.fechaVencimiento = 'La fecha de vencimiento tiene que ser posterior a la de emisión.'
    }

    if (archivo !== null && archivo.size > AdjuntoFlota.tamanioMaximoEnBytes) {
      encontrados.archivo = `El archivo tiene que ser ${AdjuntoFlota.descripcion}.`
    }

    return encontrados
  }

  async function guardar(evento: FormEvent) {
    evento.preventDefault()

    const encontrados = validar()
    setErrores(encontrados)
    setErrorGeneral(null)

    if (Object.keys(encontrados).length > 0) {
      return
    }

    setGuardando(true)

    const peticion = {
      documentacionTipoId: Number(tipoId),
      numero: numero.trim(),
      fechaEmision,
      fechaVencimiento,
    }

    try {
      if (corrigiendo) {
        await corregirDocumentoVehiculo(documento.id, peticion, archivo)
        onGuardado('El documento quedó actualizado.')
      } else {
        await cargarDocumentoVehiculo(vehiculoId, peticion, archivo)
        onGuardado(
          archivo === null
            ? 'El documento quedó cargado. Todavía no tiene archivo adjunto; podés agregarlo más adelante.'
            : 'El documento quedó cargado.',
        )
      }
    } catch (fallo) {
      // Nada se limpia acá: si el archivo no se guardó, el documento no se creó ni se modificó y lo
      // tipeado tiene que seguir en pantalla para poder reintentar (FR-029).
      if (fallo instanceof ErrorHttp) {
        if (fallo.detalle.campo !== undefined) {
          setErrores({ [fallo.detalle.campo]: fallo.detalle.mensaje })
        } else {
          setErrorGeneral(fallo.detalle.mensaje)
        }
      } else {
        setErrorGeneral('Ocurrió un problema inesperado. Volvé a intentar en unos minutos.')
      }
    } finally {
      setGuardando(false)
    }
  }

  if (tipos === null) {
    return <p role="status">Cargando tipos de documentación…</p>
  }

  // Sin tipos de ámbito vehículo no se puede cargar nada, y se dice por qué con el enlace que lo
  // resuelve (FR-017a, US2 esc. 6 y 7 aplicados a la documentación).
  if (tipos.length === 0) {
    return (
      <div>
        <p role="alert">{MENSAJE_SIN_TIPOS_DE_VEHICULO}</p>
        <Link to="/tipos-documentacion">Ir a Tipos de documentación</Link>
      </div>
    )
  }

  // Si la unidad ya tiene uno de ese tipo, esta carga es una renovación. Se avisa y nada más: no se
  // impide ni se pide confirmar (FR-023, FR-024).
  const esRenovacion =
    !corrigiendo &&
    tipoId !== '' &&
    documentosDelVehiculo.some((otro) => otro.tipo.id === Number(tipoId))

  return (
    <form onSubmit={guardar} noValidate>
      <h2>{corrigiendo ? 'Corregir documento' : 'Cargar documento'}</h2>

      {errorGeneral !== null && <p role="alert">{errorGeneral}</p>}

      <div className="campo">
        <label htmlFor="documentacionTipoId">Tipo de documentación</label>
        <select
          id="documentacionTipoId"
          value={tipoId}
          onChange={(evento) => setTipoId(Number(evento.target.value))}
          required
          aria-invalid={errores.documentacionTipoId !== undefined}
        >
          {tipos.map((tipo) => (
            <option key={tipo.id} value={tipo.id}>
              {tipo.nombre}
            </option>
          ))}
        </select>
        {errores.documentacionTipoId !== undefined && (
          <p className="campo__error" role="alert">
            {errores.documentacionTipoId}
          </p>
        )}
      </div>

      {esRenovacion && (
        <p role="status">
          Esta unidad ya tiene un documento de ese tipo. De todos ellos cuenta el de vencimiento más
          lejano, y los demás quedan como historial.
        </p>
      )}

      <div className="campo">
        <label htmlFor="numero">Número</label>
        <input
          id="numero"
          type="text"
          maxLength={50}
          value={numero}
          onChange={(evento) => setNumero(evento.target.value)}
          required
          aria-invalid={errores.numero !== undefined}
        />
        {errores.numero !== undefined && (
          <p className="campo__error" role="alert">
            {errores.numero}
          </p>
        )}
      </div>

      <div className="campo">
        <label htmlFor="fechaEmision">Fecha de emisión</label>
        <input
          id="fechaEmision"
          type="date"
          value={fechaEmision}
          onChange={(evento) => setFechaEmision(evento.target.value)}
          required
          aria-invalid={errores.fechaEmision !== undefined}
        />
        {errores.fechaEmision !== undefined && (
          <p className="campo__error" role="alert">
            {errores.fechaEmision}
          </p>
        )}
      </div>

      <div className="campo">
        <label htmlFor="fechaVencimiento">Fecha de vencimiento</label>
        <input
          id="fechaVencimiento"
          type="date"
          value={fechaVencimiento}
          onChange={(evento) => setFechaVencimiento(evento.target.value)}
          required
          aria-invalid={errores.fechaVencimiento !== undefined}
        />
        {errores.fechaVencimiento !== undefined && (
          <p className="campo__error" role="alert">
            {errores.fechaVencimiento}
          </p>
        )}
      </div>

      <div className="campo">
        <label htmlFor="archivo">Archivo (opcional)</label>
        <input
          id="archivo"
          type="file"
          accept={AdjuntoFlota.tiposAceptados.join(',')}
          onChange={(evento) => setArchivo(evento.target.files?.[0] ?? null)}
          aria-describedby="ayuda-archivo"
          aria-invalid={errores.archivo !== undefined}
        />
        {/* Se informa antes de elegir nada, no después de que la subida falle (FR-025). */}
        <small id="ayuda-archivo">{AdjuntoFlota.descripcion}</small>

        {corrigiendo && documento.tieneArchivo && (
          <p>
            Adjunto actual: {documento.archivoNombre}. Si no elegís uno nuevo, se conserva; si elegís
            otro, el anterior se borra.
          </p>
        )}

        {errores.archivo !== undefined && (
          <p className="campo__error" role="alert">
            {errores.archivo}
          </p>
        )}
      </div>

      <div className="acciones">
        <button type="submit" disabled={guardando}>
          {guardando ? 'Guardando…' : corrigiendo ? 'Guardar cambios' : 'Cargar documento'}
        </button>
        <button type="button" onClick={onCancelar} disabled={guardando}>
          Cancelar
        </button>
      </div>
    </form>
  )
}
