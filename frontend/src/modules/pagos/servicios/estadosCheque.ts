import type { EstadoCheque, SituacionCheque } from './tipos'
export const nombresEstadoCheque: Record<EstadoCheque, string> = { pendiente: 'Pendiente', entregado: 'Entregado', depositado: 'Depositado', cobrado: 'Cobrado', rechazado: 'Rechazado', anulado: 'Anulado' }
export const nombresSituacionCheque: Record<SituacionCheque, string> = { vencido: 'Vencido', proximoAvencer: 'Próximo a vencer', fueraDelPeriodoDeAlerta: 'Fuera del período de alerta' }
/** Sólo ofrece acciones: el servidor revalida la arista y el estado anterior. */
export const destinosCheque: Record<EstadoCheque, EstadoCheque[]> = { pendiente: ['entregado', 'anulado'], entregado: ['depositado', 'anulado'], depositado: ['cobrado', 'rechazado'], cobrado: [], rechazado: [], anulado: [] }
