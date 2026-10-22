import type { ReactNode } from 'react'
import { Boton } from '../../../compartido/ui/Boton'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { tipoDeReporteAnalisis } from '../servicios/servicioReportes'
import { useConsultaTemporal } from '../servicios/useConsultaTemporal'
import type { RecursoTemporal, RespuestasAnalisis } from '../servicios/tiposAnalisis'
import { SelectorPeriodo } from './SelectorPeriodo'
import { EstadoConsultaAnalisis } from './EstadoConsultaAnalisis'
import { GenerarReporte } from './GenerarReporte'
import { ResumenReporte } from './ResumenReporte'

export function ConsultaTemporal<R extends RecursoTemporal>({ titulo, consulta: c, filtros, children }: {
  titulo: string; consulta: ReturnType<typeof useConsultaTemporal<R>>; filtros?: ReactNode; children: (datos: RespuestasAnalisis[R]) => ReactNode
}) {
  return <>
    <EncabezadoDePantalla titulo={titulo} volverA={{ ruta: '/reportes', etiqueta: 'Reportes' }} accionSecundaria={c.solicitud.estado === 'listo' && <GenerarReporte reporte={tipoDeReporteAnalisis(c.recurso)} filtros={{ ...c.aplicados }} cantidadDeFilas={c.solicitud.datos.detalle.total} puedeEmitir={c.acceso.permisos.includes('reportes.emitir')} />} />
    {c.solicitud.estado !== 'inactivo' && <form noValidate className="mb-5 flex flex-wrap items-end gap-3 rounded-card border border-line bg-surface p-4" onSubmit={e => { e.preventDefault(); const invalido = Array.from(e.currentTarget.querySelectorAll<HTMLInputElement>('[role="combobox"]')).find(input => !input.checkValidity()); if (invalido) { invalido.reportValidity(); invalido.focus(); return }; c.aplicar() }}>
      <SelectorPeriodo valor={c.borrador} onCambiar={p => c.setBorrador(actual => ({ ...actual, ...p }))} mostrarErrores={c.intento} />
      {filtros}<Boton variante="secundario" type="submit">Aplicar filtros</Boton><Boton variante="secundario" onClick={c.limpiar}>Limpiar</Boton>
    </form>}
    <EstadoConsultaAnalisis solicitud={c.solicitud} />
    {c.solicitud.estado === 'listo' && <div className="space-y-5 text-ink"><ResumenReporte contexto={c.solicitud.datos.contexto}>
      <p>Los cambios de los campos quedan pendientes hasta aplicar. Este resultado corresponde a los filtros aplicados:</p>
      <ul>{Object.entries(c.aplicados).filter(([clave, valor]) => !['pagina', 'tamanioPagina', 'desde', 'hasta'].includes(clave) && valor !== undefined && valor !== '').map(([clave, valor]) => <li key={clave}>{({ clienteId: 'Cliente (referencia)', transportistaId: 'Transportista (referencia)', cajaId: 'Caja (referencia)', vehiculoId: 'Vehículo (referencia)', tipoVehiculoId: 'Tipo actual (referencia)', busqueda: 'Búsqueda', numero: 'Número de factura', estado: 'Estado', vigencia: 'Vigencia', activo: 'Actividad', orden: 'Orden' } as Record<string, string>)[clave] ?? clave}: {typeof valor === 'boolean' ? valor ? 'Activas' : 'Inactivas' : ({ enCurso: 'En curso', cantidadDesc: 'Mayor cantidad primero', cantidadAsc: 'Menor cantidad primero' } as Record<string, string>)[String(valor)] ?? String(valor)}</li>)}</ul>
    </ResumenReporte>{children(c.solicitud.datos)}</div>}
  </>
}
