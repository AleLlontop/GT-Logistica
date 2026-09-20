import { TokenDeIdentificador } from '../../../compartido/ui/TokenDeIdentificador'
import { EncabezadoDeChevron, FilaNavegable } from '../../../compartido/ui/FilaNavegable'
import { Aviso } from '../../../compartido/ui/Aviso'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useEffect, useState } from 'react'
import { formatearFecha } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { situacion } from '../servicios/api'
import { GenerarReporte } from '../../reportes/componentes/GenerarReporte'
import { consultarVencimientos, type FilaDeVencimiento } from '../servicios/servicioFacturas'

export const MENSAJE_PANEL_VACIO =
  'No hay facturas vencidas ni por vencer en los próximos 7 días.'

/**
 * Panel de vencimientos (User Story 5, FR-063).
 *
 * Las facturas `vencida` y las que vencen dentro de los próximos **7 días corridos**. Las `pagada` y
 * `anulada` no figuran: la exclusión vive en la consulta del servidor, no acá.
 *
 * **La situación va con la palabra, no sólo con un color** (FR-065): `Vencida hace 3 días`,
 * `Vence en 5 días`, `Vence hoy`. Es la diferencia entre un panel que se puede leer y uno que hay que
 * interpretar.
 *
 * **Un panel vacío es una respuesta legítima** y se dice con esas palabras, en vez de mostrar una tabla sin
 * filas que se lee como un error de carga.
 */
interface Props {
  /** `reportes.emitir`. Sin él, *Generar reporte* no se dibuja (Módulo 12, FR-013). */
  puedeEmitirReportes: boolean
}

export function PanelVencimientos({ puedeEmitirReportes }: Props) {
  const [filas, setFilas] = useState<FilaDeVencimiento[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let vigente = true

    consultarVencimientos()
      .then((traidas) => {
        if (vigente) setFilas(traidas)
      })
      .catch(() => {
        if (vigente) {
          setError('No pudimos traer los vencimientos. Volvé a intentar en unos minutos.')
        }
      })

    return () => {
      vigente = false
    }
  }, [])

  return (
    <section>
      {/*
        **Este panel no tenía ninguna acción de encabezado, ni siquiera un volver** (FR-022): era la
        única de las cuatro pantallas de solo lectura sin salida visible. El texto es nuevo y lo
        escribe esta feature, siguiendo la forma de los que ya existen en choferes y flota (FR-065).
      */}
      <EncabezadoDePantalla
        titulo="Vencimientos de facturas"
        volverA={{ ruta: '/facturas', etiqueta: 'Volver al listado de facturas' }}
        /* Mismo criterio que los otros dos paneles: secundaria, y la pantalla sigue sin primaria. */
        accionSecundaria={
          <GenerarReporte
            reporte="vencimientos-facturas"
            puedeEmitir={puedeEmitirReportes}
            cantidadDeFilas={filas?.length ?? 0}
          />
        }
      />

      {error !== null && (
        <Aviso tono="error" rol="alert" className="mb-4">
          {error}
        </Aviso>
      )}

      {filas === null && error === null && (
        <EstadoVacio caso="cargando" className="border-0 shadow-none">
          Cargando vencimientos…
        </EstadoVacio>
      )}

      {filas !== null && filas.length === 0 && <EstadoVacio caso="vacio" className="border-0 shadow-none">
          {MENSAJE_PANEL_VACIO}
        </EstadoVacio>}

      {filas !== null && filas.length > 0 && (
        <Listado>
          <TablaDesplazable>
            <table>
          <caption>Facturas vencidas y por vencer en los próximos 7 días</caption>
          <thead>
            <tr>
              <th scope="col">Cliente</th>
              <th scope="col">Número</th>
              <th scope="col" className="text-right">Importe</th>
              <th scope="col">Vencimiento</th>
              <th scope="col">Situación</th>
              <EncabezadoDeChevron />
            </tr>
          </thead>
          <tbody>
            {filas.map((fila) => (
              <FilaNavegable key={fila.id} a={`/facturas/${fila.id}`}>
                <td>{fila.cliente}</td>
                <td>
                  <TokenDeIdentificador
                    a={`/facturas/${fila.id}`}
                    numero={fila.numeroComprobante}
                  />
                </td>
                <td className="text-right font-bold whitespace-nowrap">
                  {formatearPesos(fila.total)}
                </td>
                <td>{formatearFecha(fila.vencimientoPago)}</td>
                {/* La palabra, no el color (FR-055). */}
                <td>{situacion(fila.dias)}</td>
              </FilaNavegable>
            ))}
          </tbody>
        </table>
          </TablaDesplazable>
        </Listado>
      )}
    </section>
  )
}
