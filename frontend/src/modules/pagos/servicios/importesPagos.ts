import { netoCanonico } from '../../proveedores/servicios/importesFactura'
import type { ImporteDecimal } from './tipos'
/** Reutiliza sólo el parser exacto de 013; Pagos nunca calcula IVA. */
export const importeCanonico = netoCanonico
const MAXIMO = 999999999999999999n
export function centavos(importe: ImporteDecimal): bigint {
  if (!/^(0|[1-9]\d*)\.\d{2}$/.test(importe)) throw new Error('Importe decimal inválido.')
  const valor = BigInt(importe.replace('.', ''))
  if (valor > MAXIMO) throw new Error('Importe fuera de capacidad.')
  return valor
}
export function sumarImportes(importes: ImporteDecimal[]): ImporteDecimal | null {
  try {
    const total = importes.reduce((suma, importe) => suma + centavos(importe), 0n)
    return total <= MAXIMO ? `${total / 100n}.${(total % 100n).toString().padStart(2, '0')}` as ImporteDecimal : null
  } catch { return null }
}
