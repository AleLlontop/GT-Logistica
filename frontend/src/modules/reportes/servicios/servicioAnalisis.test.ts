import { afterEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp, registrarManejadorDeSesionExpirada } from '../../../compartido/clienteHttp'
import { formatearPesos } from '../../../compartido/moneda'
import { centavosAnalisis, consultarAnalisis, consultarDashboard, consultarGrupos, consultarReferencias, queryAnalisis, RUTAS_REFERENCIAS } from './servicioAnalisis'
import type { AnalisisViajes, RecursoAnalisis } from './tiposAnalisis'

const periodo = { desde: '2026-10-01', hasta: '2026-10-31' }
function responder(valor: unknown, estado = 200) {
  const fetch = vi.fn().mockImplementation(() => Promise.resolve(new Response(JSON.stringify(valor), { status: estado, headers: { 'Content-Type': 'application/json' } })))
  vi.stubGlobal('fetch', fetch)
  return fetch
}
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); registrarManejadorDeSesionExpirada(() => {}) })

describe('cliente de análisis015', () => {
  it('conserva todos los centavos, agregados y fechas sin Number ni almacenamiento', async () => {
    const guardar = vi.spyOn(Storage.prototype, 'setItem')
    const historia = vi.spyOn(window.history, 'pushState')
    const maximo = '9999999999999999.99'
    const suma = '19999999999999999.98'
    const datos: AnalisisViajes = {
      contexto: { ...periodo, consultadoEn: '2026-10-20T12:00:00Z', hoyArgentina: '2026-10-20', criterioTemporal: 'fechaViaje' },
      resumen: { cantidad: 1, realizados: 1, pendientesRendicion: 0, pendientes: 0, anulados: 0, importeTotal: suma }, series: [],
      distribuciones: { estado: { top: [], otros: null, totalCategorias: 0 }, cliente: { top: [], otros: null, totalCategorias: 0 }, transportista: { top: [], otros: null, totalCategorias: 0 } },
      detalle: { items: [{ id: 1, numero: 1, fecha: '2026-10-18', cliente: { id: 3, nombre: 'Cliente' }, origen: 'Rosario', destino: 'Córdoba', chofer: null, vehiculo: null, transportista: null, estado: 'rendido', importe: maximo }], total: 1, pagina: 1, tamanioPagina: 20 },
    }
    const fetch = responder(datos)
    const resultado = await consultarAnalisis('viajes', { ...periodo, clienteId: 3 })
    expect(resultado.detalle.items[0].importe).toBe(maximo)
    expect(resultado.resumen.importeTotal).toBe(suma)
    expect(resultado.detalle.items[0].fecha).toBe('2026-10-18')
    expect(formatearPesos(resultado.resumen.importeTotal)).toBe('$ 19.999.999.999.999.999,98')
    expect(centavosAnalisis(suma)).toBe(1999999999999999998n)
    expect(centavosAnalisis('-12345678901234567.89')).toBe(-1234567890123456789n)
    expect(formatearPesos('-12345678901234567.89')).toBe('-$ 12.345.678.901.234.567,89')
    expect(fetch).toHaveBeenCalledWith('/api/reportes/viajes?desde=2026-10-01&hasta=2026-10-31&clienteId=3', expect.objectContaining({ credentials: 'include', method: 'GET' }))
    expect(guardar).not.toHaveBeenCalled(); expect(historia).not.toHaveBeenCalled()
  })

  it('envía fechas exactas, boolean false y filtros combinados; grupos y referencias usan sus rutas mínimas', async () => {
    const fetch = responder({})
    await consultarAnalisis('flota', { ...periodo, activo: false, orden: 'cantidadAsc', vehiculoId: 4, tipoVehiculoId: 2, transportistaId: 3, pagina: 2 })
    expect(fetch.mock.calls[0][0]).toBe('/api/reportes/flota?desde=2026-10-01&hasta=2026-10-31&activo=false&orden=cantidadAsc&vehiculoId=4&tipoVehiculoId=2&transportistaId=3&pagina=2')
    await consultarGrupos('facturacion/cobros', 'cliente', { ...periodo, numero: '0001-02', pagina: 2 })
    expect(fetch.mock.calls[1][0]).toContain('/api/reportes/facturacion/cobros/grupos?desde=2026-10-01&hasta=2026-10-31&numero=0001-02&pagina=2&dimension=cliente')
    for (const clave of Object.keys(RUTAS_REFERENCIAS) as (keyof typeof RUTAS_REFERENCIAS)[]) {
      await consultarReferencias(clave, 'Pérez & Cía', 2)
      expect(fetch.mock.lastCall?.[0]).toBe(`/api/reportes/${RUTAS_REFERENCIAS[clave]}?busqueda=P%C3%A9rez+%26+C%C3%ADa&pagina=2`)
    }
    await consultarDashboard(periodo)
    expect(fetch.mock.lastCall?.[0]).toBe('/api/reportes/dashboard?desde=2026-10-01&hasta=2026-10-31')
  })

  it('incluye las diez perspectivas y vencimientos no recibe período ficticio', async () => {
    const fetch = responder({})
    const recursos: RecursoAnalisis[] = ['viajes', 'facturacion', 'facturacion/cobros', 'liquidaciones', 'liquidaciones/pagos', 'caja', 'flota', 'vencimientos/choferes', 'vencimientos/flota', 'vencimientos/facturas']
    for (const recurso of recursos) {
      if (recurso.startsWith('vencimientos')) {
        await consultarAnalisis(recurso as 'vencimientos/choferes', { pagina: 2 })
        expect(fetch.mock.lastCall?.[0]).toBe(`/api/reportes/${recurso}?pagina=2`)
      } else {
        await consultarAnalisis(recurso as 'viajes', periodo)
        expect(fetch.mock.lastCall?.[0]).toBe(`/api/reportes/${recurso}?desde=2026-10-01&hasta=2026-10-31`)
      }
    }
    expect(queryAnalisis({ busqueda: '', activo: false, pagina: 1, inexistente: undefined })).toBe('?activo=false&pagina=1')
  })

  it('mantiene errores de campo y 503 sin convertirlos en ceros', async () => {
    responder({ codigo: 'rango_invalido', mensaje: 'Revisá el rango.', campos: { hasta: ['Revisá el rango.'] } }, 400)
    await expect(consultarDashboard(periodo)).rejects.toMatchObject({ estado: 400, detalle: { campos: { hasta: ['Revisá el rango.'] } } })
    responder({ codigo: 'analisis_no_disponible', mensaje: 'Análisis no disponible.' }, 503)
    await expect(consultarDashboard(periodo)).rejects.toBeInstanceOf(ErrorHttp)
  })

  it('conserva la señal de sesión expirada del cliente compartido', async () => {
    const expirada = vi.fn(); registrarManejadorDeSesionExpirada(expirada)
    responder({ codigo: 'sesion_expirada', mensaje: 'Ingresá nuevamente.' }, 401)
    await expect(consultarDashboard(periodo)).rejects.toMatchObject({ estado: 401 })
    expect(expirada).toHaveBeenCalledOnce()
  })
})

describe('centavos exactos', () => {
  it.each(['0.00', '0.01', '-0.01', '1.10', '10000000000000000.00'])('admite canónico %s', valor => expect(typeof centavosAnalisis(valor)).toBe('bigint'))
  it.each(['1', '1.1', '01.00', '1,10', 'NaN', '-0.00', '1.001'])('rechaza no canónico %s', valor => expect(() => centavosAnalisis(valor)).toThrow())
})
