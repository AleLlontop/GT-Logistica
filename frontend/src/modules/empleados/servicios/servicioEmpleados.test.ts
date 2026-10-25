import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  crearEmpleado,
  darDeBajaEmpleado,
  historialDeAdelantos,
  historialDeCajas,
  listarEmpleados,
  modificarEmpleado,
  obtenerEmpleado,
  reactivarEmpleado,
  type DatosEmpleado,
} from './servicioEmpleados'

const EMPLEADO = {
  id: 17,
  nombre: 'Ana',
  apellido: 'Pérez',
  dni: '30123456',
  tipo: 'empleado' as const,
  telefono: '221-555-0101',
  email: 'ana.perez@gt.com.ar',
  fechaNacimiento: '1990-05-17',
  activa: true,
}

const DATOS: DatosEmpleado = {
  nombre: EMPLEADO.nombre,
  apellido: EMPLEADO.apellido,
  dni: EMPLEADO.dni,
  telefono: EMPLEADO.telefono,
  email: EMPLEADO.email,
  fechaNacimiento: EMPLEADO.fechaNacimiento,
}

const fetchMock = vi.fn<typeof fetch>()

function respuestaJson(cuerpo: unknown, estado = 200): Response {
  return new Response(JSON.stringify(cuerpo), {
    status: estado,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('servicioEmpleados', () => {
  beforeEach(() => {
    fetchMock.mockReset()
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lista empleados y codifica el filtro de búsqueda', async () => {
    fetchMock.mockResolvedValue(respuestaJson([EMPLEADO]))

    await expect(listarEmpleados({ texto: 'Pérez 30' })).resolves.toEqual([EMPLEADO])

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/empleados?texto=P%C3%A9rez+30',
      expect.objectContaining({ method: 'GET', credentials: 'include' }),
    )
  })

  it('fuerza tipo empleado al crear y modificar', async () => {
    fetchMock.mockResolvedValueOnce(respuestaJson(EMPLEADO, 201))
    await crearEmpleado(DATOS)

    expect(JSON.parse(fetchMock.mock.calls[0][1]?.body as string)).toEqual({ ...DATOS, tipo: 'empleado' })
    expect(fetchMock.mock.calls[0][1]?.method).toBe('POST')

    fetchMock.mockResolvedValueOnce(respuestaJson(EMPLEADO))
    await modificarEmpleado(EMPLEADO.id, DATOS)

    expect(fetchMock.mock.calls[1][0]).toBe('/api/empleados/17')
    expect(fetchMock.mock.calls[1][1]?.method).toBe('PUT')
    expect(JSON.parse(fetchMock.mock.calls[1][1]?.body as string)).toEqual({ ...DATOS, tipo: 'empleado' })
  })

  it('usa las rutas de detalle y los dos historiales', async () => {
    fetchMock
      .mockResolvedValueOnce(respuestaJson(EMPLEADO))
      .mockResolvedValueOnce(respuestaJson([]))
      .mockResolvedValueOnce(respuestaJson([]))

    await obtenerEmpleado(17)
    await historialDeAdelantos(17)
    await historialDeCajas(17)

    expect(fetchMock.mock.calls.map((llamada) => llamada[0])).toEqual([
      '/api/empleados/17',
      '/api/empleados/17/adelantos',
      '/api/empleados/17/cajas',
    ])
  })

  it('reactiva con un POST al recurso propio', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))

    await reactivarEmpleado(17)

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/empleados/17/reactivacion',
      expect.objectContaining({ method: 'POST' }),
    )
  })

  it('no convierte un conflicto de baja por caja abierta en una respuesta exitosa', async () => {
    fetchMock.mockResolvedValue(
      respuestaJson(
        {
          codigo: 'empleado_con_caja_abierta',
          mensaje: 'Cerrá la caja antes de dar de baja al empleado.',
        },
        409,
      ),
    )

    await expect(darDeBajaEmpleado(17)).rejects.toMatchObject({
      estado: 409,
      detalle: { codigo: 'empleado_con_caja_abierta' },
    })
  })
})
