import { useCallback, useEffect, useState } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { Layout } from './compartido/Layout'
import { registrarManejadorDeSesionExpirada } from './compartido/clienteHttp'
import { RutaProtegida } from './modules/autenticacion/componentes/RutaProtegida'
import { PantallaIngreso } from './modules/autenticacion/paginas/PantallaIngreso'
import { PantallaInicio } from './modules/autenticacion/paginas/PantallaInicio'
import { CambiarPassword } from './modules/usuarios/paginas/CambiarPassword'
import { DetalleUsuario } from './modules/usuarios/paginas/DetalleUsuario'
import { FormularioUsuario } from './modules/usuarios/paginas/FormularioUsuario'
import { ListadoUsuarios } from './modules/usuarios/paginas/ListadoUsuarios'
import { PanelRoles } from './modules/usuarios/paginas/PanelRoles'
import { FormularioPersona } from './modules/usuarios/personas/paginas/FormularioPersona'
import { ListadoPersonas } from './modules/usuarios/personas/paginas/ListadoPersonas'
import { cerrarSesion, obtenerSesion, type Sesion } from './modules/autenticacion/servicios/sesion'

export default function App() {
  const [sesion, setSesion] = useState<Sesion | null>(null)
  const [cargando, setCargando] = useState(true)

  // Al arrancar se consulta si hay sesión vigente: la cookie es `HttpOnly`, así que el frontend no
  // puede saberlo de otra forma que preguntándole al servidor.
  useEffect(() => {
    obtenerSesion()
      .then(setSesion)
      .finally(() => setCargando(false))
  }, [])

  // FR-015: ante un 401 en cualquier petición se descarta el estado y se vuelve a ingresar.
  useEffect(() => {
    registrarManejadorDeSesionExpirada(() => setSesion(null))
  }, [])

  const alCerrarSesion = useCallback(async () => {
    await cerrarSesion()
    setSesion(null)
  }, [])

  if (cargando) {
    return <p role="status">Cargando…</p>
  }

  return (
    <BrowserRouter>
      <Routes>
        <Route
          path="/ingresar"
          element={
            sesion === null ? (
              <PantallaIngreso onIngreso={setSesion} />
            ) : (
              <Navigate to="/" replace />
            )
          }
        />

        {/* Todas las demás rutas exigen sesión activa (FR-007). */}
        <Route
          path="/"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <PantallaInicio sesion={sesion} />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/usuarios"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <ListadoUsuarios />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/usuarios/nuevo"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <FormularioUsuario />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/usuarios/:id"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <DetalleUsuario />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/usuarios/:id/editar"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <FormularioUsuario />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/usuarios/:id/roles"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <PanelRoles />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/personas"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <ListadoPersonas />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/personas/nueva"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <FormularioPersona />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/personas/:id/editar"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <FormularioPersona />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        {/* Sólo exige sesión, no rol: es la excepción de FR-029. */}
        <Route
          path="/mi-cuenta/contrasena"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <CambiarPassword />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
