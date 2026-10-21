import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { entregarArchivo, obtenerArchivo } from '../../../compartido/archivos'
import { generarReporte, tipoDeReporteAnalisis } from './servicioReportes'
import type { RecursoAnalisis } from './tiposAnalisis'

vi.mock('../../../compartido/archivos', () => ({ obtenerArchivo: vi.fn(), entregarArchivo: vi.fn() }))
beforeEach(() => { vi.mocked(obtenerArchivo).mockReset().mockResolvedValue({ blob: new Blob(['archivo']), nombre: 'reporte.pdf' }); vi.mocked(entregarArchivo).mockReset().mockReturnValue('descargado') })

describe('emisión de análisis por el servicio común', () => {
  it('las diez perspectivas conservan rutas, filtros aplicados y descartan página/tamaño', async () => {
    const recursos: RecursoAnalisis[] = ['viajes', 'facturacion', 'facturacion/cobros', 'liquidaciones', 'liquidaciones/pagos', 'caja', 'flota', 'vencimientos/choferes', 'vencimientos/flota', 'vencimientos/facturas']
    for (const recurso of recursos) {
      const filtros = Object.freeze(recurso.startsWith('vencimientos') ? { pagina: 2, tamanioPagina: 1 } : { desde: '2026-10-01', hasta: '2026-10-31', pagina: 2, tamanioPagina: 1 })
      await generarReporte(tipoDeReporteAnalisis(recurso), 'excel', filtros)
      const ruta = vi.mocked(obtenerArchivo).mock.lastCall?.[0]
      expect(ruta).toContain(`/reportes/${recurso}/exportacion?`)
      expect(ruta).toContain('formato=excel'); expect(ruta).not.toContain('pagina='); expect(ruta).not.toContain('tamanioPagina=')
      expect(filtros.pagina).toBe(2)
    }
  })
  it('false no desaparece, PDF abre y Excel descarga; rutas originales siguen presentes', async () => {
    await generarReporte('analisis-flota', 'pdf', { desde: '2026-10-01', hasta: '2026-10-31', activo: false, tipoVehiculoId: 3 })
    expect(obtenerArchivo).toHaveBeenLastCalledWith('/reportes/flota/exportacion?desde=2026-10-01&hasta=2026-10-31&activo=false&tipoVehiculoId=3&formato=pdf')
    expect(entregarArchivo).toHaveBeenLastCalledWith(expect.anything(), 'abrir')
    await generarReporte('viajes', 'excel', { clienteId: 3 })
    expect(obtenerArchivo).toHaveBeenLastCalledWith('/viajes/reporte?clienteId=3&formato=excel')
    expect(entregarArchivo).toHaveBeenLastCalledWith(expect.anything(), 'descargar')
  })
  it('tope y precisión mantienen mensaje servidor y no entregan ningún archivo', async () => {
    vi.mocked(obtenerArchivo).mockRejectedValueOnce(new ErrorHttp(409, { codigo: 'precision_monetaria_xlsx', mensaje: 'No se conservan centavos.' }))
    expect(await generarReporte('analisis-pagos', 'excel')).toEqual({ estado: 'precision_monetaria_xlsx', mensaje: 'No se conservan centavos.' })
    vi.mocked(obtenerArchivo).mockRejectedValueOnce(new ErrorHttp(409, { codigo: 'tope_de_filas_superado', mensaje: 'Acotá el reporte.', filas: 5001, tope: 5000 } as never))
    expect(await generarReporte('analisis-pagos', 'excel')).toMatchObject({ estado: 'tope_superado', filas: 5001, tope: 5000, mensaje: 'Acotá el reporte.' })
    expect(entregarArchivo).not.toHaveBeenCalled()
  })
})
