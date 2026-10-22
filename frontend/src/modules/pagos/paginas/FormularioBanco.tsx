import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { Boton } from '../../../compartido/ui/Boton'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeBoton, clasesDeControl } from '../../../compartido/ui/clases'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { BarraDeAcciones, LEYENDA_DE_OBLIGATORIOS } from '../../../compartido/ui/BarraDeAcciones'
import { useResultadoPago } from '../componentes/resultadoPago'
import { guardarBanco as enviarBanco, mensajePago, obtenerBanco, SIN_PERMISO_PAGOS } from '../servicios/pagos'
import { errorNombreBanco, trimExterior } from '../servicios/normalizacionPagos'

export function FormularioBanco({ puedeGestionar = false }: { puedeGestionar?: boolean }) {
  const parametro = useParams().id
  const id = parametro === undefined ? undefined : Number(parametro)
  const navegar = useNavigate()
  const { banco, guardarBanco } = useResultadoPago()
  const [nombre, setNombre] = useState('')
  const [version, setVersion] = useState(0)
  const [cargando, setCargando] = useState(id !== undefined)
  const [guardando, setGuardando] = useState(false)
  const [error, setError] = useState('')
  const [errorCampo, setErrorCampo] = useState('')
  const [falloCarga, setFalloCarga] = useState(false)
  const [versionObsoleta, setVersionObsoleta] = useState(false)
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    if (id === undefined || !puedeGestionar) return
    let vigente = true
    setCargando(true); setError(''); setFalloCarga(false)
    const carga = revision === 0 && banco?.id === id ? Promise.resolve(banco) : obtenerBanco(id)
    carga.then(b => { if (vigente) { setNombre(b.nombre); setVersion(b.version); setVersionObsoleta(false) } })
      .catch(e => { if (vigente) { setError(mensajePago(e)); setFalloCarga(true) } }).finally(() => { if (vigente) setCargando(false) })
    return () => { vigente = false }
  }, [id, banco, puedeGestionar, revision])
  async function enviar(evento: FormEvent) {
    evento.preventDefault()
    if (!puedeGestionar || cargando || guardando) return
    const invalido = errorNombreBanco(nombre)
    setErrorCampo(invalido); setError('')
    if (invalido) return
    setGuardando(true)
    try {
      const resultado = await enviarBanco(id, trimExterior(nombre), version)
      guardarBanco(resultado)
      navegar(`/pagos/bancos/${resultado.id}`)
    } catch (e) {
      if (e instanceof ErrorHttp && e.detalle.campo === 'nombre') setErrorCampo(e.message)
      else { setError(mensajePago(e)); setVersionObsoleta(e instanceof ErrorHttp && e.estado === 409) }
    } finally { setGuardando(false) }
  }
  const cancelar = id === undefined ? '/pagos/bancos' : `/pagos/bancos/${id}`
  return <>
    <EncabezadoDePantalla titulo={id === undefined ? 'Nuevo banco' : 'Editar banco'} volverA={{ ruta: cancelar, etiqueta: 'Volver' }} />
    {!puedeGestionar ? <p role="alert">{SIN_PERMISO_PAGOS}</p> : cargando ? <p role="status">Cargando banco…</p> :
      <form noValidate onSubmit={evento => void enviar(evento)} className="rounded-card border border-line bg-surface shadow-card">
        {error && <p role="alert" className="p-5">{error}</p>}
        {versionObsoleta && <Boton variante="secundario" onClick={() => setRevision(r => r + 1)}>Volver a cargar banco</Boton>}
        <div className="p-[30px]"><Campo id="nombre-banco" etiqueta="Nombre" obligatorio error={errorCampo} ancho="largo">
          <input id="nombre-banco" required className={clasesDeControl(!!errorCampo)} value={nombre} disabled={guardando}
            onChange={e => { setNombre(e.target.value); setErrorCampo('') }} onBlur={() => setErrorCampo(errorNombreBanco(nombre))}
            aria-invalid={!!errorCampo} aria-describedby={errorCampo ? 'nombre-banco-error' : undefined} />
        </Campo></div>
        <BarraDeAcciones anclaje="viewport" leyenda={LEYENDA_DE_OBLIGATORIOS}>
          <Link className={clasesDeBoton('secundario')} to={cancelar}>Cancelar</Link>
          <Boton variante="primario" type="submit" disabled={guardando || falloCarga || versionObsoleta}>Guardar</Boton>
        </BarraDeAcciones>
      </form>}
  </>
}
