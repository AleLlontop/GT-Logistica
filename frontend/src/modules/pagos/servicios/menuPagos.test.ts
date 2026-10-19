import { expect, it } from 'vitest'
import { opcionesDisponiblesPagos } from './menuPagos'
import { agruparEnSecciones } from '../../../compartido/seccionesDeMenu'
it('publica las cuatro pantallas, agrupa Pagos y preserva códigos desconocidos', () => {
  const opciones = ['generar-orden-proveedor', 'bancos-pagos', 'consultar-ordenes-proveedor', 'cheques-por-vencer', 'futuro-desconocido']
    .map(codigo => ({ codigo, etiqueta: codigo, ruta: '/' + codigo }))
  const disponibles = opcionesDisponiblesPagos(opciones)
  expect(disponibles.map(x => x.codigo)).toEqual(['generar-orden-proveedor', 'bancos-pagos', 'consultar-ordenes-proveedor', 'cheques-por-vencer', 'futuro-desconocido'])
  const secciones = agruparEnSecciones(disponibles)
  expect(secciones.find(x => x.nombre === 'Gestión de pagos')?.opciones).toHaveLength(4)
  expect(secciones.at(-1)?.opciones[0].codigo).toBe('futuro-desconocido')
})
