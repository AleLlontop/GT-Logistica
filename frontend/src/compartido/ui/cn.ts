import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

/**
 * Compone clases resolviendo los conflictos entre utilidades.
 *
 * `clsx` arma la lista —descartando `false`, `null` y `undefined`— y `tailwind-merge` deja una sola
 * utilidad por propiedad: si una primitiva trae `px-3` y quien la usa pasa `px-6`, gana la de afuera
 * en vez de quedar las dos peleando por especificidad.
 */
export function cn(...clases: ClassValue[]) {
  return twMerge(clsx(clases))
}
