import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { rutaDelArchivo } from './api'
import { cargarDocumento, corregirDocumento, eliminarDocumento } from '../documentacion/servicioDocumentacion'
import { crearTipo, darDeBajaTipo, listarTipos, modificarTipo } from '../documentacion/servicioTipos'
import {
  crearChofer,
  darDeBajaChofer,
  listarChoferes,
  listarVencimientos,
  modificarChofer,
  obtenerChofer,
  reactivarChofer,
  FILTROS_CHOFERES_INICIALES,
} from './servicioChoferes'
import {
  crearTransportista,
  darDeBajaTransportista,
  listarTransportistas,
  modificarTransportista,
  obtenerTransportista,
} from '../transportistas/servicioTransportistas'

/**
 * Las direcciones que el módulo le pide de verdad al backend.
 *
 * Existe por un error concreto: los servicios escribían `/api/transportistas` y `peticion` ya
 * antepone `/api`, así que salía `/api/api/transportistas` y **ninguna pantalla del módulo
 * funcionaba**. Los tests de pantalla no lo vieron porque mockean los servicios, y los de backend
 * tampoco porque nunca pasan por el cliente HTTP.
 *
 * Este test cierra ese hueco: llama a los servicios reales con `fetch` interceptado y mira la URL.
 */
describe('rutas del módulo', () => {
  let fetchFalso: ReturnType<typeof vi.fn>

  beforeEach(() => {
    fetchFalso = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({}), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    vi.stubGlobal('fetch', fetchFalso)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function urlPedida() {
    return fetchFalso.mock.calls[0][0] as string
  }

  const peticionDeChofer = {
    nombre: 'Ramona',
    apellido: 'Gómez',
    dni: '31111222',
    cuil: '27311112223',
    fechaNacimiento: '1990-05-17',
    telefono: '11-5555-5555',
    email: 'ramona@gt.com.ar',
    transportistaId: 1,
  }

  const peticionDeTransportista = {
    nombre: 'G&T Logística S.A.',
    cuit: '30710000006',
    tipo: 'juridica' as const,
    telefono: '11-5555-5555',
    email: 'info@gt.com.ar',
  }

  const peticionDeDocumento = {
    documentacionTipoId: 1,
    numero: 'LIC-123',
    fechaEmision: '2026-01-01',
    fechaVencimiento: '2027-01-01',
  }

  it.each([
    ['listarTransportistas', () => listarTransportistas(), '/api/transportistas'],
    ['obtenerTransportista', () => obtenerTransportista(3), '/api/transportistas/3'],
    ['crearTransportista', () => crearTransportista(peticionDeTransportista), '/api/transportistas'],
    [
      'modificarTransportista',
      () => modificarTransportista(3, peticionDeTransportista),
      '/api/transportistas/3',
    ],
    ['darDeBajaTransportista', () => darDeBajaTransportista(3), '/api/transportistas/3'],
    ['crearChofer', () => crearChofer(peticionDeChofer), '/api/choferes'],
    ['obtenerChofer', () => obtenerChofer(7), '/api/choferes/7'],
    ['modificarChofer', () => modificarChofer(7, peticionDeChofer), '/api/choferes/7'],
    ['darDeBajaChofer', () => darDeBajaChofer(7), '/api/choferes/7'],
    ['reactivarChofer', () => reactivarChofer(7), '/api/choferes/7/reactivacion'],
    ['listarVencimientos', () => listarVencimientos(), '/api/vencimientos'],
    ['listarTipos', () => listarTipos(), '/api/tipos-documentacion'],
    ['crearTipo', () => crearTipo({ nombre: 'ART', diasAvisoVencimiento: 0 }), '/api/tipos-documentacion'],
    [
      'modificarTipo',
      () => modificarTipo(2, { nombre: 'ART', diasAvisoVencimiento: 0 }),
      '/api/tipos-documentacion/2',
    ],
    ['darDeBajaTipo', () => darDeBajaTipo(2), '/api/tipos-documentacion/2'],
    [
      'cargarDocumento',
      () => cargarDocumento(7, peticionDeDocumento, null),
      '/api/choferes/7/documentacion',
    ],
    [
      'corregirDocumento',
      () => corregirDocumento(5, peticionDeDocumento, null),
      '/api/documentacion/5',
    ],
    ['eliminarDocumento', () => eliminarDocumento(5), '/api/documentacion/5'],
  ])('%s pega en %s', async (_nombre, llamar, esperada) => {
    await llamar()

    const url = urlPedida()

    expect(url.split('?')[0]).toBe(esperada)
    // Lo que rompía: el prefijo repetido.
    expect(url).not.toContain('/api/api/')
  })

  it('listarChoferes manda los filtros y la página como parámetros', async () => {
    await listarChoferes({ ...FILTROS_CHOFERES_INICIALES, apellido: 'Gómez' }, 2)

    const url = urlPedida()

    expect(url).toContain('/api/choferes?')
    expect(url).toContain('apellido=G%C3%B3mez')
    expect(url).toContain('estado=activo')
    expect(url).toContain('pagina=2')
  })

  /**
   * La única que lleva `/api` escrito: va a un `href` del navegador y no pasa por `peticion`, así
   * que nadie le antepone el prefijo.
   */
  it('rutaDelArchivo apunta al endpoint autorizado y no al volumen', () => {
    expect(rutaDelArchivo(5)).toBe('/api/documentacion/5/archivo')
  })
})
