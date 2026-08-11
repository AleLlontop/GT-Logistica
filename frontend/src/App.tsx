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
import { ListadoTransportistas } from './modules/choferes/transportistas/ListadoTransportistas'
import { FormularioTransportista } from './modules/choferes/transportistas/FormularioTransportista'
import { FormularioChofer } from './modules/choferes/paginas/FormularioChofer'
import { ListadoChoferes } from './modules/choferes/paginas/ListadoChoferes'
import { FichaChofer } from './modules/choferes/paginas/FichaChofer'
import { PanelVencimientos } from './modules/choferes/paginas/PanelVencimientos'
import { TiposDocumentacion } from './modules/choferes/documentacion/TiposDocumentacion'
import { ListadoFlota } from './modules/flota/paginas/ListadoFlota'
import { FichaVehiculo } from './modules/flota/paginas/FichaVehiculo'
import { FormularioVehiculo } from './modules/flota/paginas/FormularioVehiculo'
import { PanelVencimientosFlota } from './modules/flota/paginas/PanelVencimientosFlota'
import { ListadoTiposVehiculo } from './modules/flota/tiposVehiculo/ListadoTiposVehiculo'
import { ListadoClientes } from './modules/viajes/clientes/ListadoClientes'
import { FormularioCliente } from './modules/viajes/clientes/FormularioCliente'
import { ListadoViajes } from './modules/viajes/paginas/ListadoViajes'
import { FormularioViaje } from './modules/viajes/paginas/FormularioViaje'
import { FichaViaje } from './modules/viajes/paginas/FichaViaje'
import { AsignacionViaje } from './modules/viajes/paginas/AsignacionViaje'
import { TotalesPeriodo } from './modules/viajes/paginas/TotalesPeriodo'
import {
  cerrarSesion,
  obtenerSesion,
  Permisos,
  tienePermiso,
  type Sesion,
} from './modules/autenticacion/servicios/sesion'

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

  // Módulo 5: las pantallas se miran con `viajes.consultar` y se operan con `viajes.gestionar`, así
  // que las de este módulo reciben el permiso para decidir qué acciones ofrecen (FR-052).
  const puedeGestionarViajes = tienePermiso(sesion, Permisos.viajesGestionar)

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

        {/* Rutas del Módulo 3. `/choferes/vencimientos` y `/choferes/nuevo` van antes que
            `/choferes/:id` para que no las tome como si fueran un identificador. */}
        <Route
          path="/choferes"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <ListadoChoferes />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/choferes/vencimientos"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <PanelVencimientos />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/choferes/nuevo"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <FormularioChofer />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/choferes/:id"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <FichaChofer />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/choferes/:id/editar"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <FormularioChofer />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/transportistas"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <ListadoTransportistas />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/transportistas/nuevo"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <FormularioTransportista />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/transportistas/:id/editar"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <FormularioTransportista />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        {/* Rutas del Módulo 4. `/flota/vencimientos` y `/flota/nuevo` van antes que `/flota/:id`
            para que no las tome como si fueran un identificador. */}
        <Route
          path="/flota"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <ListadoFlota />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/flota/vencimientos"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <PanelVencimientosFlota />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/flota/nuevo"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <FormularioVehiculo />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/flota/:id"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <FichaVehiculo />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/flota/:id/editar"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <FormularioVehiculo />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/tipos-vehiculo"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <ListadoTiposVehiculo />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        {/* Rutas del Módulo 5. `/viajes/nuevo` y `/viajes/totales` van antes que `/viajes/:id`, y
            `/clientes/nuevo` antes que `/clientes/:id`, para que no las tome como identificadores.
            Es la misma precaución que del lado del backend resuelve la restricción `{id:int}`. */}
        <Route
          path="/viajes"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <ListadoViajes puedeGestionar={puedeGestionarViajes} />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/viajes/totales"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <TotalesPeriodo />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/viajes/nuevo"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <FormularioViaje />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/viajes/:id"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <FichaViaje puedeGestionar={puedeGestionarViajes} />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/viajes/:id/asignacion"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <AsignacionViaje />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/viajes/:id/editar"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <FormularioViaje />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/clientes"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <ListadoClientes puedeGestionar={puedeGestionarViajes} />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/clientes/nuevo"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <FormularioCliente />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/clientes/:id"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <FormularioCliente />
                </Layout>
              )}
            </RutaProtegida>
          }
        />

        <Route
          path="/tipos-documentacion"
          element={
            <RutaProtegida sesion={sesion}>
              {sesion !== null && (
                <Layout
                  username={sesion.username}
                  opcionesMenu={sesion.opcionesMenu}
                  onCerrarSesion={alCerrarSesion}
                >
                  <TiposDocumentacion />
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
