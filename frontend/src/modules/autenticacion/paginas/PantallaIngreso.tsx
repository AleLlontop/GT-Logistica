import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { Boton } from '../../../compartido/ui/Boton'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { Aviso } from '../../../compartido/ui/Aviso'
import { iniciarSesion, type Sesion } from '../servicios/sesion'
import { rutaInternaSegura } from '../servicios/rutaSegura'

interface Props {
  onIngreso: (sesion: Sesion) => void
}

const MENSAJE_CAMPOS_VACIOS = 'Completá el nombre de usuario y la contraseña.'

/**
 * Única pantalla pública del sistema (FR-007).
 *
 * Accesibilidad (FR-025): se opera entera con teclado, cada campo tiene su etiqueta visible
 * asociada, el foco arranca en el nombre de usuario y el mensaje de error se anuncia solo a los
 * lectores de pantalla gracias a `role="alert"`.
 */
export function PantallaIngreso({ onIngreso }: Props) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  const campoUsuario = useRef<HTMLInputElement>(null)
  const campoPassword = useRef<HTMLInputElement>(null)
  const navegar = useNavigate()
  const ubicacion = useLocation()

  useEffect(() => {
    campoUsuario.current?.focus()
  }, [])

  /**
   * Enter en el nombre de usuario baja a la contraseña en lugar de enviar el formulario.
   *
   * Por defecto, Enter en cualquier campo de texto envía el formulario, así que quien completa el
   * usuario y sigue con Enter se choca con el error de campos incompletos antes de haber podido
   * escribir la contraseña. Con dos campos, encadenarlos con Enter es lo que la gente espera.
   *
   * El recorrido con teclado sigue completo (FR-025): Tab funciona igual, Enter en la contraseña
   * envía, y el botón se alcanza con Tab.
   */
  function alPresionarTeclaEnUsuario(evento: KeyboardEvent<HTMLInputElement>) {
    if (evento.key !== 'Enter') {
      return
    }

    evento.preventDefault()
    campoPassword.current?.focus()
  }

  async function alEnviar(evento: FormEvent) {
    evento.preventDefault()

    // FR-011: si alguno de los dos está vacío se marca en pantalla y no se llama al servidor.
    if (username.trim() === '' || password === '') {
      setError(MENSAJE_CAMPOS_VACIOS)
      return
    }

    setEnviando(true)
    setError(null)

    try {
      const sesion = await iniciarSesion(username, password)
      onIngreso(sesion)

      // FR-026: vuelve a donde quería ir, siempre que sea una pantalla de esta aplicación.
      const destinoPedido = (ubicacion.state as { destino?: string } | null)?.destino
      navegar(rutaInternaSegura(destinoPedido), { replace: true })
    } catch (fallo) {
      // El mensaje viene del servidor y se muestra tal cual: es el único lugar que decide si
      // corresponde el genérico de credenciales o el de cuenta no habilitada (FR-003, FR-004).
      setError(
        fallo instanceof ErrorHttp
          ? fallo.detalle.mensaje
          : 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
      )
    } finally {
      setEnviando(false)
    }
  }

  const faltanDatos = username.trim() === '' || password === ''

  return (
    <main className="mx-auto flex min-h-screen w-full max-w-sm flex-col justify-center px-6 py-12">
      <h1 className="text-2xl font-semibold text-texto">Sistema Integral de Gestión</h1>
      <p className="mt-1 mb-8 text-sm text-texto-suave">G&amp;T Logística</p>

      <form onSubmit={alEnviar} noValidate className="flex flex-col gap-4">
        {/* El mensaje va arriba del formulario y no borra lo escrito, para poder reintentar de
            inmediato. `role="alert"` hace que un lector de pantalla lo lea al aparecer (FR-025). */}
        {error !== null && (
          <Aviso tono="error" rol="alert">
            {error}
          </Aviso>
        )}

        <Campo id="username" etiqueta="Nombre de usuario" ancho="completo">
          <input
            id="username"
            name="username"
            type="text"
            ref={campoUsuario}
            value={username}
            onChange={(evento) => setUsername(evento.target.value)}
            onKeyDown={alPresionarTeclaEnUsuario}
            autoComplete="username"
            required
            aria-required="true"
            aria-invalid={error !== null && username.trim() === ''}
            className={clasesDeControl(error !== null && username.trim() === '')}
          />
        </Campo>

        <Campo id="password" etiqueta="Contraseña" ancho="completo">
          {/* type="password" siempre: la contraseña nunca se muestra, ni con un botón de ver
              (FR-018). Al viajar en el cuerpo de un POST tampoco queda en la URL. */}
          <input
            id="password"
            name="password"
            type="password"
            ref={campoPassword}
            value={password}
            onChange={(evento) => setPassword(evento.target.value)}
            autoComplete="current-password"
            required
            aria-required="true"
            aria-invalid={error !== null && password === ''}
            className={clasesDeControl(error !== null && password === '')}
          />
        </Campo>

        <Boton type="submit" variante="primario" disabled={enviando || faltanDatos} className="mt-2">
          {enviando ? 'Ingresando…' : 'Ingresar'}
        </Boton>
      </form>
    </main>
  )
}
