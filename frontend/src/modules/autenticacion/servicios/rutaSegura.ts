/**
 * Validación del destino al que se vuelve tras autenticarse (FR-026).
 *
 * El sistema recuerda la ruta que el usuario quiso abrir y lo lleva ahí después de ingresar, pero
 * **sólo si es una pantalla de la propia aplicación**. Sin esta validación, un enlace preparado
 * podría usar la pantalla de ingreso para mandar a alguien a otro sitio después de que ingrese sus
 * credenciales.
 *
 * No alcanza con pedir que empiece con "/": hay tres formas conocidas de escribir un destino
 * externo que igual arranca con esa barra.
 */
const RUTA_POR_DEFECTO = '/'

export function rutaInternaSegura(destino: string | null | undefined): string {
  if (!destino) {
    return RUTA_POR_DEFECTO
  }

  // Las barras invertidas las normaliza el navegador a barras comunes, así que "/\ejemplo.com"
  // termina siendo "//ejemplo.com". Se descartan antes de cualquier otra comprobación.
  if (destino.includes('\\')) {
    return RUTA_POR_DEFECTO
  }

  // Tiene que ser una ruta absoluta dentro del sitio.
  if (!destino.startsWith('/')) {
    return RUTA_POR_DEFECTO
  }

  // "//ejemplo.com" es una URL relativa al protocolo: el navegador la interpreta como otro sitio.
  if (destino.startsWith('//')) {
    return RUTA_POR_DEFECTO
  }

  // "/ingresar" como destino dejaría al usuario dando vueltas en la misma pantalla.
  if (destino === '/ingresar' || destino.startsWith('/ingresar?')) {
    return RUTA_POR_DEFECTO
  }

  return destino
}
