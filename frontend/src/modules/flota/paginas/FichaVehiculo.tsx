import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import {
  ConfirmacionBajaVehiculo,
  type QueSeConfirma,
} from '../componentes/ConfirmacionBajaVehiculo'
import { ConfirmacionEliminarDocumento } from '../componentes/ConfirmacionEliminarDocumento'
import { FormularioDocumentoVehiculo } from '../documentacion/FormularioDocumentoVehiculo'
import { eliminarDocumentoVehiculo } from '../documentacion/servicioDocumentacionVehiculo'
import { rutaDelArchivoDeFlota } from '../servicios/api'
import {
  claseDeEstado,
  formatearFecha,
  TEXTO_ESTADO_DOCUMENTACION,
  TEXTO_ESTADO_DOCUMENTO,
  TEXTO_ESTADO_VEHICULO,
  textoDelPlazo,
} from '../servicios/estados'
import {
  darDeBajaVehiculo,
  obtenerVehiculo,
  reactivarVehiculo,
  type DocumentoVehiculo,
  type VehiculoDetalle,
} from '../servicios/servicioFlota'

const MENSAJE_SIN_DOCUMENTACION =
  'Esta unidad todavía no tiene documentación cargada. Mientras no la tenga, no puede quedar ' +
  'disponible.'

/**
 * Ficha de una unidad con toda su documentación (User Stories 3 y 4).
 *
 * **El estado de cada documento no es editable por ninguna vía** (FR-021, SC-004): no hay lista
 * desplegable, ni casilla, ni forma de forzarlo. Se muestra y nada más.
 *
 * Los documentos vienen del servidor agrupados por tipo y, dentro de cada tipo, por vencimiento
 * descendente: el vigente arriba y sus renovaciones anteriores debajo. Los que no son el vigente se
 * muestran atenuados **y con la palabra "Histórico"**, no sólo con el gris: es lo que explica por qué
 * la unidad figura en regla con un documento vencido a la vista (FR-024, convención [003]).
 */
export function FichaVehiculo() {
  const { id } = useParams()
  const navegar = useNavigate()
  const vehiculoId = Number(id)

  const [vehiculo, setVehiculo] = useState<VehiculoDetalle | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [aviso, setAviso] = useState<string | null>(null)

  const [cargandoDocumento, setCargandoDocumento] = useState(false)
  const [corrigiendo, setCorrigiendo] = useState<DocumentoVehiculo | null>(null)
  const [aConfirmar, setAConfirmar] = useState<QueSeConfirma | null>(null)
  const [documentoAEliminar, setDocumentoAEliminar] = useState<DocumentoVehiculo | null>(null)

  const traer = useCallback(() => {
    obtenerVehiculo(vehiculoId)
      .then((detalle) => {
        setVehiculo(detalle)
        setError(null)
      })
      .catch((fallo) =>
        setError(
          fallo instanceof ErrorHttp
            ? fallo.detalle.mensaje
            : 'No pudimos traer la ficha. Volvé a intentar en unos minutos.',
        ),
      )
  }, [vehiculoId])

  useEffect(() => {
    traer()
  }, [traer])

  async function confirmarBajaOReactivacion() {
    if (aConfirmar === null || vehiculo === null) {
      return
    }

    try {
      if (aConfirmar.tipo === 'baja') {
        await darDeBajaVehiculo(vehiculo.id)
        setAviso(`La unidad ${vehiculo.patente} quedó dada de baja. Su documentación se conserva.`)
      } else {
        await reactivarVehiculo(vehiculo.id)
        setAviso(`La unidad ${vehiculo.patente} volvió a la flota.`)
      }

      setError(null)
      traer()
    } catch (fallo) {
      // Acá cae el rechazo de FR-008e: el transportista o el tipo quedaron inactivos.
      setError(
        fallo instanceof ErrorHttp
          ? fallo.detalle.mensaje
          : 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
      )
    } finally {
      setAConfirmar(null)
    }
  }

  async function confirmarEliminacion() {
    if (documentoAEliminar === null) {
      return
    }

    try {
      await eliminarDocumentoVehiculo(documentoAEliminar.id)
      setError(null)
      setAviso('El documento y su archivo se eliminaron.')
      traer()
    } catch (fallo) {
      setError(
        fallo instanceof ErrorHttp
          ? fallo.detalle.mensaje
          : 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
      )
    } finally {
      setDocumentoAEliminar(null)
    }
  }

  if (error !== null && vehiculo === null) {
    return (
      <main>
        <h1>Ficha de la unidad</h1>
        <p role="alert">{error}</p>
        <Link to="/flota">Volver al listado</Link>
      </main>
    )
  }

  if (vehiculo === null) {
    return (
      <main>
        <p role="status">Cargando ficha…</p>
      </main>
    )
  }

  return (
    <main>
      <h1>{vehiculo.patente}</h1>

      {error !== null && <p role="alert">{error}</p>}
      {aviso !== null && <p role="status">{aviso}</p>}

      <section aria-label="Datos de la unidad">
        <h2>Datos de la unidad</h2>
        <dl>
          <dt>Marca</dt>
          <dd>{vehiculo.marca}</dd>
          <dt>Modelo</dt>
          <dd>{vehiculo.modelo}</dd>
          <dt>Tipo</dt>
          <dd>{vehiculo.tipo.nombre}</dd>
          <dt>Transportista</dt>
          <dd>{vehiculo.transportista.nombre}</dd>
          <dt>Estado</dt>
          {/* El **derivado**, que es el que responde si la unidad puede salir a la ruta (FR-014). */}
          <dd className={claseDeEstado(vehiculo.estado)}>
            {TEXTO_ESTADO_VEHICULO[vehiculo.estado]}
            {!vehiculo.activo && ' — Dada de baja'}
          </dd>
          <dt>Documentación</dt>
          <dd className={claseDeEstado(vehiculo.estadoDocumentacion)}>
            {TEXTO_ESTADO_DOCUMENTACION[vehiculo.estadoDocumentacion]}
          </dd>
        </dl>
      </section>

      <div className="acciones">
        <button type="button" onClick={() => navegar(`/flota/${vehiculo.id}/editar`)}>
          Editar
        </button>

        {/* Si está dada de baja, en lugar de Dar de baja aparece Reactivar (FR-008e). */}
        {vehiculo.activo ? (
          <button
            type="button"
            onClick={() => setAConfirmar({ tipo: 'baja', patente: vehiculo.patente })}
          >
            Dar de baja
          </button>
        ) : (
          <button
            type="button"
            onClick={() => setAConfirmar({ tipo: 'reactivacion', patente: vehiculo.patente })}
          >
            Reactivar
          </button>
        )}

        <button type="button" onClick={() => setCargandoDocumento(true)}>
          Agregar documento
        </button>
      </div>

      {cargandoDocumento && (
        <FormularioDocumentoVehiculo
          vehiculoId={vehiculo.id}
          documentosDelVehiculo={vehiculo.documentos}
          onGuardado={(mensaje) => {
            setCargandoDocumento(false)
            setAviso(mensaje)
            traer()
          }}
          onCancelar={() => setCargandoDocumento(false)}
        />
      )}

      {corrigiendo !== null && (
        <FormularioDocumentoVehiculo
          vehiculoId={vehiculo.id}
          documento={corrigiendo}
          documentosDelVehiculo={vehiculo.documentos}
          onGuardado={(mensaje) => {
            setCorrigiendo(null)
            setAviso(mensaje)
            traer()
          }}
          onCancelar={() => setCorrigiendo(null)}
        />
      )}

      <section aria-label="Documentación">
        <h2>Documentación</h2>

        {vehiculo.documentos.length === 0 && <p role="status">{MENSAJE_SIN_DOCUMENTACION}</p>}

        {vehiculo.documentos.length > 0 && (
          <table>
            <caption>Documentos de la unidad</caption>
            <thead>
              <tr>
                <th scope="col">Tipo</th>
                <th scope="col">Número</th>
                <th scope="col">Emisión</th>
                <th scope="col">Vencimiento</th>
                <th scope="col">Estado</th>
                <th scope="col">Archivo</th>
                <th scope="col">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {vehiculo.documentos.map((documento) => (
                <tr
                  key={documento.id}
                  className={documento.esVigenteDelTipo ? undefined : 'documento--historico'}
                >
                  <td>{documento.tipo.nombre}</td>
                  <td>{documento.numero}</td>
                  <td>{formatearFecha(documento.fechaEmision)}</td>
                  <td>{formatearFecha(documento.fechaVencimiento)}</td>
                  <td className={claseDeEstado(documento.estado)}>
                    {TEXTO_ESTADO_DOCUMENTO[documento.estado]}
                    {/* El histórico lleva la palabra, no nada más el gris (convención [003]). */}
                    {!documento.esVigenteDelTipo && ' — Histórico'}
                    {documento.esVigenteDelTipo && (
                      <> — {textoDelPlazo(documento.diasHastaVencimiento)}</>
                    )}
                  </td>
                  <td>
                    {documento.tieneArchivo ? (
                      <a
                        href={rutaDelArchivoDeFlota(documento.id)}
                        target="_blank"
                        rel="noreferrer"
                      >
                        Abrir archivo
                      </a>
                    ) : (
                      // Ni un enlace roto ni un espacio en blanco: la leyenda que lo explica
                      // (FR-016a).
                      'Sin archivo adjunto'
                    )}
                  </td>
                  <td>
                    <button type="button" onClick={() => setCorrigiendo(documento)}>
                      Corregir
                    </button>
                    <button type="button" onClick={() => setDocumentoAEliminar(documento)}>
                      Eliminar
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <Link to="/flota">Volver al listado</Link>

      {aConfirmar !== null && (
        <ConfirmacionBajaVehiculo
          que={aConfirmar}
          onConfirmar={confirmarBajaOReactivacion}
          onCancelar={() => setAConfirmar(null)}
        />
      )}

      {documentoAEliminar !== null && (
        <ConfirmacionEliminarDocumento
          onConfirmar={confirmarEliminacion}
          onCancelar={() => setDocumentoAEliminar(null)}
        />
      )}
    </main>
  )
}
