import type { PeriodoAnalisis } from './tiposAnalisis'

/** Mismo corte fijo −03 de servidor, aunque el navegador esté configurado en otra zona. */
export function mesActualArgentina(instante = new Date()): PeriodoAnalisis {
  const argentina = new Date(instante.getTime() - 3 * 60 * 60 * 1000)
  const anio = argentina.getUTCFullYear(), mes = argentina.getUTCMonth()
  const ultimo = new Date(Date.UTC(anio, mes + 1, 0)).getUTCDate()
  const prefijo = `${anio}-${String(mes + 1).padStart(2, '0')}`
  return { desde: `${prefijo}-01`, hasta: `${prefijo}-${String(ultimo).padStart(2, '0')}` }
}
export function fechaAnalisisValida(texto: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(texto)) return false
  const [anio, mes, dia] = texto.split('-').map(Number)
  if (anio < 1 || anio > 9999 || mes < 1 || mes > 12 || dia < 1 || dia > 31) return false
  const fecha = new Date(0); fecha.setUTCFullYear(anio, mes - 1, dia)
  return fecha.getUTCFullYear() === anio && fecha.getUTCMonth() === mes - 1 && fecha.getUTCDate() === dia
}
export function erroresPeriodo(periodo: PeriodoAnalisis): Partial<Record<keyof PeriodoAnalisis, string>> {
  const errores: Partial<Record<keyof PeriodoAnalisis, string>> = {}
  if (!periodo.desde) errores.desde = 'Elegí la fecha desde.'
  else if (!fechaAnalisisValida(periodo.desde)) errores.desde = 'Elegí una fecha válida.'
  if (!periodo.hasta) errores.hasta = 'Elegí la fecha hasta.'
  else if (!fechaAnalisisValida(periodo.hasta)) errores.hasta = 'Elegí una fecha válida.'
  if (!errores.desde && !errores.hasta && periodo.desde > periodo.hasta) errores.hasta = 'La fecha hasta no puede ser anterior a desde.'
  return errores
}

export function instanteArgentina(iso: string): string {
  return new Intl.DateTimeFormat('es-AR', { timeZone: 'America/Argentina/Buenos_Aires', day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }).format(new Date(iso))
}
