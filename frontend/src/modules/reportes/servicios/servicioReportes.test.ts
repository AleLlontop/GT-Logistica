import { afterEach, describe, expect, it, vi } from 'vitest'
import { generarReporte } from './servicioReportes'

describe('rechazo de precisión XLSX', () => {
  afterEach(() => vi.unstubAllGlobals())
  it('conserva el mensaje del servidor como resultado sin entregar archivo', async () => {
    const mensaje = 'Excel perdería centavos; PDF disponible.'
    const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ codigo: 'precision_monetaria_xlsx', mensaje }), {
      status: 409, headers: { 'Content-Type': 'application/json' },
    }))
    vi.stubGlobal('fetch', fetch)
    expect(await generarReporte('viajes', 'excel', { clienteId: 3 })).toEqual({ estado: 'precision_monetaria_xlsx', mensaje })
    expect(fetch).toHaveBeenCalledOnce()
  })
})
