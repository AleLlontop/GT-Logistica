import type { OpcionMenu } from './tipos'

/** Organización visual: el servidor decide qué opciones existen; acá sólo se agrupan.
 * Los códigos desconocidos siguen visibles en Administración. */
export const SECCIONES = [
  'Proveedores', 'Viajes', 'Liquidaciones', 'Adelantos', 'Caja', 'Gestión de pagos',
  'Personas', 'Transportistas', 'Flota', 'Clientes', 'Reportes y seguimiento',
  'Configuración', 'Administración',
] as const
export type NombreDeSeccion = typeof SECCIONES[number]

const SECCION_POR_CODIGO: Record<string, NombreDeSeccion> = {
  proveedores: 'Proveedores',
  'cargar-factura-proveedor': 'Proveedores',
  'consultar-facturas-proveedores': 'Proveedores',
  viajes: 'Viajes', facturas: 'Viajes', totales: 'Viajes',
  'consultar-liquidacion': 'Liquidaciones', 'generar-liquidacion': 'Liquidaciones',
  'consultar-adelanto': 'Adelantos', 'registrar-adelanto': 'Adelantos',
  'consultar-caja': 'Caja', 'consultar-movimientos-caja': 'Caja',
  'generar-orden-proveedor': 'Gestión de pagos',
  'bancos-pagos': 'Gestión de pagos',
  'consultar-ordenes-proveedor': 'Gestión de pagos',
  'cheques-por-vencer': 'Gestión de pagos',
  personas: 'Personas', empleados: 'Personas', choferes: 'Personas',
  'vencimientos-choferes': 'Personas', transportistas: 'Transportistas',
  flota: 'Flota', 'tipos-vehiculo': 'Flota', 'vencimientos-flota': 'Flota',
  // Catálogo compartido por Choferes y Flota: no pertenece sólo a vehículos.
  'tipos-documentacion': 'Configuración',
  clientes: 'Clientes', reportes: 'Reportes y seguimiento',
  'vencimientos-facturas': 'Reportes y seguimiento',
  'totales-facturados': 'Reportes y seguimiento',
  'empresa-emisora': 'Configuración', usuarios: 'Administración',
}
export interface SeccionConOpciones { nombre: NombreDeSeccion; opciones: OpcionMenu[] }

/** Una única ubicación por opción, sin grupos vacíos ni cambios de permisos. */
export function agruparEnSecciones(opciones: OpcionMenu[]): SeccionConOpciones[] {
  return SECCIONES.map(nombre => ({
    nombre,
    opciones: opciones.filter(o => (SECCION_POR_CODIGO[o.codigo] ?? 'Administración') === nombre),
  })).filter(s => s.opciones.length > 0)
}

const PRIORIDADES = ['viajes', 'facturas', 'consultar-liquidacion', 'consultar-caja',
  'consultar-ordenes-proveedor', 'reportes', 'flota', 'proveedores']

/** Hasta ocho accesos autorizados. Si un rol sólo recibe otras opciones, también tiene entrada.
 * La prioridad es editorial, no un registro de frecuencia ni una regla de autorización. */
export function accesosPrioritarios(opciones: OpcionMenu[]): OpcionMenu[] {
  const principales = PRIORIDADES.flatMap(codigo => opciones.filter(o => o.codigo === codigo))
  return [...principales, ...opciones.filter(o => !PRIORIDADES.includes(o.codigo))].slice(0, 8)
}
