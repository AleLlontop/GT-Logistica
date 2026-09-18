import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { formatearInstante } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import {
  clasesDeAvisoDePantalla,
  clasesDeBoton,
  clasesDeCirculoDeIcono,
} from '../../../compartido/ui/clases'
import { cn } from '../../../compartido/ui/cn'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { EnlaceDeFila } from '../../../compartido/ui/EnlaceDeFila'
import { Estado } from '../../../compartido/ui/Estado'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { EncabezadoDeChevron, FilaNavegable } from '../../../compartido/ui/FilaNavegable'
import { IconoNuevo } from '../../../compartido/ui/iconos'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { Paginacion } from '../../../compartido/ui/Paginacion'
import {
  esSinPermiso,
  listarCajas,
  MENSAJE_SIN_PERMISO,
  NOMBRES_DE_ESTADO,
  obtenerMiCajaAbierta,
  type CajaDetalle,
  type PaginaDeCajas,
} from '../servicios/servicioCaja'

const TITULO = 'Cajas'

interface Props {
  /** `caja.gestionar`. Quien sólo consulta ve el listado sin *Abrir caja* (FR-033). */
  puedeGestionar: boolean
}

/**
 * Consulta de cajas (FR-026): las de todos los responsables, la más reciente primero.
 *
 * **Es la entrada del módulo y donde vive *Abrir caja***: con `gestionar` se pregunta si el usuario ya tiene
 * una abierta, y en ese caso se ofrece ir a ella en vez de abrir otra (RN1).
 *
 * **Quien llega sin permiso escribiendo la dirección** ve que le falta permiso y a quién pedírselo: lo decide
 * el `403` de la carga (convención [010]).
 */
export function ConsultaDeCajas({ puedeGestionar }: Props) {
  const [pagina, setPagina] = useState(1)
  const [resultado, setResultado] = useState<PaginaDeCajas | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [sinPermiso, setSinPermiso] = useState(false)

  // `undefined` mientras no se sabe: no se ofrece ni abrir ni ir a la propia hasta tener la respuesta.
  const [propia, setPropia] = useState<CajaDetalle | null | undefined>(undefined)

  const traer = useCallback(() => {
    listarCajas(pagina)
      .then((traida) => {
        setResultado(traida)
        setError(null)
      })
      .catch((fallo) => {
        if (esSinPermiso(fallo)) {
          setSinPermiso(true)
        } else {
          setError('No pudimos traer las cajas. Volvé a intentar en unos minutos.')
        }
      })
  }, [pagina])

  useEffect(() => {
    traer()
  }, [traer])

  useEffect(() => {
    if (!puedeGestionar) return

    obtenerMiCajaAbierta()
      .then(setPropia)
      .catch(() => setPropia(undefined))
  }, [puedeGestionar])

  if (sinPermiso) {
    return (
      <section>
        <EncabezadoDePantalla titulo={TITULO} />
        <p role="alert" className={clasesDeAvisoDePantalla.advertencia}>
          {MENSAJE_SIN_PERMISO}
        </p>
      </section>
    )
  }

  const accion =
    !puedeGestionar || propia === undefined ? undefined : propia === null ? (
      <Link to="/caja/nueva" className={clasesDeBoton('primario')}>
        Abrir caja
        <span aria-hidden="true" className={clasesDeCirculoDeIcono}>
          <IconoNuevo className="size-3" />
        </span>
      </Link>
    ) : (
      <Link to={`/caja/${propia.id}`} className={clasesDeBoton('terciario')}>
        Ir a mi caja abierta
      </Link>
    )

  return (
    <section>
      <EncabezadoDePantalla titulo={TITULO} accionPrincipal={accion} />

      {error !== null && (
        <p role="alert" className={cn(clasesDeAvisoDePantalla.error, 'mb-4')}>
          {error}
        </p>
      )}

      <Listado>
        {resultado === null && error === null && (
          <EstadoVacio caso="cargando" className="border-0 shadow-none">
            Cargando cajas…
          </EstadoVacio>
        )}

        {resultado !== null && resultado.items.length === 0 && (
          <EstadoVacio caso="vacio" className="border-0 shadow-none">
            {`Todavía no se abrió ninguna caja.${
              puedeGestionar ? ' Abrí la tuya con el saldo inicial del día.' : ''
            }`}
          </EstadoVacio>
        )}

        {resultado !== null && resultado.items.length > 0 && (
          <TablaDesplazable>
            <table>
              <caption>{TITULO}</caption>
              <thead>
                <tr>
                  <th scope="col">Responsable</th>
                  <th scope="col">Apertura</th>
                  <th scope="col">Estado</th>
                  <th scope="col">Cierre</th>
                  <th scope="col" className="text-right">
                    Saldo final
                  </th>
                  <EncabezadoDeChevron />
                </tr>
              </thead>
              <tbody>
                {resultado.items.map((caja) => {
                  const destino = `/caja/${caja.id}`
                  const apertura = formatearInstante(caja.fechaApertura)

                  return (
                    <FilaNavegable key={caja.id} a={destino}>
                      <td>
                        {/* Un responsable tiene varias cajas: el nombre accesible suma la apertura, entero
                            en un solo `sr-only` (convención [010]). */}
                        <EnlaceDeFila a={destino}>
                          <span className="sr-only">{`Ver caja de ${caja.responsable.nombre} abierta el ${apertura}`}</span>
                          <span aria-hidden="true">{caja.responsable.nombre}</span>
                        </EnlaceDeFila>
                      </td>
                      <td className="whitespace-nowrap">{apertura}</td>
                      <td>
                        <Estado valor={caja.estado} texto={NOMBRES_DE_ESTADO[caja.estado]} forma="pastilla" />
                      </td>
                      <td className="whitespace-nowrap">
                        {caja.fechaCierre !== null ? formatearInstante(caja.fechaCierre) : ''}
                      </td>
                      <td className="text-right font-bold whitespace-nowrap">
                        {caja.saldoFinal !== null ? formatearPesos(caja.saldoFinal) : ''}
                      </td>
                    </FilaNavegable>
                  )
                })}
              </tbody>
            </table>
          </TablaDesplazable>
        )}
      </Listado>

      {resultado !== null && (
        <Paginacion
          pagina={resultado.pagina}
          total={resultado.total}
          tamanioPagina={resultado.tamanioPagina}
          nombrePlural="cajas"
          onCambiarPagina={setPagina}
        />
      )}
    </section>
  )
}
