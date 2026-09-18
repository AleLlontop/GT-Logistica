/**
 * Lee lo que dejó `InputImporte` —`1.240.000,50`— como número, o dice que falta o está mal escrito.
 *
 * Las dos pantallas del módulo que piden un importe lo leen igual; qué texto corresponde a cada caso lo
 * decide cada una, porque el saldo inicial admite cero y el importe de un movimiento no.
 */
export type ImporteLeido =
  | { tipo: 'vacio' }
  | { tipo: 'malEscrito' }
  | { tipo: 'valor'; valor: number }

export function leerImporte(texto: string): ImporteLeido {
  const normalizado = texto.replace(/\./g, '').trim().replace(',', '.')

  if (normalizado === '') return { tipo: 'vacio' }
  if (!/^-?\d+(\.\d{1,2})?$/.test(normalizado)) return { tipo: 'malEscrito' }

  return { tipo: 'valor', valor: Number(normalizado) }
}
