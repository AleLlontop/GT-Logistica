import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { ConfirmacionBaja, type QueSeDaDeBaja } from '../componentes/ConfirmacionBaja'
import { FormularioDocumento } from '../documentacion/FormularioDocumento'
import { eliminarDocumento } from '../documentacion/servicioDocumentacion'
import { rutaDelArchivo } from '../servicios/api'
import {
  claseDeEstado,
  formatearFecha,
  TEXTO_ESTADO_CHOFER,
  TEXTO_ESTADO_DOCUMENTO,
  textoDelPlazo,
} from '../servicios/estados'
import {
  darDeBajaChofer,
  obtenerChofer,
  reactivarChofer,
  type ChoferDetalle,
  type Documento,
} from '../servicios/servicioChoferes'

const MENSAJE_SIN_DOCUMENTACION = 'Este chofer todavía no tiene documentación cargada.'

/**
 * Ficha de un chofer con toda su documentación (User Story 4).
 *
 * **El estado de cada documento no es editable por ninguna vía** (FR-018): no hay lista desplegable,
 * ni casilla, ni forma de forzarlo. Se muestra y nada más.
 *
 * Un documento reemplazado por una renovación se muestra con su estado real —una licencia vieja
 * sigue diciendo `Vencida`— pero atenuado y **con la palabra "Reemplazado"**, no sólo con el gris:
 * es lo que explica por qué el chofer figura en regla con un documento vencido a la vista (FR-020a).
 */
export function FichaChofer() {
  const { id } = useParams()
  const navegar = useNavigate()
  const choferId = Number(id)

  const [chofer, setChofer] = useState<ChoferDetalle | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [aviso, setAviso] = useState<string | null>(null)

  const [cargandoDocumento, setCargandoDocumento] = useState(false)
  const [corrigiendo, setCorrigiendo] = useState<Documento | null>(null)
  const [aConfirmar, setAConfirmar] = useState<QueSeDaDeBaja | null>(null)
  const [documentoAEliminar, setDocumentoAEliminar] = useState<Documento | null>(null)

  const traer = useCallback(() => {
    obtenerChofer(choferId)
      .then((detalle) => {
        setChofer(detalle)
        setError(null)
      })
      .catch((fallo) =>
        setError(
          fallo instanceof ErrorHttp
            ? fallo.detalle.mensaje
            : 'No pudimos traer la ficha. Volvé a intentar en unos minutos.',
        ),
      )
  }, [choferId])

  useEffect(() => {
    traer()
  }, [traer])

  async function confirmar() {
    if (aConfirmar === null || chofer === null) {
      return
    }

    try {
      if (aConfirmar.tipo === 'chofer') {
        await darDeBajaChofer(chofer.id)
        setAviso(`${chofer.apellido}, ${chofer.nombre} quedó inactivo.`)
      } else if (aConfirmar.tipo === 'reactivarChofer') {
        await reactivarChofer(chofer.id)
        setAviso(`${chofer.apellido}, ${chofer.nombre} volvió a estar activo.`)
      } else if (aConfirmar.tipo === 'documento' && documentoAEliminar !== null) {
        await eliminarDocumento(documentoAEliminar.id)
        setAviso('El documento se eliminó junto con su archivo adjunto.')
      }

      setError(null)
      traer()
    } catch (fallo) {
      setError(
        fallo instanceof ErrorHttp
          ? fallo.detalle.mensaje
          : 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
      )
    } finally {
      setAConfirmar(null)
      setDocumentoAEliminar(null)
    }
  }

  if (error !== null && chofer === null) {
    return (
      <main>
        <h1>Ficha del chofer</h1>
        <p role="alert">{error}</p>
        <Link to="/choferes">Volver al listado</Link>
      </main>
    )
  }

  if (chofer === null) {
    return (
      <main>
        <p role="status">Cargando ficha…</p>
      </main>
    )
  }

  return (
    <main>
      <h1>
        {chofer.apellido}, {chofer.nombre}
      </h1>

      {error !== null && <p role="alert">{error}</p>}
      {aviso !== null && <p role="status">{aviso}</p>}

      <section aria-label="Datos personales">
        <h2>Datos personales</h2>
        <dl>
          <dt>DNI</dt>
          <dd>{chofer.dni}</dd>
          <dt>CUIL</dt>
          <dd>{chofer.cuil}</dd>
          <dt>Fecha de nacimiento</dt>
          <dd>{formatearFecha(chofer.fechaNacimiento)}</dd>
          <dt>Teléfono</dt>
          <dd>{chofer.telefono}</dd>
          <dt>Email</dt>
          <dd>{chofer.email}</dd>
          <dt>Transportista</dt>
          <dd>{chofer.transportista.nombre}</dd>
          <dt>Estado</dt>
          <dd>{chofer.activo ? 'Activo' : 'Inactivo'}</dd>
          <dt>Documentación</dt>
          <dd className={claseDeEstado(chofer.estadoDocumentacion)}>
            {TEXTO_ESTADO_CHOFER[chofer.estadoDocumentacion]}
          </dd>
        </dl>
      </section>

      <div className="acciones">
        <button type="button" onClick={() => navegar(`/choferes/${chofer.id}/editar`)}>
          Editar chofer
        </button>

        {/* Si está inactivo, en lugar de Dar de baja aparece Reactivar (FR-005b). */}
        {chofer.activo ? (
          <button
            type="button"
            onClick={() =>
              setAConfirmar({
                tipo: 'chofer',
                apellido: chofer.apellido,
                nombre: chofer.nombre,
              })
            }
          >
            Dar de baja
          </button>
        ) : (
          <button
            type="button"
            onClick={() =>
              setAConfirmar({
                tipo: 'reactivarChofer',
                apellido: chofer.apellido,
                nombre: chofer.nombre,
              })
            }
          >
            Reactivar
          </button>
        )}

        <button type="button" onClick={() => setCargandoDocumento(true)}>
          Cargar documento
        </button>
      </div>

      {cargandoDocumento && (
        <FormularioDocumento
          choferId={chofer.id}
          documentosDelChofer={chofer.documentos}
          onGuardado={() => {
            setCargandoDocumento(false)
            setAviso('El documento se cargó correctamente.')
            traer()
          }}
          onCancelar={() => setCargandoDocumento(false)}
        />
      )}

      {corrigiendo !== null && (
        <FormularioDocumento
          choferId={chofer.id}
          documento={corrigiendo}
          documentosDelChofer={chofer.documentos}
          onGuardado={() => {
            setCorrigiendo(null)
            setAviso('Los cambios se guardaron correctamente.')
            traer()
          }}
          onCancelar={() => setCorrigiendo(null)}
        />
      )}

      <section aria-label="Documentación">
        <h2>Documentación</h2>

        {chofer.documentos.length === 0 && <p role="status">{MENSAJE_SIN_DOCUMENTACION}</p>}

        {chofer.documentos.length > 0 && (
          <table>
            <caption>Documentos del chofer</caption>
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
              {chofer.documentos.map((documento) => (
                <tr
                  key={documento.id}
                  className={documento.esVigenteDelTipo ? undefined : 'documento--reemplazado'}
                >
                  <td>{documento.tipo.nombre}</td>
                  <td>{documento.numero}</td>
                  <td>{formatearFecha(documento.fechaEmision)}</td>
                  <td>{formatearFecha(documento.fechaVencimiento)}</td>
                  <td className={claseDeEstado(documento.estado)}>
                    {TEXTO_ESTADO_DOCUMENTO[documento.estado]}
                    {/* El reemplazado lleva la palabra, no nada más el gris. */}
                    {!documento.esVigenteDelTipo && ' — Reemplazado'}
                    {documento.esVigenteDelTipo && (
                      <> — {textoDelPlazo(documento.diasHastaVencimiento)}</>
                    )}
                  </td>
                  <td>
                    {documento.tieneArchivo ? (
                      <a href={rutaDelArchivo(documento.id)} target="_blank" rel="noreferrer">
                        Abrir archivo
                      </a>
                    ) : (
                      'Sin respaldo'
                    )}
                  </td>
                  <td>
                    <button type="button" onClick={() => setCorrigiendo(documento)}>
                      Corregir
                    </button>
                    <button
                      type="button"
                      onClick={() => {
                        setDocumentoAEliminar(documento)
                        setAConfirmar({
                          tipo: 'documento',
                          tipoDocumento: documento.tipo.nombre,
                          numero: documento.numero,
                        })
                      }}
                    >
                      Eliminar
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <Link to="/choferes">Volver al listado</Link>

      {aConfirmar !== null && (
        <ConfirmacionBaja
          que={aConfirmar}
          onConfirmar={confirmar}
          onCancelar={() => {
            setAConfirmar(null)
            setDocumentoAEliminar(null)
          }}
        />
      )}
    </main>
  )
}
