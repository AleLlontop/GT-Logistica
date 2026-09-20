import { EnlaceDeFila } from '../../../compartido/ui/EnlaceDeFila'
import { EncabezadoDeChevron, FilaNavegable } from '../../../compartido/ui/FilaNavegable'
import { Estado } from '../../../compartido/ui/Estado'
import { Aviso } from '../../../compartido/ui/Aviso'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { Listado, TablaDesplazable } from '../../../compartido/ui/Listado'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useEffect, useState } from 'react'
import { formatearFecha, TEXTO_ESTADO_DOCUMENTO, textoDelPlazo } from '../servicios/estados'
import { GenerarReporte } from '../../reportes/componentes/GenerarReporte'
import { listarVencimientos, type AlertaVencimiento } from '../servicios/servicioChoferes'

const MENSAJE_SIN_VENCIMIENTOS = 'No hay documentación próxima a vencer ni vencida.'

interface Props {
  /**
   * Si la sesión tiene `choferes.gestionar`. Decide el *volver*, nada más: el panel va bajo su propio
   * permiso de lectura, que también tiene Gerencia, y para ella `/choferes` es un 403. Ofrecer una
   * salida a una pantalla que no se puede abrir es peor que no ofrecer ninguna (convención [005]).
   */
  puedeVolverAlListado: boolean
  /** `reportes.emitir`. Sin él, *Generar reporte* no se dibuja (Módulo 12, FR-013). */
  puedeEmitirReportes: boolean
}

/**
 * Panel de vencimientos (User Story 5).
 *
 * Muestra, al entrar al módulo, qué choferes necesitan renovar algo. **Nadie ejecuta nada**: el
 * estado se calcula al consultar, así que un documento entra al panel solo el día que le toca
 * (FR-019).
 *
 * Sólo entran choferes activos y documentos vigentes de su tipo: un chofer dado de baja no alerta
 * aunque tenga todo vencido, y una licencia vieja ya renovada tampoco (FR-021, FR-020a).
 */
export function PanelVencimientos({ puedeVolverAlListado, puedeEmitirReportes }: Props) {
  const [alertas, setAlertas] = useState<AlertaVencimiento[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    listarVencimientos()
      .then((lista) => {
        setAlertas(lista)
        setError(null)
      })
      .catch(() =>
        setError('No pudimos traer los vencimientos. Volvé a intentar en unos minutos.'),
      )
  }, [])

  return (
    <section>
      {/*
        El *volver* pasa de `accionPrincipal` a `volverA` (FR-022): es una **salida**, no una
        alternativa a la acción principal, y va arriba a la izquierda, fuera de la línea de decisión.
        El texto, el rol y la etiqueta accesible no cambian. Este panel es de solo lectura y **no
        lleva acción principal**: no se le inventa una para llenar el lugar (FR-017).
      */}
      <EncabezadoDePantalla
        titulo="Vencimientos"
        volverA={
          puedeVolverAlListado
            ? { ruta: '/choferes', etiqueta: 'Volver al listado de choferes' }
            : undefined
        }
        /*
          Módulo 12: *Generar reporte* es **secundaria**, y la pantalla **sigue sin acción primaria**.
          No se la promueve por descarte: el propósito de un panel de vencimientos es resolver lo que
          está por vencer, no exportarlo (FR-005). `Volver al listado` sigue siendo terciaria.

          El panel no tiene filtros y esta feature no se los agrega: el reporte se lleva el panel
          completo y su encabezado dice `Sin filtros aplicados`.
        */
        accionSecundaria={
          <GenerarReporte
            reporte="vencimientos-choferes"
            puedeEmitir={puedeEmitirReportes}
            cantidadDeFilas={alertas?.length ?? 0}
          />
        }
      />
      {error !== null && (
        <Aviso tono="error" rol="alert" className="mb-4">
          {error}
        </Aviso>
      )}

      {alertas === null && error === null && (
        <EstadoVacio caso="cargando" className="border-0 shadow-none">
          Cargando vencimientos…
        </EstadoVacio>
      )}

      {/* Una lista vacía es una buena noticia, y se dice: no se muestra una tabla vacía. */}
      {alertas !== null && alertas.length === 0 && <EstadoVacio caso="vacio" className="border-0 shadow-none">
          {MENSAJE_SIN_VENCIMIENTOS}
        </EstadoVacio>}

      {alertas !== null && alertas.length > 0 && (
        <Listado>
          <TablaDesplazable>
            <table>
          <caption>Documentación próxima a vencer o vencida</caption>
          <thead>
            <tr>
              <th scope="col">Chofer</th>
              <th scope="col">Transportista</th>
              {/* `Documento` + `Vencimiento` nombran un solo concepto (FR-047): el nombre arriba
                  y la fecha debajo, en menor jerarquía. */}
              <th scope="col">Documento</th>
              <th scope="col">Estado</th>
              <EncabezadoDeChevron />
            </tr>
          </thead>
          <tbody>
            {/* Ordenadas por urgencia desde el servidor: primero lo vencido hace más tiempo. */}
            {alertas.map((alerta) => (
              <FilaNavegable key={alerta.documento.id} a={`/choferes/${alerta.choferId}`}>
                <td>
                  <EnlaceDeFila a={`/choferes/${alerta.choferId}`}>
                    {alerta.apellido}, {alerta.nombre}
                  </EnlaceDeFila>
                </td>
                <td>{alerta.transportista.nombre}</td>
                <td>
                  <span className="flex flex-col gap-0.5">
                    <span>
                      {alerta.documento.tipo.nombre} N°{' '}
                      <span className="font-mono">{alerta.documento.numero}</span>
                    </span>
                    <span className="text-[11.5px] text-faint">
                      Vence el {formatearFecha(alerta.documento.fechaVencimiento)}
                    </span>
                  </span>
                </td>
                <td>
                  <Estado
                    valor={alerta.documento.estado}
                    texto={TEXTO_ESTADO_DOCUMENTO[alerta.documento.estado]}
                    forma="pastilla"
                    detalle={textoDelPlazo(alerta.documento.diasHastaVencimiento)}
                  />
                </td>
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
