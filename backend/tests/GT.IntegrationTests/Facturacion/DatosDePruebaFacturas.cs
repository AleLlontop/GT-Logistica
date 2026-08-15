using GT.Domain.Choferes;
using GT.Domain.Facturacion;
using GT.Domain.Usuarios;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Facturacion;

/// <summary>
/// Datos de prueba del módulo de facturación, cargados directamente en la base para no depender de la
/// API que el propio test está verificando.
/// </summary>
public static class DatosDePruebaFacturas
{
    private static int _contadorNumero;

    /// <summary>Un número de comprobante con el formato de FR-027, distinto en cada llamada.</summary>
    public static string NumeroUnico(string puntoDeVenta = "0014") =>
        $"{puntoDeVenta}-{Interlocked.Increment(ref _contadorNumero):D8}";

    /// <summary>
    /// La empresa emisora completa. Casi todo test de emisión la necesita: sin ella la emisión se
    /// rechaza con <c>empresa_emisora_incompleta</c>, que es lo que FR-006 describe.
    /// </summary>
    public static Task<EmpresaEmisora> ConfigurarEmpresaEmisoraAsync(
        this AplicacionDePrueba app,
        string? cbu = "0170099220000067797470",
        string? puntoDeVenta = "0014") =>
        app.ConAlcanceAsync(async contexto =>
        {
            var existente = await contexto.EmpresaEmisora.FirstOrDefaultAsync();

            if (existente is not null)
            {
                return existente;
            }

            var empresa = new EmpresaEmisora
            {
                Id = EmpresaEmisora.IdUnico,
                RazonSocial = "G&T Logística S.R.L.",
                Cuit = "30712345671",
                Domicilio = "Av. Pellegrini 1234, Rosario",
                CondicionIva = "IVA Responsable Inscripto",
                IngresosBrutos = "902-123456-7",
                InicioActividades = new DateOnly(2018, 3, 1),
                PuntoDeVenta = puntoDeVenta,
                Cbu = cbu,
                Telefono = "0341-444-4444",
                Email = "administracion@gtlogistica.com.ar",
            };

            contexto.EmpresaEmisora.Add(empresa);
            await contexto.SaveChangesAsync();

            return empresa;
        });

    /// <summary>
    /// Una factura ya en el estado pedido, saltando la emisión.
    ///
    /// Sirve para armar el punto de partida de un test —"con una factura pendiente…"— sin recorrer una
    /// emisión que ese test no está verificando. Los tests de emisión sí usan el endpoint.
    /// </summary>
    public static Task<FacturaCliente> CrearFacturaAsync(
        this AplicacionDePrueba app,
        int clienteId,
        string? numeroComprobante = null,
        DateOnly? fecha = null,
        EstadoFactura estado = EstadoFactura.Pendiente,
        TipoComprobante tipo = TipoComprobante.FacturaA,
        TipoFacturacion tipoFacturacion = TipoFacturacion.Original,
        int mes = 8,
        int anio = 2026,
        decimal neto = 100_000m,
        DateOnly? vencimientoPago = null,
        DateOnly? fechaCobro = null,
        string? motivoAnulacion = null,
        int? facturaReemplazadaId = null,
        string? detalle = null) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var fechaFactura = fecha ?? FechaHoyArgentina.Hoy();
            var iva = Math.Round(neto * AlicuotasIva.De(tipo), 2, MidpointRounding.AwayFromZero);

            var factura = new FacturaCliente
            {
                NumeroComprobante = numeroComprobante ?? NumeroUnico(),
                Fecha = fechaFactura,
                TipoComprobante = tipo,
                TipoFacturacion = tipoFacturacion,
                CondicionDeVenta = CondicionDeVenta.CuentaCorriente,
                PeriodoMes = (byte)mes,
                PeriodoAnio = (short)anio,
                Detalle = detalle,
                ClienteId = clienteId,
                ClienteRazonSocial = "Distribuidora del Litoral",
                ClienteCuit = "27000000015",
                ClienteDomicilio = "Ruta 9 km 312, Rosario",
                EmisorRazonSocial = "G&T Logística S.R.L.",
                EmisorCuit = "30712345671",
                EmisorDomicilio = "Av. Pellegrini 1234, Rosario",
                EmisorCondicionIva = "IVA Responsable Inscripto",
                EmisorPuntoDeVenta = "0014",
                Neto = neto,
                Iva = iva,
                Total = neto + iva,
                Cae = "75123456789012",
                CaeVencimiento = fechaFactura.AddDays(10),
                VencimientoPago = vencimientoPago ?? fechaFactura.AddDays(30),
                Estado = estado,
                FechaCobro = fechaCobro,
                MotivoAnulacion = motivoAnulacion,
                FacturaReemplazadaId = facturaReemplazadaId,
                DocumentoRuta = $"facturas/{Guid.NewGuid():N}.pdf",
            };

            contexto.Facturas.Add(factura);
            await contexto.SaveChangesAsync();

            return factura;
        });

    /// <summary>Marca viajes existentes como facturados por esa factura, sin pasar por la emisión.</summary>
    public static Task AsociarViajesAsync(
        this AplicacionDePrueba app,
        int facturaId,
        params int[] viajeIds) =>
        app.EnLaBaseAsync(async contexto =>
        {
            await contexto.Viajes
                .Where(viaje => viajeIds.Contains(viaje.Id))
                .ExecuteUpdateAsync(cambio => cambio
                    .SetProperty(viaje => viaje.FacturaId, facturaId)
                    .SetProperty(viaje => viaje.Estado, EstadoViaje.Facturado));
        });

    public static Task<FacturaCliente?> RecargarFacturaAsync(this AplicacionDePrueba app, int id) =>
        app.ConAlcanceAsync(contexto => contexto.Facturas
            .AsNoTracking()
            .FirstOrDefaultAsync(factura => factura.Id == id));

    /// <summary>El historial de una factura, de la línea más vieja a la más nueva (FR-045).</summary>
    public static Task<List<CambioDeEstadoFactura>> HistorialDeFacturaAsync(
        this AplicacionDePrueba app,
        int facturaId) =>
        app.ConAlcanceAsync(contexto => contexto.CambiosDeEstadoFactura
            .Where(cambio => cambio.FacturaId == facturaId)
            .OrderBy(cambio => cambio.OcurridoEn)
            .ThenBy(cambio => cambio.Id)
            .AsNoTracking()
            .ToListAsync());

    /// <summary>
    /// Una cuenta con exactamente los permisos que se le pasan, para verificar el reparto de los tres
    /// (FR-066 a FR-068). Va con un rol propio del test: los del sistema tienen su reparto fijo y
    /// tocarlos rompería los otros tests.
    /// </summary>
    public static Task<Usuario> CrearUsuarioConPermisosAsync(
        this AplicacionDePrueba app,
        string username,
        string password,
        params string[] codigosPermiso) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var hasheador = new GT.Infrastructure.Seguridad.HasheadorPassword();

            var rol = new Rol
            {
                Codigo = $"rol_de_prueba_{username}",
                Nombre = $"Rol de prueba de {username}",
            };

            foreach (var codigo in codigosPermiso)
            {
                rol.Permisos.Add(await contexto.Permisos.FirstAsync(p => p.Codigo == codigo));
            }

            var usuario = new Usuario
            {
                Username = username,
                UsernameNormalizado = username.ToUpperInvariant(),
                Email = $"{username}@gt.local",
                EmailNormalizado = $"{username}@gt.local".ToLowerInvariant(),
                PasswordHash = hasheador.Hashear(password),
                Estado = EstadoUsuario.Activo,
                FechaAlta = DateTime.UtcNow,
                PasswordActualizadaEn = DateTime.UtcNow,
            };

            usuario.Roles.Add(rol);
            contexto.Roles.Add(rol);
            contexto.Usuarios.Add(usuario);
            await contexto.SaveChangesAsync();

            return usuario;
        });

    /// <summary>
    /// Una factura en memoria, sin tocar la base: es la entrada del armador en la vista previa
    /// (research §2). Lleva los viajes ya cargados en su colección.
    /// </summary>
    public static FacturaCliente FacturaEnMemoria(
        TipoComprobante tipo = TipoComprobante.FacturaA,
        TipoFacturacion tipoFacturacion = TipoFacturacion.Original,
        EstadoFactura estado = EstadoFactura.Pendiente,
        string? cbu = "0170099220000067797470",
        string? detalle = "Servicios de transporte del período.",
        string? motivoAnulacion = null,
        IEnumerable<Viaje>? viajes = null)
    {
        var hoy = FechaHoyArgentina.Hoy();
        var incluidos = viajes?.ToList() ?? [ViajeEnMemoria(1041, 30_000m), ViajeEnMemoria(1042, 52_644.63m)];
        var neto = incluidos.Sum(viaje => viaje.Importe);
        var iva = Math.Round(neto * AlicuotasIva.De(tipo), 2, MidpointRounding.AwayFromZero);

        var factura = new FacturaCliente
        {
            NumeroComprobante = "0014-00000003",
            Fecha = hoy,
            TipoComprobante = tipo,
            TipoFacturacion = tipoFacturacion,
            CondicionDeVenta = CondicionDeVenta.CuentaCorriente,
            PeriodoMes = (byte)hoy.Month,
            PeriodoAnio = (short)hoy.Year,
            Detalle = detalle,
            ClienteId = 1,
            ClienteRazonSocial = "Distribuidora del Litoral S.A.",
            ClienteCuit = "27000000015",
            ClienteDomicilio = "Ruta 9 km 312, Rosario",
            EmisorRazonSocial = "G&T Logística S.R.L.",
            EmisorCuit = "30712345671",
            EmisorDomicilio = "Av. Pellegrini 1234, Rosario",
            EmisorCondicionIva = "IVA Responsable Inscripto",
            EmisorIngresosBrutos = "902-123456-7",
            EmisorInicioActividades = new DateOnly(2018, 3, 1),
            EmisorPuntoDeVenta = "0014",
            EmisorCbu = cbu,
            EmisorTelefono = "0341-444-4444",
            EmisorEmail = "administracion@gtlogistica.com.ar",
            Neto = neto,
            Iva = iva,
            Total = neto + iva,
            Cae = "75123456789012",
            CaeVencimiento = hoy.AddDays(10),
            VencimientoPago = hoy.AddDays(30),
            Estado = estado,
            MotivoAnulacion = motivoAnulacion,
            DocumentoRuta = "facturas/pendiente.pdf",
        };

        foreach (var viaje in incluidos)
        {
            factura.Viajes.Add(viaje);
        }

        return factura;
    }

    /// <summary>
    /// Un viaje en memoria como los que la vista previa arma después de leerlos de la base.
    ///
    /// <b><see cref="Viaje.Numero"/> se escribe por reflexión, y hace falta.</b> Su <c>private set</c>
    /// existe para que ningún caso de uso pueda asignarlo —el valor lo pone el <c>DEFAULT</c> de la
    /// columna—, y esa garantía no se toca. Pero un viaje construido en memoria queda con el número en
    /// <c>0</c>, y entonces la columna <c>Código</c> del detalle saldría en cero en el test mientras en
    /// producción sale bien: el test mentiría en la dirección peligrosa. Escribirlo acá, en el helper
    /// de datos de prueba y con este comentario, es lo que hace que el test verifique lo que pasa de
    /// verdad (FR-031e).
    /// </summary>
    public static Viaje ViajeEnMemoria(int numero, decimal importe)
    {
        var viaje = new Viaje
        {
            Id = numero,
            ClienteId = 1,
            Fecha = FechaHoyArgentina.Hoy(),
            Origen = "Rosario",
            Destino = "Córdoba",
            NumeroRemito = $"R-{numero}",
            Importe = importe,
            Estado = EstadoViaje.Rendido,
        };

        typeof(Viaje)
            .GetProperty(nameof(Viaje.Numero))!
            .SetValue(viaje, numero);

        return viaje;
    }
}

// ── Lo que devuelve el backend, tal como lo fija contracts/facturacion-api.yaml ─────────────────

public record LogoLeido(string Nombre, string Url);

public record EmpresaEmisoraLeida(
    bool Configurada,
    List<string> Faltantes,
    string? RazonSocial,
    string? Cuit,
    string? Domicilio,
    string? CondicionIva,
    string? IngresosBrutos,
    DateOnly? InicioActividades,
    string? PuntoDeVenta,
    string? Cbu,
    string? Telefono,
    string? Email,
    LogoLeido? Logo);

public record ViajeFacturableLeido(
    int Id,
    int Numero,
    string Fecha,
    string? NumeroRemito,
    string Origen,
    string Destino,
    decimal Importe,
    bool PuedeFacturarse,
    string? MotivoNoFacturable);

public record FacturaResumenLeido(int Id, string NumeroComprobante, string Fecha, string Estado);

public record ClienteResumidoLeido(int Id, string RazonSocial, bool Activo);

public record FacturaListadoLeida(
    int Id,
    string NumeroComprobante,
    string Fecha,
    ClienteResumidoLeido Cliente,
    string TipoComprobante,
    int Mes,
    int Anio,
    decimal Total,
    string Estado,
    string VencimientoPago,
    string? MotivoAnulacion,
    string? FechaCobro);

public record PaginaDeFacturasLeida(
    List<FacturaListadoLeida> Items,
    int Total,
    int Pagina,
    int TamanioPagina);

public record EmisorLeido(
    string RazonSocial,
    string Cuit,
    string Domicilio,
    string CondicionIva,
    string? IngresosBrutos,
    DateOnly? InicioActividades,
    string? PuntoDeVenta,
    string? Cbu,
    string? Telefono,
    string? Email);

public record ClienteDeFacturaLeido(
    int Id,
    string RazonSocial,
    string Cuit,
    string Domicilio,
    bool Activo);

public record ViajeDeFacturaLeido(
    int Id,
    int Numero,
    string Fecha,
    string? NumeroRemito,
    string Origen,
    string Destino,
    decimal Importe);

public record EntradaDeHistorialLeida(
    string? EstadoAnterior,
    string? EstadoNuevo,
    string Usuario,
    DateTime OcurridoEn);

public record FacturaDetalleLeida(
    int Id,
    string NumeroComprobante,
    string Fecha,
    string TipoComprobante,
    string TipoFacturacion,
    string CondicionDeVenta,
    int Mes,
    int Anio,
    string? Detalle,
    EmisorLeido Emisor,
    ClienteDeFacturaLeido Cliente,
    List<ViajeDeFacturaLeido> Viajes,
    decimal Neto,
    decimal Iva,
    decimal Alicuota,
    decimal Total,
    string Cae,
    string CaeVencimiento,
    string VencimientoPago,
    string Estado,
    string? FechaCobro,
    string? MotivoAnulacion,
    FacturaResumenLeido? ReemplazaA,
    FacturaResumenLeido? ReemplazadaPor,
    string DocumentoUrl,
    List<EntradaDeHistorialLeida> Historial);

public record FilaDeVencimientoLeida(
    int Id,
    string NumeroComprobante,
    string Cliente,
    decimal Total,
    string VencimientoPago,
    int Dias);

public record TotalPorClienteLeido(
    int ClienteId,
    string RazonSocial,
    int Cantidad,
    decimal Facturado,
    decimal Cobrado,
    decimal Pendiente);

/// <summary>
/// El cuerpo de error del módulo, con los campos opcionales que llevan los rechazos que necesitan
/// explicarse: qué falta, qué factura está en conflicto, qué viajes lo producen y qué hay que
/// confirmar.
/// </summary>
public record ErrorFacturaLeido(
    string Codigo,
    string Mensaje,
    string? Campo,
    List<string>? Faltantes,
    FacturaResumenLeido? FacturaEnConflicto,
    List<ViajeEnConflictoLeido>? Viajes,
    string? MotivoConfirmacion,
    string? FechaCobro);

public record ViajeEnConflictoLeido(int Id, int Numero, string Motivo);
