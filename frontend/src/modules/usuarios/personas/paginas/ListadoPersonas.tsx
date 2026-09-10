import { Aviso } from '../../../../compartido/ui/Aviso'
import { Estado } from '../../../../compartido/ui/Estado'
import { EstadoVacio } from '../../../../compartido/ui/EstadoVacio'
import { Filtros, FranjaDeBusqueda } from '../../../../compartido/ui/Filtros'
import { Listado, TablaDesplazable } from '../../../../compartido/ui/Listado'
import { MenuDeFila } from '../../../../compartido/ui/MenuDeFila'
import {
  clasesDeBoton,
  clasesDeEtiquetaDeFiltro,
  clasesDeFiltro,
} from '../../../../compartido/ui/clases'
import { IconoNuevo } from '../../../../compartido/ui/iconos'
import { EncabezadoDePantalla } from '../../../../compartido/ui/EncabezadoDePantalla'
import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ErrorHttp } from '../../../../compartido/clienteHttp'
import type { Persona } from '../../../../compartido/tipos'
import { DialogoConfirmacion } from '../../../../compartido/ui/DialogoConfirmacion'
import { formatearFecha, NOMBRE_DE_TIPO_INTEGRANTE } from '../../servicios/formato'
import { darDeBajaPersona, listarPersonas } from '../servicios/personas'

// Dos mensajes distintos a propósito (FR-025): "todavía no cargaste ninguna" y "tu búsqueda no
// encontró nada" son situaciones distintas y llevan a acciones distintas.
const MENSAJE_PADRON_VACIO =
  'Todavía no hay personas cargadas. Registrá la primera para poder asociarla a un usuario.'
const MENSAJE_SIN_COINCIDENCIAS = 'No hay personas que coincidan con la búsqueda.'

/**
 * Padrón de personas (User Story 6).
 *
 * El padrón arranca vacío en toda instalación nueva (FR-024), así que la lista vacía es el estado
 * inicial esperado y no un error.
 */
export function ListadoPersonas() {
  const [texto, setTexto] = useState('')
  const [personas, setPersonas] = useState<Persona[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [aBajar, setABajar] = useState<Persona | null>(null)

  const traer = useCallback(() => {
    listarPersonas({ texto })
      .then((lista) => {
        setPersonas(lista)
        setError(null)
      })
      .catch(() => setError('No pudimos traer el padrón. Volvé a intentar en unos minutos.'))
  }, [texto])

  useEffect(() => {
    traer()
  }, [traer])

  async function confirmarBaja() {
    if (aBajar === null) {
      return
    }

    try {
      await darDeBajaPersona(aBajar.id)
      setError(null)
      traer()
    } catch (fallo) {
      // Acá cae el rechazo de FR-028: la persona está vinculada a un usuario. El mensaje del
      // servidor identifica a cuál.
      setError(
        fallo instanceof ErrorHttp
          ? fallo.detalle.mensaje
          : 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
      )
    } finally {
      setABajar(null)
    }
  }

  const buscando = texto.trim() !== ''

  return (
    <section>
      <EncabezadoDePantalla
        titulo="Personas"
        accionPrincipal={
          <Link to="/personas/nueva" className={clasesDeBoton('primario')}>
            Nueva persona
            <span
                aria-hidden="true"
                className="flex size-8 shrink-0 items-center justify-center rounded-pastilla bg-white/[0.14] transition-transform duration-200 ease-gt group-hover:translate-x-0.5"
              >
                <IconoNuevo className="size-3" />
              </span>
          </Link>
        }
      />

      {error !== null && (
        <Aviso tono="error" rol="alert" className="mb-[18px]">
          {error}
        </Aviso>
      )}

      <Listado>
        {/* Su único buscador pasa a la franja de arriba, a todo el ancho (FR-033). */}
        <Filtros
          resumen={
            personas !== null && (
              <>
                <span>
                  {personas.length} {personas.length === 1 ? 'persona' : 'personas'}
                </span>
                <span>Ordenado por apellido y nombre</span>
              </>
            )
          }
        >
          <FranjaDeBusqueda>
            <div className="flex flex-col gap-1.5">
              <label htmlFor="busqueda" className={clasesDeEtiquetaDeFiltro}>
                Buscar por nombre, apellido o DNI
              </label>
              <input
                id="busqueda"
                type="search"
                value={texto}
                onChange={(evento) => setTexto(evento.target.value)}
                className={clasesDeFiltro(buscando)}
              />
            </div>
          </FranjaDeBusqueda>
        </Filtros>

        {personas === null && error === null && (
          <EstadoVacio caso="cargando" className="border-0 shadow-none">
            Cargando personas…
          </EstadoVacio>
        )}

        {personas !== null && personas.length === 0 && (
          <EstadoVacio
            caso={buscando ? 'sinCoincidencias' : 'vacio'}
            className="border-0 shadow-none"
          >
            {buscando ? MENSAJE_SIN_COINCIDENCIAS : MENSAJE_PADRON_VACIO}
          </EstadoVacio>
        )}

        {personas !== null && personas.length > 0 && (
          <TablaDesplazable>
            <table>
              <caption>Padrón de personas</caption>
              <thead>
                <tr>
                  {/*
                    **La fusión `Nombre` + `Apellido` + `DNI` → `Persona` no se aplica acá.** Es la
                    única de las quince que no se puede hacer: `ListadoPersonas.test.tsx` consulta
                    los tres encabezados por nombre —`getByRole('columnheader', …)`— y FR-069 sólo
                    autoriza a agregarle pasos de interacción, no a cambiar una aserción.
                    Para evitar el scroll horizontal sin romper la suite, acercamos visualmente
                    estas tres columnas reduciendo su padding.
                  */}
                  <th scope="col" className="!pr-2">Nombre</th>
                  <th scope="col" className="!px-2">Apellido</th>
                  <th scope="col" className="!pl-2">DNI</th>
                  <th scope="col">Tipo</th>
                  <th scope="col">Teléfono</th>
                  <th scope="col">Email</th>
                  <th scope="col">Fecha de nacimiento</th>
                  <th scope="col">Estado</th>
                  <th scope="col" className="w-8" aria-hidden="true" />
                </tr>
              </thead>
              <tbody>
                {/* **Sin enlace de fila**: su único destino es la edición, y editar es una acción
                    secundaria que va al `···`. No se le inventa una ficha de solo lectura. */}
                {personas.map((persona) => (
                  <tr key={persona.id}>
                    <td className="!pr-2">{persona.nombre}</td>
                    <td className="!px-2">{persona.apellido}</td>
                    <td className="!pl-2 font-mono">{persona.dni}</td>
                    <td>{NOMBRE_DE_TIPO_INTEGRANTE[persona.tipo]}</td>
                    <td>{persona.telefono}</td>
                    <td className="max-w-[160px] truncate" title={persona.email}>{persona.email}</td>
                    <td>{formatearFecha(persona.fechaNacimiento)}</td>
                    <td>
                      <Estado
                        valor={persona.activa ? 'activa' : 'dadoDeBaja'}
                        texto={persona.activa ? 'Activa' : 'Dada de baja'}
                        forma="punto"
                      />
                    </td>
                    <td className="w-8">
                      <MenuDeFila
                        etiqueta={`Acciones de ${persona.apellido}, ${persona.nombre}`}
                        items={[
                          { etiqueta: 'Editar', a: `/personas/${persona.id}/editar` },
                          ...(persona.activa
                            ? [
                                {
                                  etiqueta: 'Dar de baja',
                                  onSeleccionar: () => setABajar(persona),
                                  destructivo: true,
                                },
                              ]
                            : []),
                        ]}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TablaDesplazable>
        )}
      </Listado>

      {aBajar !== null && (
        <DialogoConfirmacion
          titulo="Dar de baja"
          mensaje={`¿Confirmás la baja de ${aBajar.nombre} ${aBajar.apellido}? Va a dejar de estar disponible para asociar a un usuario.`}
          onConfirmar={confirmarBaja}
          onCancelar={() => setABajar(null)}
        />
      )}
    </section>
  )
}
