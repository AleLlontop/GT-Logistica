import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { formatearPesos } from '../../../compartido/moneda'
import { formatearFecha } from '../../../compartido/fechas'
import { formatearCuit } from '../../../compartido/cuit'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { FichaCuerpo, FichaEncabezado, FichaSeccion } from '../../../compartido/ui/Ficha'
import { AsideDeFicha, CifraDestacada } from '../../../compartido/ui/AsideDeFicha'
import { Estado } from '../../../compartido/ui/Estado'
import { useResultadoFactura } from '../componentes/resultadoFactura'
import { obtenerFacturaProveedor } from '../servicios/facturasProveedor'
import type { DetalleFacturaProveedor as Detalle } from '../servicios/tipos'

export function DetalleFacturaProveedor({ puedeConsultar = false, puedeGestionarProveedores = false, puedeConsultarLiquidaciones = false }: {
  puedeConsultar?: boolean; puedeGestionarProveedores?: boolean; puedeConsultarLiquidaciones?: boolean
}) {
  const { id } = useParams(); const numero = Number(id); const { resultado } = useResultadoFactura()
  const enMemoria = resultado?.id === numero ? resultado : null
  const [detalle, setDetalle] = useState<Detalle | null>(enMemoria)
  const [cargando, setCargando] = useState(!enMemoria); const [error, setError] = useState('')
  useEffect(() => {
    if (enMemoria) { setDetalle(enMemoria); setCargando(false); setError(''); return }
    let vigente = true; setDetalle(null); setCargando(true); setError('')
    obtenerFacturaProveedor(numero).then(d => { if (vigente) setDetalle(d) })
      .catch(e => { if (vigente) setError(e instanceof ErrorHttp && e.estado === 403 ? 'No tenés permiso para consultar esta factura. Pedile acceso a un administrador.' : 'No pudimos traer la factura. Volvé a intentar.') })
      .finally(() => { if (vigente) setCargando(false) })
    return () => { vigente = false }
  }, [numero, enMemoria])
  return <>
    <EncabezadoDePantalla titulo="Factura de proveedor" volverA={puedeConsultar ? { ruta: '/proveedores/facturas', etiqueta: 'Volver a facturas de proveedores' } : { ruta: '/', etiqueta: 'Volver al inicio' }} />
    {enMemoria && <p role="status">Factura registrada.</p>}{error && <p role="alert">{error}</p>}
    {cargando ? <p role="status">Cargando factura…</p> : detalle?.id === numero && <>
      <FichaEncabezado identidad={detalle.numero} estado={<Estado forma="pastilla" valor={detalle.estado} texto={detalle.estado === 'pagada' ? 'Pagada' : 'Pendiente'} />} resumen={detalle.proveedorRazonSocial} />
      <FichaCuerpo aside={<AsideDeFicha destacado={<CifraDestacada rotulo="Total" valor={formatearPesos(detalle.importeTotal)} />} />}>
        <FichaSeccion id="datos-factura" titulo="Datos de factura"><dl>
          <dt>Proveedor al registrar</dt><dd>{detalle.proveedorRazonSocial}</dd><dt>CUIT</dt><dd>{formatearCuit(detalle.proveedorCuit)}</dd>
          <dt>Tipo al registrar</dt><dd>{detalle.tipoProveedorAlRegistrar === 'fletero' ? 'Fletero' : 'Proveedor normal'}</dd>
          {puedeGestionarProveedores && <><dt>Proveedor de origen</dt><dd><Link to={`/proveedores/${detalle.proveedorId}`} className="underline">Ver proveedor actual</Link></dd></>}
          <dt>Fecha</dt><dd>{formatearFecha(detalle.fecha)}</dd><dt>Detalle</dt><dd>{detalle.detalle}</dd>
          <dt>Neto</dt><dd>{formatearPesos(detalle.neto)}</dd><dt>IVA (21%)</dt><dd>{formatearPesos(detalle.iva)}</dd>
          <dt>CAE</dt><dd>{detalle.cae}</dd><dt>Vencimiento del CAE</dt><dd>{formatearFecha(detalle.fechaVencimientoCae)}</dd>
          <dt>Liquidación</dt><dd>{detalle.liquidacion ? `${detalle.liquidacion.numero} · ${detalle.liquidacion.periodo}` : 'No corresponde liquidación.'}</dd>
          {detalle.liquidacion && <>
            <dt>Importe de liquidación al registrar</dt><dd>{formatearPesos(detalle.liquidacion.importeAlRegistrar)}</dd>
            <dt>Importe actual de liquidación</dt><dd>{formatearPesos(detalle.liquidacion.importeActual)}</dd>
            <dt>Estado actual de liquidación</dt><dd>{{ pendiente: 'Pendiente', pagada: 'Pagada', anulada: 'Anulada' }[detalle.liquidacion.estadoActual]}</dd>
            {puedeConsultarLiquidaciones && <><dt>Liquidación de origen</dt><dd><Link to={`/liquidaciones/${detalle.liquidacion.id}`} className="underline">Ver liquidación {detalle.liquidacion.numero}</Link></dd></>}
          </>}
        </dl></FichaSeccion>
        <FichaSeccion id="comprobante" titulo="Comprobante">{detalle.archivo ? puedeConsultar ? <a className="m-5 inline-block underline" href={detalle.archivo.url} target="_blank" rel="noreferrer">Abrir PDF: {detalle.archivo.nombre}</a> : <p className="p-5">El PDF requiere permiso de consulta.</p> : <p className="p-5">Sin PDF adjunto.</p>}</FichaSeccion>
      </FichaCuerpo>
    </>}
  </>
}
