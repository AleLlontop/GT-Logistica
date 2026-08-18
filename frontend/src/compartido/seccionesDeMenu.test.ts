import { describe, expect, it } from 'vitest'
import { agruparEnSecciones } from './seccionesDeMenu'
import type { OpcionMenu } from './tipos'

function opcion(codigo: string): OpcionMenu {
  return { codigo, etiqueta: codigo, ruta: `/${codigo}` }
}

/**
 * El mapa de secciones es lo único del rediseño que decide algo sobre el menú, así que lleva test:
 * lo que hay que proteger es que **decidir dónde se dibuja una opción no se convierta nunca en
 * decidir si existe** (research §6).
 *
 * El tercer caso —el código desconocido— es el que el quickstart declara que no se puede verificar a
 * mano, porque haría falta un séptimo módulo para probarlo.
 */
describe('agruparEnSecciones', () => {
  it('agrupa cada opción en su sección, en el orden del sistema', () => {
    const secciones = agruparEnSecciones([
      opcion('usuarios'),
      opcion('viajes'),
      opcion('tipos-vehiculo'),
      opcion('choferes'),
      opcion('totales'),
    ])

    expect(secciones.map((s) => s.nombre)).toEqual([
      'Operación',
      'Padrones',
      'Seguimiento',
      'Configuración',
      'Administración',
    ])
  })

  it('no dibuja una sección que quedó sin opciones autorizadas', () => {
    // Un usuario de Tráfico sin permisos de facturación ni de administración.
    const secciones = agruparEnSecciones([opcion('viajes'), opcion('choferes'), opcion('flota')])

    expect(secciones.map((s) => s.nombre)).toEqual(['Operación', 'Padrones'])
    expect(secciones.every((s) => s.opciones.length > 0)).toBe(true)
  })

  it('dibuja igual una opción cuyo código no conoce, en la última sección', () => {
    // El día que exista un Módulo 8, su entrada tiene que aparecer sin tocar el frontend.
    const secciones = agruparEnSecciones([opcion('viajes'), opcion('liquidaciones')])

    const ultima = secciones[secciones.length - 1]
    expect(ultima.nombre).toBe('Administración')
    expect(ultima.opciones.map((o) => o.codigo)).toContain('liquidaciones')
  })

  it('no inventa ninguna opción: sólo reparte las que llegaron', () => {
    const recibidas = [opcion('viajes'), opcion('facturas')]

    const dibujadas = agruparEnSecciones(recibidas).flatMap((s) => s.opciones)

    expect(dibujadas).toHaveLength(recibidas.length)
    expect(dibujadas.map((o) => o.codigo).sort()).toEqual(['facturas', 'viajes'])
  })

  it('no devuelve ninguna sección cuando el menú viene vacío', () => {
    expect(agruparEnSecciones([])).toEqual([])
  })
})
