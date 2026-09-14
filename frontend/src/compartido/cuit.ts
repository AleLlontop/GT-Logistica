/**
 * Formateo del CUIT, en un solo lugar (Módulo 9, research §11b).
 *
 * Existía copiado como función local en *Transportistas* y en *Clientes*. La tercera necesidad —la
 * liquidación lo muestra en tres lugares— es la que lo trae acá, y las dos copias pasan a importarlo.
 *
 * **Conserva exactamente la regla de las copias, incluido el caso borde**: once caracteres salen
 * `XX-XXXXXXXX-X` y cualquier otro largo sale sin tocar. Un CUIT guardado siempre tiene once dígitos,
 * así que ese caso no se "arregla": cambiarlo sería un cambio de comportamiento disfrazado de refactor.
 */
export function formatearCuit(cuit: string): string {
  if (cuit.length !== 11) return cuit

  return `${cuit.slice(0, 2)}-${cuit.slice(2, 10)}-${cuit.slice(10)}`
}
