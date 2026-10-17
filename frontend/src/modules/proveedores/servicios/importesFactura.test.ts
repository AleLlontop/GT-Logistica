import { expect,it } from 'vitest'
import { netoCanonico, calcularImportes } from './importesFactura'
it.each([
  ['0,50','0.50','0.11','0.61'],['0,01','0.01','0.00','0.01'],
  ['100.000.000.000.000,01','100000000000000.01','21000000000000.00','121000000000000.01'],
  ['100.000.000.000.000,02','100000000000000.02','21000000000000.00','121000000000000.02'],
])('calcula centavos exactos de %s',(entrada,neto,iva,total)=>{
  expect(netoCanonico(entrada)).toBe(neto)
  expect(calcularImportes(entrada)).toEqual({neto,iva,importeTotal:total})
})
it.each(['','0','-1','1,001','1e3','9999999999999999,99'])('rechaza neto inválido %s',valor=>expect(calcularImportes(valor)).toBeNull())
