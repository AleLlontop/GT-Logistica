/**
 * Formateo de dinero del sistema, en un solo lugar.
 *
 * El Módulo 5 es el primero que maneja dinero, y el Principio II fija el formato argentino exacto:
 * **punto de miles y coma decimal**, siempre con dos decimales — `$ 1.240.000,00`.
 *
 * Existe por el mismo motivo por el que existe `compartido/fechas.ts`: la primera pantalla que lo
 * escriba distinto va a ser la que nadie revise. Con `toFixed(2)` los miles quedan sin separar y el
 * decimal sale con punto, que en una planilla argentina se lee como separador de miles: un total de
 * `1240000.00` puede leerse como ciento veinticuatro millones.
 *
 * Nunca se arma un `Intl.NumberFormat` a mano en una pantalla: se importa `formatearPesos`.
 */

const FORMATEADOR = new Intl.NumberFormat('es-AR', {
  style: 'currency',
  currency: 'ARS',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

/**
 * Un importe en pesos, como se lee acá: `$ 1.240.000,00`.
 *
 * El cero se muestra igual que cualquier otro importe (`$ 0,00`) y no como vacío: un viaje sin cargo
 * es un caso válido del negocio, y dejarlo en blanco lo confundiría con un dato que falta cargar
 * (FR-013).
 */
export function formatearPesos(importe: number): string {
  // `Intl` intercala un espacio duro entre el símbolo y el número; se normaliza a un espacio común
  // para que lo que se ve sea también lo que un test puede escribir.
  return FORMATEADOR.format(importe).replace(/ /g, ' ')
}
