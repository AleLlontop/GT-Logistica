import { clasesDeFormulario } from '../../../compartido/ui/clases'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useState, type FormEvent } from 'react'
import { ErrorHttp, peticion } from '../../../compartido/clienteHttp'

const LARGO_MINIMO = 8

const MENSAJE_CORTA = `La contraseña nueva tiene que tener al menos ${LARGO_MINIMO} caracteres.`
const MENSAJE_NO_COINCIDEN = 'Las dos contraseñas nuevas no coinciden.'
const MENSAJE_ACTUAL_VACIA = 'Escribí tu contraseña actual.'
const MENSAJE_EXITO = 'Tu contraseña se cambió correctamente.'

type ErroresDeCampo = Partial<
  Record<'passwordActual' | 'passwordNueva' | 'repeticion', string>
>

function cambiarPasswordPropia(passwordActual: string, passwordNueva: string): Promise<void> {
  return peticion<void>('/mi-cuenta/contrasena', {
    metodo: 'POST',
    cuerpo: { passwordActual, passwordNueva },
  })
}

/**
 * Cambio de la contraseña propia (User Story 7).
 *
 * Es la única pantalla del módulo abierta a **cualquier** usuario autenticado, sin importar sus
 * roles (FR-029): quien recibe una contraseña temporal puede ser de cualquier área, y sin esta
 * pantalla quedaría afuera del sistema a las 24 horas.
 *
 * Los tres campos arrancan vacíos y enmascarados: la contraseña anterior no se muestra ni se
 * recupera en ninguna circunstancia (FR-013, FR-030).
 */
export function CambiarPassword() {
  const [passwordActual, setPasswordActual] = useState('')
  const [passwordNueva, setPasswordNueva] = useState('')
  const [repeticion, setRepeticion] = useState('')

  const [errores, setErrores] = useState<ErroresDeCampo>({})
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null)
  const [exito, setExito] = useState(false)
  const [enviando, setEnviando] = useState(false)

  function validarEnPantalla(): ErroresDeCampo {
    const encontrados: ErroresDeCampo = {}

    if (passwordActual === '') {
      encontrados.passwordActual = MENSAJE_ACTUAL_VACIA
    }

    if (passwordNueva.length < LARGO_MINIMO) {
      encontrados.passwordNueva = MENSAJE_CORTA
    }

    // Se comprueba en pantalla: no hace falta molestar al servidor para saber que no coinciden.
    if (passwordNueva !== repeticion) {
      encontrados.repeticion = MENSAJE_NO_COINCIDEN
    }

    return encontrados
  }

  async function alEnviar(evento: FormEvent) {
    evento.preventDefault()

    const deFormato = validarEnPantalla()
    setErrores(deFormato)
    setErrorGeneral(null)
    setExito(false)

    if (Object.keys(deFormato).length > 0) {
      return
    }

    setEnviando(true)

    try {
      await cambiarPasswordPropia(passwordActual, passwordNueva)

      // La sesión sigue abierta: el servidor reemite la cookie (FR-032). No hay que volver a
      // ingresar.
      setExito(true)
      setPasswordActual('')
      setPasswordNueva('')
      setRepeticion('')
    } catch (fallo) {
      if (fallo instanceof ErrorHttp && fallo.detalle.codigo === 'password_actual_incorrecta') {
        setErrores({ passwordActual: fallo.detalle.mensaje })
      } else if (fallo instanceof ErrorHttp) {
        setErrorGeneral(fallo.detalle.mensaje)
      } else {
        setErrorGeneral('Ocurrió un problema inesperado. Volvé a intentar en unos minutos.')
      }
    } finally {
      setEnviando(false)
    }
  }

  return (
    <section>
      <EncabezadoDePantalla titulo="Cambiar contraseña" />

      <form onSubmit={alEnviar} noValidate className={clasesDeFormulario}>
        {errorGeneral !== null && (
          <p className="formulario__error" role="alert">
            {errorGeneral}
          </p>
        )}

        {exito && <p role="status">{MENSAJE_EXITO}</p>}

        <div className="campo">
          <label htmlFor="passwordActual">Contraseña actual</label>
          <input
            id="passwordActual"
            name="passwordActual"
            type="password"
            value={passwordActual}
            onChange={(evento) => setPasswordActual(evento.target.value)}
            autoComplete="current-password"
            required
            aria-required="true"
            aria-invalid={errores.passwordActual !== undefined}
            aria-describedby={
              errores.passwordActual !== undefined ? 'error-password-actual' : undefined
            }
          />
          {errores.passwordActual !== undefined && (
            <p className="campo__error" id="error-password-actual" role="alert">
              {errores.passwordActual}
            </p>
          )}
        </div>

        <div className="campo">
          <label htmlFor="passwordNueva">Contraseña nueva</label>
          <input
            id="passwordNueva"
            name="passwordNueva"
            type="password"
            value={passwordNueva}
            onChange={(evento) => setPasswordNueva(evento.target.value)}
            autoComplete="new-password"
            required
            aria-required="true"
            aria-invalid={errores.passwordNueva !== undefined}
            aria-describedby={
              errores.passwordNueva !== undefined ? 'error-password-nueva' : undefined
            }
          />
          {errores.passwordNueva !== undefined && (
            <p className="campo__error" id="error-password-nueva" role="alert">
              {errores.passwordNueva}
            </p>
          )}
        </div>

        <div className="campo">
          <label htmlFor="repeticion">Repetir contraseña nueva</label>
          <input
            id="repeticion"
            name="repeticion"
            type="password"
            value={repeticion}
            onChange={(evento) => setRepeticion(evento.target.value)}
            autoComplete="new-password"
            required
            aria-required="true"
            aria-invalid={errores.repeticion !== undefined}
            aria-describedby={errores.repeticion !== undefined ? 'error-repeticion' : undefined}
          />
          {errores.repeticion !== undefined && (
            <p className="campo__error" id="error-repeticion" role="alert">
              {errores.repeticion}
            </p>
          )}
        </div>

        <button type="submit" disabled={enviando}>
          {enviando ? 'Guardando…' : 'Cambiar contraseña'}
        </button>
      </form>
    </section>
  )
}
