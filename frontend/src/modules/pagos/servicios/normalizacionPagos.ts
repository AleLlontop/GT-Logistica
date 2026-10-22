const BLANCOS = new Set([9, 10, 11, 12, 13, 32, 133, 160, 5760,
  ...Array.from({ length: 11 }, (_, i) => 8192 + i), 8232, 8233, 8239, 8287, 12288])
/** Paridad con String.Trim()/Char.IsWhiteSpace de .NET y el CHECK de 014; no toca contenido interno. */
export function trimExterior(valor: string): string {
  let inicio = 0; let fin = valor.length
  while (inicio < fin && BLANCOS.has(valor.charCodeAt(inicio))) inicio++
  while (fin > inicio && BLANCOS.has(valor.charCodeAt(fin - 1))) fin--
  return valor.slice(inicio, fin)
}
export function errorNombreBanco(nombre: string): string {
  const normalizado = trimExterior(nombre)
  return !normalizado ? 'Escribí el nombre del banco.' : normalizado.length > 200
    ? 'No puede superar 200 caracteres después de quitar los espacios exteriores' : ''
}
