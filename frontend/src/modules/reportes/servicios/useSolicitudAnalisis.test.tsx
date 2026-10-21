import { act, renderHook, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { useFiltrosAnalisis, useSolicitudAnalisis } from './useSolicitudAnalisis'

function diferida<T>() {
  let resolver!: (valor: T) => void, rechazar!: (error: unknown) => void
  const promesa = new Promise<T>((resolve, reject) => { resolver = resolve; rechazar = reject })
  return { promesa, resolver, rechazar }
}

describe('solicitudes de análisis por identidad', () => {
  it.each(['éxito', 'error'])('B responde antes que A; %s viejo no reemplaza B', async finalViejo => {
    const a = diferida<string>(), b = diferida<string>()
    const cargar = vi.fn().mockReturnValueOnce(a.promesa).mockReturnValueOnce(b.promesa)
    const { result, rerender } = renderHook(({ clave }) => useSolicitudAnalisis(clave, 'gerencia', cargar), { initialProps: { clave: 'octubre' } })
    await waitFor(() => expect(cargar).toHaveBeenCalledTimes(1))
    rerender({ clave: 'noviembre' })
    expect(result.current.datos).toBeNull(); expect(result.current.estado).toBe('cargando')
    await waitFor(() => expect(cargar).toHaveBeenCalledTimes(2))
    await act(async () => b.resolver('datos noviembre'))
    expect(result.current.datos).toBe('datos noviembre')
    await act(async () => { if (finalViejo === 'éxito') a.resolver('datos octubre'); else a.rechazar(new Error('fallo octubre')) })
    expect(result.current.estado).toBe('listo'); expect(result.current.datos).toBe('datos noviembre')
  })

  it('cambiar página y sección descarta la respuesta previa y no la rotula como actual', async () => {
    const a = diferida<string>(), b = diferida<string>(), c = diferida<string>()
    const cargar = vi.fn().mockReturnValueOnce(a.promesa).mockReturnValueOnce(b.promesa).mockReturnValueOnce(c.promesa)
    const { result, rerender } = renderHook(({ clave }) => useSolicitudAnalisis(clave, 'gerencia', cargar), { initialProps: { clave: 'viajes:1' } })
    await act(async () => a.resolver('página1'))
    expect(result.current.datos).toBe('página1')
    rerender({ clave: 'viajes:2' }); expect(result.current.datos).toBeNull()
    await waitFor(() => expect(cargar).toHaveBeenCalledTimes(2))
    rerender({ clave: 'cobros:1' }); expect(result.current.datos).toBeNull()
    await waitFor(() => expect(cargar).toHaveBeenCalledTimes(3))
    await act(async () => c.resolver('cobros'))
    await act(async () => b.resolver('página2 vieja'))
    expect(result.current.datos).toBe('cobros')
  })

  it('sesión nula oculta datos y no consulta; respuesta del usuario anterior no reaparece', async () => {
    const anterior = diferida<string>(), nueva = diferida<string>()
    const cargar = vi.fn().mockReturnValueOnce(anterior.promesa).mockReturnValueOnce(nueva.promesa)
    const { result, rerender } = renderHook(({ sesion }: { sesion: string | null }) => useSolicitudAnalisis('viajes', sesion, cargar), { initialProps: { sesion: 'gerencia' as string | null } })
    await waitFor(() => expect(cargar).toHaveBeenCalledTimes(1))
    rerender({ sesion: null }); expect(result.current.estado).toBe('inactivo')
    await act(async () => anterior.resolver('datos privados anteriores'))
    expect(result.current.datos).toBeNull(); expect(cargar).toHaveBeenCalledTimes(1)
    rerender({ sesion: 'otra-persona' }); expect(result.current.datos).toBeNull()
    await waitFor(() => expect(cargar).toHaveBeenCalledTimes(2))
    await act(async () => nueva.resolver('datos autorizados nuevos'))
    expect(result.current.datos).toBe('datos autorizados nuevos')
  })

  it('401 no conserva datos anteriores y el reintento tiene su propia secuencia', async () => {
    const fallo = new ErrorHttp(401, { codigo: 'sin_permiso', mensaje: 'Sesión finalizada.' })
    const cargar = vi.fn().mockResolvedValueOnce('antes').mockRejectedValueOnce(fallo).mockResolvedValueOnce('después')
    const { result, rerender } = renderHook(({ clave }) => useSolicitudAnalisis(clave, 'gerencia', cargar), { initialProps: { clave: 'A' } })
    await waitFor(() => expect(result.current.datos).toBe('antes'))
    rerender({ clave: 'B' }); expect(result.current.datos).toBeNull()
    await waitFor(() => expect(result.current.estado).toBe('error'))
    expect(result.current.error).toBe(fallo); expect(result.current.datos).toBeNull()
    act(() => result.current.reintentar())
    expect(result.current.datos).toBeNull()
    await waitFor(() => expect(result.current.datos).toBe('después'))
  })

  it('deshabilitar una sección y desmontar cancela la publicación pendiente', async () => {
    const a = diferida<string>(); const cargar = vi.fn().mockReturnValue(a.promesa)
    const { result, rerender, unmount } = renderHook(({ clave }: { clave: string | null }) => useSolicitudAnalisis(clave, 'gerencia', cargar), { initialProps: { clave: 'A' as string | null } })
    await waitFor(() => expect(cargar).toHaveBeenCalledTimes(1))
    rerender({ clave: null }); expect(result.current.estado).toBe('inactivo')
    unmount(); await act(async () => a.resolver('no publicar'))
    expect(cargar).toHaveBeenCalledTimes(1)
  })
})

describe('borrador separado de filtro aplicado', () => {
  it('editar no consulta otro universo hasta aplicar y restablecer no retiene referencias mutables', () => {
    const inicial = { desde: '2026-10-01', hasta: '2026-10-31', clienteId: 3 }
    const { result } = renderHook(() => useFiltrosAnalisis(inicial))
    act(() => result.current.setBorrador({ ...inicial, clienteId: 4 }))
    expect(result.current.aplicados.clienteId).toBe(3)
    act(() => result.current.aplicar())
    expect(result.current.aplicados.clienteId).toBe(4)
    act(() => result.current.restablecer(inicial))
    inicial.clienteId = 99
    expect(result.current.aplicados.clienteId).toBe(3); expect(result.current.borrador.clienteId).toBe(3)
  })
})
