import type { OpcionMenu } from './tipos'

/**
 * Cómo se agrupan las opciones de menú que manda el servidor.
 *
 * **El servidor sigue decidiendo qué existe; la pantalla decide dónde se dibuja.** La regla del
 * Módulo 2 —"el frontend dibuja lo que recibe y no tiene lógica propia de permisos"— protege que el
 * frontend no invente opciones, y este mapa no puede: no menciona ni un permiso, y una opción que no
 * llegó no se dibuja aunque figure acá (research §6).
 *
 * Un código que el mapa no conozca **se dibuja igual**, en la última sección. Es lo que permite que
 * un módulo futuro aparezca en el menú sin tocar el frontend, que es la propiedad que la regla
 * original buscaba conservar.
 */

export type NombreDeSeccion =
  | 'Operación'
  | 'Padrones'
  | 'Seguimiento'
  | 'Configuración'
  | 'Administración'

/** El orden de las secciones en pantalla. La última recibe los códigos desconocidos. */
export const SECCIONES: NombreDeSeccion[] = [
  'Operación',
  'Padrones',
  'Seguimiento',
  'Configuración',
  'Administración',
]

const SECCION_POR_CODIGO: Record<string, NombreDeSeccion> = {
  // Lo que se opera todos los días
  viajes: 'Operación',
  facturas: 'Operación',

  // Los padrones sobre los que se opera
  clientes: 'Padrones',
  choferes: 'Padrones',
  flota: 'Padrones',
  transportistas: 'Padrones',
  personas: 'Padrones',

  // Lo que se mira para saber cómo viene la cosa
  'vencimientos-choferes': 'Seguimiento',
  'vencimientos-flota': 'Seguimiento',
  'vencimientos-facturas': 'Seguimiento',
  totales: 'Seguimiento',
  'totales-facturados': 'Seguimiento',

  // Los catálogos que se tocan de vez en cuando
  'tipos-documentacion': 'Configuración',
  'tipos-vehiculo': 'Configuración',
  'empresa-emisora': 'Configuración',

  // Quién entra al sistema
  usuarios: 'Administración',
}

export interface SeccionConOpciones {
  nombre: NombreDeSeccion
  opciones: OpcionMenu[]
}

/**
 * Reparte las opciones recibidas en sus secciones, **descartando las secciones que quedaron
 * vacías**: una sección sin ninguna opción autorizada no se muestra, ni siquiera vacía (FR-012).
 */
export function agruparEnSecciones(opciones: OpcionMenu[]): SeccionConOpciones[] {
  const ultima = SECCIONES[SECCIONES.length - 1]

  return SECCIONES.map((nombre) => ({
    nombre,
    opciones: opciones.filter(
      (opcion) => (SECCION_POR_CODIGO[opcion.codigo] ?? ultima) === nombre,
    ),
  })).filter((seccion) => seccion.opciones.length > 0)
}
