import type { ImporteDecimal } from './tipos'
const MAXIMO_CENTAVOS = 999999999999999999n
function decimal(centavos: bigint): ImporteDecimal {
  return `${centavos / 100n}.${(centavos % 100n).toString().padStart(2, '0')}` as ImporteDecimal
}
export function netoCanonico(texto: string): ImporteDecimal | null {
  if (!/^(?:\d{1,3}(?:\.\d{3})+|\d+)?(?:,\d{0,2})?$/.test(texto) || !/\d/.test(texto)) return null
  const [entero, fraccion = ''] = texto.replace(/\./g, '').split(',')
  const centavos = BigInt(entero || '0') * 100n + BigInt(fraccion.padEnd(2, '0'))
  return centavos > 0n && centavos <= MAXIMO_CENTAVOS ? decimal(centavos) : null
}
export function calcularImportes(texto: string) {
  const neto = netoCanonico(texto)
  if (!neto) return null
  const centavos = BigInt(neto.replace('.', ''))
  const iva = (centavos * 21n + 50n) / 100n
  const total = centavos + iva
  return total <= MAXIMO_CENTAVOS ? { neto, iva: decimal(iva), importeTotal: decimal(total) } : null
}
