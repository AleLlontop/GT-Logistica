import { AsideDeFicha, BloqueDeAside } from '../../../compartido/ui/AsideDeFicha'
import { Boton } from '../../../compartido/ui/Boton'
import { FichaCuerpo, FichaSeccion } from '../../../compartido/ui/Ficha'
import { MenuDeFila } from '../../../compartido/ui/MenuDeFila'
import { TablaDesplazable } from '../../../compartido/ui/Listado'
import { IconoAnulado, IconoDocumento } from '../../../compartido/ui/iconos'
import { TokenDeIdentificador } from '../../../compartido/ui/TokenDeIdentificador'
import { Estado } from '../../../compartido/ui/Estado'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
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
      <section>
        <EncabezadoDePantalla titulo="Ficha de la unidad" />
        <p role="alert">{error}</p>
        <Link to="/flota">Volver al listado</Link>
      </section>
    )
  }

  if (vehiculo === null) {
    return (
      <section>
        <p role="status">Cargando ficha…</p>
      </section>
    )
  }

  return (
    <section>
      <EncabezadoDePantalla
        titulo={vehiculo.patente}
        volverA={{ ruta: '/flota', etiqueta: 'Volver al listado' }}
        resumen={
          <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <span>
              {vehiculo.marca} {vehiculo.modelo}
            </span>
            <span aria-hidden="true">
              ·
            </span>
            <span>{vehiculo.tipo.nombre}</span>
            <span aria-hidden="true">
              ·
            </span>
            <span>{vehiculo.transportista.nombre}</span>
            {!vehiculo.activo && (
              <>
                <span aria-hidden="true">
                  ·
                </span>
                {/* La unidad dada de baja lleva la palabra que lo explica (FR-060). */}
                <span className="atenuada">Dada de baja</span>
              </>
            )}
          </span>
        }
        accionPrincipal={
          <>
            {/*
              **La acción principal es *Agregar documento*** (FR-019). El verbo es el que ya está en
              pantalla y **no es el mismo que el de choferes** —ahí dice *Cargar documento*—: los dos
              quedan como estaban (FR-066).
            */}
            <Boton variante="secundario" onClick={() => navegar(`/flota/${vehiculo.id}/editar`)}>
              Editar
            </Boton>

            {/* Si está dada de baja, en lugar de Dar de baja aparece Reactivar (FR-008e del
                Módulo 4): no conviven. */}
            {vehiculo.activo ? (
              <Boton
                variante="destructivo"
                icono={<IconoAnulado className="size-3" />}
                onClick={() => setAConfirmar({ tipo: 'baja', patente: vehiculo.patente })}
              >
                Dar de baja
              </Boton>
            ) : (
              <Boton
                variante="secundario"
                onClick={() => setAConfirmar({ tipo: 'reactivacion', patente: vehiculo.patente })}
              >
                Reactivar
              </Boton>
            )}

            <Boton
              variante="primario"
              onClick={() => setCargandoDocumento(true)}
              icono={<IconoDocumento className="size-3" />}
            >
              Agregar documento
            </Boton>
          </>
        }
      />

      {error !== null && (
        <p
          role="alert"
          className="mb-[18px] rounded-card border border-line bg-danger-bg px-[18px] py-4 text-[13px] leading-5 font-medium text-danger-text"
        >
          {error}
        </p>
      )}
      {aviso !== null && (
        <p
          role="status"
          className="mb-[18px] rounded-card border border-line bg-estado-rendido-bg px-[18px] py-4 text-[13px] leading-5 font-medium text-estado-rendido"
        >
          {aviso}
        </p>
      )}

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

      <FichaCuerpo
        aside={
          <AsideDeFicha
            /* El dato de más valor de una unidad es su estado operativo **derivado**: es el que
               responde si puede salir a la ruta (data-model §8, FR-014 del Módulo 4). */
            destacado={
              <div className="flex flex-col gap-2.5">
                <span className="text-[9.5px] font-bold tracking-[0.14em] text-encabezado uppercase">
                  Estado operativo
                </span>
                <Estado
                  valor={vehiculo.estado}
                  texto={TEXTO_ESTADO_VEHICULO[vehiculo.estado]}
                  forma="pastilla"
                  className="text-[14px]"
                />
                <TokenDeIdentificador numero={vehiculo.patente} />
              </div>
            }
          >
            <BloqueDeAside titulo="Documentación">
              <Estado
                valor={vehiculo.estadoDocumentacion}
                texto={TEXTO_ESTADO_DOCUMENTACION[vehiculo.estadoDocumentacion]}
                forma="pastilla"
              />
            </BloqueDeAside>

            <BloqueDeAside titulo="Dependencia">
              <p className="m-0">{vehiculo.transportista.nombre}</p>
            </BloqueDeAside>
          </AsideDeFicha>
        }
      >
        <FichaSeccion titulo="Datos de la unidad" id="titulo-datos-vehiculo">
          <dl>
            <dt>Marca</dt>
            <dd>{vehiculo.marca}</dd>
            <dt>Modelo</dt>
            <dd>{vehiculo.modelo}</dd>
            <dt>Tipo</dt>
            <dd>{vehiculo.tipo.nombre}</dd>
            <dt>Transportista</dt>
            <dd>{vehiculo.transportista.nombre}</dd>
          </dl>
        </FichaSeccion>

        <FichaSeccion titulo="Documentación" id="titulo-documentacion-vehiculo">
          {vehiculo.documentos.length === 0 && (
            <p role="status" className="m-0 px-[22px] py-5 text-[13px] leading-5 text-ink-soft">
              {MENSAJE_SIN_DOCUMENTACION}
            </p>
          )}

          {vehiculo.documentos.length > 0 && (
            <TablaDesplazable>
              <table className="w-full border-collapse text-[13px]">
                <caption className="sr-only">Documentos de la unidad</caption>
                <thead className="bg-surface-soft">
                  <tr className="[&>th]:border-b [&>th]:border-line [&>th]:px-5 [&>th]:py-2.5 [&>th]:text-left [&>th]:text-[9.5px] [&>th]:font-bold [&>th]:tracking-[0.14em] [&>th]:text-encabezado [&>th]:uppercase">
                    <th scope="col">Tipo</th>
                    <th scope="col">Número</th>
                    <th scope="col">Emisión</th>
                    <th scope="col">Vencimiento</th>
                    <th scope="col">Estado</th>
                    <th scope="col">Archivo</th>
                    {/* La columna `Acciones` desaparece: sus acciones pasan al `···` (FR-046). */}
                    <th scope="col" className="w-8" aria-hidden="true" />
                  </tr>
                </thead>
                <tbody className="[&>tr]:border-b [&>tr]:border-line [&>tr:last-child]:border-b-0 [&_td]:px-5 [&_td]:py-[13px]">
                  {vehiculo.documentos.map((documento) => (
                    <tr
                      key={documento.id}
                      className={documento.esVigenteDelTipo ? undefined : 'atenuada'}
                    >
                      <td>{documento.tipo.nombre}</td>
                      <td className="font-mono">{documento.numero}</td>
                      <td>{formatearFecha(documento.fechaEmision)}</td>
                      <td>{formatearFecha(documento.fechaVencimiento)}</td>
                      <td>
                        <Estado
                          valor={documento.estado}
                          texto={TEXTO_ESTADO_DOCUMENTO[documento.estado]}
                          forma="pastilla"
                          detalle={
                            /* El histórico lleva la palabra, no nada más el gris ([003], FR-060). */
                            documento.esVigenteDelTipo
                              ? textoDelPlazo(documento.diasHastaVencimiento)
                              : 'Histórico'
                          }
                        />
                      </td>
                      <td>
                        {documento.tieneArchivo ? (
                          <a
                            href={rutaDelArchivoDeFlota(documento.id)}
                            target="_blank"
                            rel="noreferrer"
                            className="text-brand underline underline-offset-2"
                          >
                            Abrir archivo
                          </a>
                        ) : (
                          // Ni un enlace roto ni un espacio en blanco: la leyenda que lo explica
                          // (FR-016a del Módulo 4).
                          <span className="atenuada">Sin archivo adjunto</span>
                        )}
                      </td>
                      <td className="w-8">
                        <MenuDeFila
                          etiqueta={`Acciones de ${documento.tipo.nombre} N° ${documento.numero}`}
                          items={[
                            { etiqueta: 'Corregir', onSeleccionar: () => setCorrigiendo(documento) },
                            {
                              etiqueta: 'Eliminar',
                              destructivo: true,
                              onSeleccionar: () => setDocumentoAEliminar(documento),
                            },
                          ]}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </TablaDesplazable>
          )}
        </FichaSeccion>
      </FichaCuerpo>

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
    </section>
  )
}
