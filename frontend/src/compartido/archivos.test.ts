import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { entregarArchivo, obtenerArchivo, type ArchivoRecibido } from './archivos'
import { ErrorHttp } from './clienteHttp'

describe('obtenerArchivo', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('devuelve el blob y el nombre que puso el backend', async () => {
    respondeCon(
      new Response('%PDF', {
        status: 200,
        headers: {
          'Content-Type': 'application/pdf',
          'Content-Disposition': 'inline; filename="viajes-2026-09-20-1432.pdf"',
        },
      }),
    )

    const archivo = await obtenerArchivo('/viajes/reporte?formato=pdf')

    expect(archivo.nombre).toBe('viajes-2026-09-20-1432.pdf')
    expect(await archivo.blob.text()).toBe('%PDF')
  })

  it('lee el nombre aunque venga sin comillas', async () => {
    respondeCon(
      new Response('x', {
        status: 200,
        headers: { 'Content-Disposition': 'attachment; filename=viajes-2026-09-20-1432.xlsx' },
      }),
    )

    expect((await obtenerArchivo('/viajes/reporte')).nombre).toBe('viajes-2026-09-20-1432.xlsx')
  })

  it('sin la cabecera, el nombre queda en null en vez de componerse acá', async () => {
    respondeCon(new Response('x', { status: 200 }))

    expect((await obtenerArchivo('/viajes/reporte')).nombre).toBeNull()
  })

  it('traduce el rechazo del servidor al ErrorHttp del sistema, con su código y su mensaje', async () => {
    respondeCon(
      new Response(
        JSON.stringify({
          codigo: 'tope_de_filas_superado',
          mensaje: 'El filtro dejó 7.412 filas y el tope de un reporte es 5.000.',
        }),
        { status: 409, headers: { 'Content-Type': 'application/json' } },
      ),
    )

    const error = await obtenerArchivo('/viajes/reporte').catch((fallo: unknown) => fallo)

    expect(error).toBeInstanceOf(ErrorHttp)
    expect((error as ErrorHttp).estado).toBe(409)
    expect((error as ErrorHttp).detalle.codigo).toBe('tope_de_filas_superado')
  })

  it('un rechazo sin cuerpo legible cae en el mensaje genérico', async () => {
    respondeCon(new Response('<html>502</html>', { status: 502 }))

    const error = (await obtenerArchivo('/viajes/reporte').catch(
      (fallo: unknown) => fallo,
    )) as ErrorHttp

    expect(error.detalle.codigo).toBe('error_inesperado')
  })

  it('sin conexión devuelve el error del sistema, no la excepción de fetch', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.reject(new TypeError('Failed to fetch'))),
    )

    const error = (await obtenerArchivo('/viajes/reporte').catch(
      (fallo: unknown) => fallo,
    )) as ErrorHttp

    expect(error.detalle.codigo).toBe('sin_conexion')
  })

  it('manda las credenciales y antepone el prefijo /api una sola vez', async () => {
    const fetchFalso = respondeCon(new Response('x', { status: 200 }))

    await obtenerArchivo('/viajes/reporte?formato=pdf')

    expect(fetchFalso).toHaveBeenCalledWith(
      '/api/viajes/reporte?formato=pdf',
      expect.objectContaining({ credentials: 'include' }),
    )
  })

  function respondeCon(respuesta: Response) {
    const fetchFalso = vi.fn(() => Promise.resolve(respuesta))
    vi.stubGlobal('fetch', fetchFalso)

    return fetchFalso
  }
})

describe('entregarArchivo', () => {
  let abrir: ReturnType<typeof vi.fn>

  beforeEach(() => {
    vi.stubGlobal('URL', {
      ...URL,
      createObjectURL: vi.fn(() => 'blob:falso'),
      revokeObjectURL: vi.fn(),
    })

    abrir = vi.fn(() => ({}) as Window)
    vi.stubGlobal('open', abrir)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('el PDF se abre en una pestaña nueva', () => {
    expect(entregarArchivo(archivo(), 'abrir')).toBe('abierto')
    expect(abrir).toHaveBeenCalledWith('blob:falso', '_blank', 'noopener')
  })

  /**
   * **El caso que hace falta que exista** (research §8): `window.open` devuelve `null` cuando el
   * navegador bloquea las ventanas emergentes, y sin la caída a descarga el reporte desaparecería sin
   * ninguna explicación.
   */
  it('con las emergentes bloqueadas, el PDF cae a la descarga', () => {
    abrir.mockReturnValue(null)

    const clic = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})

    expect(entregarArchivo(archivo(), 'abrir')).toBe('descargado')
    expect(clic).toHaveBeenCalled()
  })

  it('el Excel se descarga siempre, sin intentar abrir nada', () => {
    const clic = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})

    expect(entregarArchivo(archivo('reporte.xlsx'), 'descargar')).toBe('descargado')
    expect(abrir).not.toHaveBeenCalled()
    expect(clic).toHaveBeenCalled()
  })

  it('la descarga usa el nombre que puso el backend', () => {
    let descargado = ''

    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      descargado = this.download
    })

    entregarArchivo(archivo('viajes-2026-09-20-1432.xlsx'), 'descargar')

    expect(descargado).toBe('viajes-2026-09-20-1432.xlsx')
  })

  function archivo(nombre: string | null = 'viajes-2026-09-20-1432.pdf'): ArchivoRecibido {
    return { blob: new Blob(['x']), nombre }
  }
})
