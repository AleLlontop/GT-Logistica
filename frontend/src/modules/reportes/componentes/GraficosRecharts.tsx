import { Bar, BarChart, CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis, type TooltipContentProps } from 'recharts'
import { formatearPesos } from '../../../compartido/moneda'
import { coordenadasGrafico, type DatoGrafico, type SerieGrafico } from '../servicios/coordenadasGrafico'

export interface PropsGraficoRecharts { titulo: string; datos: DatoGrafico[]; series: SerieGrafico[]; monetario: boolean; tipo: 'lineas' | 'barras' }
const COLORES = ['var(--color-chart-primary)', 'var(--color-chart-secondary)', 'var(--color-chart-accent)']
const TRAZOS = [undefined, '6 3', '2 3']
const eje = new Intl.NumberFormat('es-AR', { maximumFractionDigits: 2 })

export function ValorAlApuntar({ contenido, series, monetario }: { contenido: TooltipContentProps; series: SerieGrafico[]; monetario: boolean }) {
  const original = contenido.payload?.[0]?.payload?.original as DatoGrafico | undefined
  if (!contenido.active || !original) return null
  return <div className="rounded-card border border-line bg-surface p-3 text-sm text-ink shadow-card">
    <p className="m-0 font-semibold">{original.etiqueta}</p>
    <dl className="m-0">{series.map(serie => <div key={serie.clave}><dt className="inline">{serie.nombre}: </dt><dd className="m-0 inline font-semibold">{monetario ? formatearPesos(original.valores[serie.clave] as string) : (original.valores[serie.clave] as number).toLocaleString('es-AR')}</dd></div>)}</dl>
  </div>
}

export default function GraficosRecharts({ titulo, datos, series, monetario, tipo }: PropsGraficoRecharts) {
  const modelo = coordenadasGrafico(datos, series, monetario)
  const comunes = { data: modelo.coordenadas, accessibilityLayer: true, 'aria-label': `${titulo}. ${modelo.unidad}. Valores exactos en la tabla.`, margin: { top: 15, right: 15, bottom: 25, left: 10 } }
  const ejes = <>
    <CartesianGrid stroke="var(--color-line)" strokeDasharray="3 3" />
    <XAxis dataKey="etiqueta" axisLine={false} tickLine={false} tick={{ fill: 'var(--color-ink-soft)', fontSize: 11 }} />
    <YAxis axisLine={false} tickLine={false} width={78} tickFormatter={valor => eje.format(valor)} tick={{ fill: 'var(--color-ink-soft)', fontSize: 11 }} />
    <Tooltip cursor={{ fill: 'var(--color-surface-hover)', stroke: 'var(--color-line-strong)' }} content={contenido => <ValorAlApuntar contenido={contenido} series={series} monetario={monetario} />} />
  </>
  return <div>
    <p className="m-0 text-xs text-ink-soft">Escala visual: {modelo.unidad}{monetario ? ' (aproximada)' : ''}.</p>
    <ResponsiveContainer width="100%" height={260} minWidth={1} initialDimension={{ width: 600, height: 260 }}>
      {tipo === 'lineas' ? <LineChart {...comunes}>{ejes}{series.map((serie, indice) => <Line key={serie.clave} dataKey={serie.clave} name={serie.nombre} stroke={COLORES[indice % COLORES.length]} strokeDasharray={TRAZOS[indice % TRAZOS.length]} strokeWidth={2} isAnimationActive={false} dot={{ r: 3, fill: COLORES[indice % COLORES.length], stroke: 'var(--color-surface)' }} activeDot={{ r: 5, fill: COLORES[indice % COLORES.length], stroke: 'var(--color-surface)', strokeWidth: 2 }} />)}</LineChart>
        : <BarChart {...comunes}>{ejes}{series.map((serie, indice) => <Bar key={serie.clave} dataKey={serie.clave} name={serie.nombre} fill={COLORES[indice % COLORES.length]} radius={[4, 4, 0, 0]} isAnimationActive={false} />)}</BarChart>}
    </ResponsiveContainer>
  </div>
}
