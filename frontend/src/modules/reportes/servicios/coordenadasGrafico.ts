import { centavosAnalisis } from './servicioAnalisis'

export interface DatoGrafico { etiqueta: string; valores: Record<string, string | number> }
export interface SerieGrafico { clave: string; nombre: string }
export interface CoordenadaGrafico { etiqueta: string; original: DatoGrafico; [clave: string]: string | number | DatoGrafico }

/** Sólo coordenadas aproximadas de dibujo. No suma importes ni sustituye los datos exactos. */
export function coordenadasGrafico(datos: DatoGrafico[], series: SerieGrafico[], monetario: boolean) {
  let maximo = 0n
  for (const dato of datos) for (const serie of series) {
    const valor = dato.valores[serie.clave]
    if (monetario) {
      if (typeof valor !== 'string') throw new Error('El gráfico monetario requiere cadenas exactas.')
      const centavos = centavosAnalisis(valor), absoluto = centavos < 0n ? -centavos : centavos
      if (absoluto > maximo) maximo = absoluto
    } else if (typeof valor !== 'number' || !Number.isSafeInteger(valor) || valor < 0) {
      throw new Error('La cantidad del gráfico debe ser un entero representable.')
    }
  }
  let factorPesos = 1n
  while (monetario && maximo > 100n * factorPesos * 1000n) factorPesos *= 1000n
  const escalas: Record<string, string> = { '1': 'ARS', '1000': 'Miles de ARS', '1000000': 'Millones de ARS', '1000000000': 'Miles de millones de ARS', '1000000000000': 'Billones de ARS' }
  const unidad = monetario ? escalas[String(factorPesos)] ?? `ARS × ${factorPesos.toLocaleString('es-AR')}` : 'Cantidad'
  const coordenadas: CoordenadaGrafico[] = datos.map(dato => {
    const fila: CoordenadaGrafico = { etiqueta: dato.etiqueta, original: dato }
    for (const serie of series) {
      if (serie.clave === 'etiqueta' || serie.clave === 'original') throw new Error('Clave de serie inválida.')
      fila[serie.clave] = monetario
        ? Number(centavosAnalisis(dato.valores[serie.clave] as string) * 1_000_000n / (100n * factorPesos)) / 1_000_000
        : dato.valores[serie.clave]
    }
    return fila
  })
  return { coordenadas, unidad, factorPesos }
}
