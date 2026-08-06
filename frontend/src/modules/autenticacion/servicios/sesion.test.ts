import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cerrarSesion, iniciarSesion, obtenerSesion } from './sesion'

/**
 * La sesión vive en una cookie `HttpOnly` que este código no puede leer (FR-023). Estos tests
 * comprueban lo contrario de lo habitual: que el frontend **no** guarde nada por su cuenta.
 */
describe('servicio de sesión', () => {
  const sesionDeEjemplo = {
    username: 'admin',
    roles: [{ codigo: 'administrador_sistema', nombre: 'Administrador del sistema' }],
    opcionesMenu: [{ codigo: 'usuarios', etiqueta: 'Gestión de usuarios', ruta: '/usuarios' }],
  }

  beforeEach(() => {
    localStorage.clear()
    sessionStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function simularRespuesta(estado: number, cuerpo: unknown) {
    const fetchSimulado = vi.fn().mockResolvedValue({
      ok: estado >= 200 && estado < 300,
      status: estado,
      json: async () => cuerpo,
    })

    vi.stubGlobal('fetch', fetchSimulado)

    return fetchSimulado
  }

  it('envía siempre las credenciales del navegador, para que la cookie viaje', async () => {
    const fetchSimulado = simularRespuesta(200, sesionDeEjemplo)

    await iniciarSesion('admin', 'una-contraseña')

    const [, opciones] = fetchSimulado.mock.calls[0]
    expect(opciones.credentials).toBe('include')
  })

  it('no guarda nada del ingreso en el almacenamiento del navegador', async () => {
    simularRespuesta(200, sesionDeEjemplo)

    await iniciarSesion('admin', 'una-contraseña')

    expect(localStorage.length).toBe(0)
    expect(sessionStorage.length).toBe(0)
  })

  it('no deja rastro de la sesión tras cerrarla', async () => {
    simularRespuesta(204, null)

    await cerrarSesion()

    expect(localStorage.length).toBe(0)
    expect(sessionStorage.length).toBe(0)
  })

  it('no falla al cerrar sesión aunque el servidor no responda', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('sin red')))

    // Dejar a alguien adentro del sistema porque el pedido de salida falló sería el peor
    // resultado posible de apretar "Cerrar sesión".
    await expect(cerrarSesion()).resolves.toBeUndefined()
  })

  it('devuelve null cuando no hay sesión vigente, sin propagar el error', async () => {
    simularRespuesta(401, { codigo: 'sesion_expirada', mensaje: 'Tu sesión expiró.' })

    await expect(obtenerSesion()).resolves.toBeNull()
  })

  it('nunca manda la contraseña en la dirección de la petición', async () => {
    const fetchSimulado = simularRespuesta(200, sesionDeEjemplo)

    await iniciarSesion('admin', 'secreta-1234')

    const [ruta, opciones] = fetchSimulado.mock.calls[0]

    // FR-018: la contraseña viaja en el cuerpo, nunca en la URL.
    expect(ruta).not.toContain('secreta-1234')
    expect(opciones.body).toContain('secreta-1234')
  })
})
