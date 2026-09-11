import { AsideDeFicha, BloqueDeAside } from '../../../compartido/ui/AsideDeFicha'
import { Boton } from '../../../compartido/ui/Boton'
import { FichaCuerpo, FichaSeccion } from '../../../compartido/ui/Ficha'
import { MenuDeFila } from '../../../compartido/ui/MenuDeFila'
import { IconoAnulado, IconoDocumento } from '../../../compartido/ui/iconos'
import { Estado } from '../../../compartido/ui/Estado'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { Dialogo } from '../../../compartido/ui/Dialogo'
import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { ConfirmacionBaja, type QueSeDaDeBaja } from '../componentes/ConfirmacionBaja'
import { FormularioDocumento } from '../documentacion/FormularioDocumento'
import { eliminarDocumento } from '../documentacion/servicioDocumentacion'
import { rutaDelArchivo } from '../servicios/api'
import {
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
      <section>
        <EncabezadoDePantalla titulo="Ficha del chofer" />
        <p role="alert">{error}</p>
        <Link to="/choferes">Volver al listado</Link>
      </section>
    )
  }

  if (chofer === null) {
    return (
      <section>
        <p role="status">Cargando ficha…</p>
      </section>
    )
  }

  /** El documento más próximo a vencer entre los vigentes: es la señal que la ficha destaca. */
  const proximoAvencer = chofer.documentos
    .filter((documento) => documento.esVigenteDelTipo)
    .slice()
    .sort((uno, otro) => uno.diasHastaVencimiento - otro.diasHastaVencimiento)[0]

  return (
    <section>
      <EncabezadoDePantalla
        titulo={`${chofer.apellido}, ${chofer.nombre}`}
        volverA={{ ruta: '/choferes', etiqueta: 'Volver al listado' }}
        resumen={
          <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <span className="font-mono">{chofer.dni}</span>
            <span aria-hidden="true">
              ·
            </span>
            <span>{chofer.transportista.nombre}</span>
            <span aria-hidden="true">
              ·
            </span>
            {/* Alta y baja acompañan y van como punto (FR-055). */}
            <Estado
              valor={chofer.activo ? 'activo' : 'inactivo'}
              texto={chofer.activo ? 'Activo' : 'Inactivo'}
              forma="punto"
            />
          </span>
        }
        accionPrincipal={
          <>
            {/*
              **La acción principal es *Cargar documento*** (FR-019): es lo que la ficha viene a
              hacer, y es lo que destraba al chofer para asignarle un viaje. *Editar chofer* la
              acompaña como secundaria y la baja es destructiva. **Ningún verbo se reescribe**
              (FR-066): dice *Cargar documento*, que no es lo mismo que el *Agregar documento* de
              flota, y los dos quedan como estaban.
            */}
            <Boton variante="secundario" onClick={() => navegar(`/choferes/${chofer.id}/editar`)}>
              Editar chofer
            </Boton>

            {/* Si está inactivo, en lugar de Dar de baja aparece Reactivar (FR-005b del Módulo 3):
                no conviven. */}
            {chofer.activo ? (
              <Boton
                variante="destructivo"
                icono={<IconoAnulado className="size-3" />}
                onClick={() =>
                  setAConfirmar({
                    tipo: 'chofer',
                    apellido: chofer.apellido,
                    nombre: chofer.nombre,
                  })
                }
              >
                Dar de baja
              </Boton>
            ) : (
              <Boton
                variante="secundario"
                onClick={() =>
                  setAConfirmar({
                    tipo: 'reactivarChofer',
                    apellido: chofer.apellido,
                    nombre: chofer.nombre,
                  })
                }
              >
                Reactivar
              </Boton>
            )}

            <Boton
              variante="primario"
              onClick={() => setCargandoDocumento(true)}
              icono={<IconoDocumento className="size-3" />}
            >
              Cargar documento
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
        <Dialogo
          titulo="Cargar documento"
          onCerrar={() => setCargandoDocumento(false)}
          className="w-[min(48rem,calc(100vw-2rem))]"
        >
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
        </Dialogo>
      )}

      {corrigiendo !== null && (
        <Dialogo
          titulo="Corregir documento"
          onCerrar={() => setCorrigiendo(null)}
          className="w-[min(48rem,calc(100vw-2rem))]"
        >
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
        </Dialogo>
      )}

      <FichaCuerpo
        aside={
          <AsideDeFicha
            /* El dato de más valor de un chofer es su semáforo de documentación: es lo que responde
               si puede salir a la ruta (data-model §8). */
            destacado={
              <div className="flex flex-col gap-2.5">
                <span className="text-[9.5px] font-bold tracking-[0.14em] text-encabezado uppercase">
                  Documentación
                </span>
                <Estado
                  valor={chofer.estadoDocumentacion}
                  texto={TEXTO_ESTADO_CHOFER[chofer.estadoDocumentacion]}
                  forma="pastilla"
                  className="text-[14px]"
                />
                {proximoAvencer !== undefined ? (
                  <p className="m-0 text-[12.5px] text-faint">
                    {proximoAvencer.tipo.nombre}: {textoDelPlazo(proximoAvencer.diasHastaVencimiento)}
                  </p>
                ) : (
                  /* El vacío se escribe: dice qué falta, nunca un guión (FR-038). */
                  <p className="atenuada m-0 text-[12.5px]">Sin documentación vigente cargada</p>
                )}
              </div>
            }
          >
            <BloqueDeAside titulo="Dependencia">
              <p className="m-0">{chofer.transportista.nombre}</p>
            </BloqueDeAside>
          </AsideDeFicha>
        }
      >
        <FichaSeccion titulo="Datos personales" id="titulo-datos-chofer">
          <dl>
            <dt>DNI</dt>
            <dd className="font-mono">{chofer.dni}</dd>
            <dt>CUIL</dt>
            <dd className="font-mono">{chofer.cuil}</dd>
            <dt>Fecha de nacimiento</dt>
            <dd>{formatearFecha(chofer.fechaNacimiento)}</dd>
            <dt>Teléfono</dt>
            <dd>{chofer.telefono}</dd>
            <dt>Email</dt>
            <dd>{chofer.email}</dd>
            <dt>Transportista</dt>
            <dd>{chofer.transportista.nombre}</dd>
          </dl>
        </FichaSeccion>

        <FichaSeccion titulo="Documentación" id="titulo-documentacion-chofer">
          {chofer.documentos.length === 0 && (
            <p role="status" className="m-0 px-[22px] py-5 text-[13px] leading-5 text-ink-soft">
              {MENSAJE_SIN_DOCUMENTACION}
            </p>
          )}

          {chofer.documentos.length > 0 && (
            <table className="w-full border-collapse text-[13px]">
              <caption className="sr-only">Documentos del chofer</caption>
              <thead className="bg-surface-soft">
                <tr className="[&>th]:border-b [&>th]:border-line [&>th]:px-5 [&>th]:py-2.5 [&>th]:text-left [&>th]:text-[9.5px] [&>th]:font-bold [&>th]:tracking-[0.14em] [&>th]:text-encabezado [&>th]:uppercase">
                  <th scope="col">Tipo</th>
                  <th scope="col">Número</th>
                  <th scope="col">Emisión</th>
                  <th scope="col">Vencimiento</th>
                  <th scope="col">Estado</th>
                  <th scope="col">Archivo</th>
                  <th scope="col" className="w-8" aria-hidden="true" />
                </tr>
              </thead>
              <tbody className="[&>tr]:border-b [&>tr]:border-line [&>tr:last-child]:border-b-0 [&_td]:px-5 [&_td]:py-[13px]">
                {chofer.documentos.map((documento) => (
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
                          documento.esVigenteDelTipo
                            ? textoDelPlazo(documento.diasHastaVencimiento)
                            : 'Reemplazado'
                        }
                      />
                    </td>
                    <td>
                      {documento.tieneArchivo ? (
                        <a
                          href={rutaDelArchivo(documento.id)}
                          target="_blank"
                          rel="noreferrer"
                          className="text-brand underline underline-offset-2"
                        >
                          Abrir archivo
                        </a>
                      ) : (
                        <span className="atenuada">Sin respaldo</span>
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
                            onSeleccionar: () => {
                              setDocumentoAEliminar(documento)
                              setAConfirmar({
                                tipo: 'documento',
                                tipoDocumento: documento.tipo.nombre,
                                numero: documento.numero,
                              })
                            },
                          },
                        ]}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </FichaSeccion>
      </FichaCuerpo>

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
    </section>
  )
}
