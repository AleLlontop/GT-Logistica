using System.Net.Http.Json;
using GT.Application.Choferes;
using GT.Application.Pagos;
using GT.Application.Usuarios.Personas;
using GT.Domain.Choferes;
using GT.Domain.Pagos;
using GT.Domain.Personas;
using GT.Domain.Proveedores;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Proveedores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests;

public class BusquedaPadronesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static int semilla = 10000;

    [Fact]
    public async Task PersonasYEmpleadosBuscanParcialesYNombreCompleto_EnAmbosSentidos_SinCambiarDatos()
    {
        var personas = new[] { Persona("Martín", "Martínez"), Persona("Martin", "Martinez", activa: false), Persona("Martín", "Martínez") };
        await app.EnLaBaseAsync(async db => { db.Personas.AddRange(personas); await db.SaveChangesAsync(); });
        using var cliente = await app.CrearClienteAutenticadoAsync();
        foreach (var ruta in new[] { "/api/personas", "/api/empleados" })
        {
            int[]? orden = null;
            foreach (var texto in new[] { "martin martinez", "martín martínez", "MARTIN MARTINEZ", "MARTÍN MARTÍNEZ", "martinez martin", "MARTÍNEZ MARTÍN", "martin", "rtin", "martinez" })
            {
                var filas = (await cliente.GetFromJsonAsync<PersonaDto[]>($"{ruta}?texto={Uri.EscapeDataString(texto)}"))!;
                Assert.Equal(personas.Length, filas.Length);
                Assert.Equal(personas.Select(p => p.Id).Order(), filas.Select(p => p.Id).Order());
                var ids = filas.Select(p => p.Id).ToArray();
                orden ??= ids;
                Assert.Equal(orden, ids);
                Assert.Equal(new[] { personas[0].Id, personas[2].Id }.Order(), filas.Where(p => p.Nombre == "Martín" && p.Apellido == "Martínez").Select(p => p.Id));
                Assert.All(filas, fila =>
                {
                    var original = personas.Single(p => p.Id == fila.Id);
                    Assert.Equal(original.Nombre, fila.Nombre);
                    Assert.Equal(original.Apellido, fila.Apellido);
                    Assert.Equal(original.Dni, fila.Dni);
                });
            }
        }
        var activas = (await cliente.GetFromJsonAsync<PersonaDto[]>("/api/empleados?texto=martin&soloActivas=true"))!;
        Assert.Equal(personas.Where(p => p.Activa).Select(p => p.Id).Order(), activas.Select(p => p.Id).Order());
        var porDni = (await cliente.GetFromJsonAsync<PersonaDto[]>($"/api/personas?texto={personas[1].Dni}"))!;
        Assert.Equal(personas[1].Id, Assert.Single(porDni).Id);
    }

    [Fact]
    public async Task LaEnieNoSeConfundeConEne_YContainsMantieneLosCaracteresLiterales()
    {
        await app.EnLaBaseAsync(async db =>
        {
            var conEnie = Persona("Inés", "Peña"); var conEne = Persona("Ines", "Pena");
            db.Personas.AddRange(conEnie, conEne);
            await db.SaveChangesAsync();
            var repo = new RepositorioPersonas(db);
            Assert.Equal(conEne.Id, Assert.Single(await repo.BuscarAsync("PENA", false)).Id);
            Assert.Equal(conEnie.Id, Assert.Single(await repo.BuscarAsync("PEÑA", false)).Id);
            Assert.Empty(await repo.BuscarAsync("%_[]\\", false));
        });
    }

    [Fact]
    public async Task ProveedoresBuscaRazonSocialYNombreComercial_AntesDelTotalYPagina_YRespetaEscapesYFiltros()
    {
        await app.EnLaBaseAsync(async db =>
        {
            var normales = Enumerable.Range(0, 21).Select(i =>
            {
                var proveedor = Proveedor(i % 2 == 0 ? "Logística Proveedores Corte" : "Logistica Proveedores Corte");
                proveedor.NombreComercial = i % 2 == 0 ? "Distribución Almacén" : "Distribucion Almacen";
                proveedor.Activo = i != 20;
                return proveedor;
            }).ToArray();
            var fletero = EscenarioProveedores.Fletero(Transportista("Logística Proveedores Corte"));
            var literal = Proveedor("Especial %_[]\\ Logística");
            var conEnie = Proveedor("Peña Comercial"); var conEne = Proveedor("Pena Comercial");
            db.Proveedores.AddRange(normales);
            db.Proveedores.AddRange(fletero, literal, conEnie, conEne);
            await db.SaveChangesAsync();
            var repo = new RepositorioProveedores(db);
            var esperados = normales.Append(fletero).ToArray();
            int[]? orden = null;
            foreach (var texto in new[] { "logistica proveedores", "logística proveedores", "LOGISTICA PROVEEDORES", "LOGÍSTICA PROVEEDORES", "ISTICA PROV" })
            {
                var primera = await repo.ConsultarAsync(new(Q: texto), default);
                var segunda = await repo.ConsultarAsync(new(Q: texto, Pagina: 2), default);
                VerificarPaginas(primera, segunda, esperados.Select(p => p.Id), p => p.Id, ref orden);
                Assert.All(primera.Items.Concat(segunda.Items), fila => Assert.Equal(
                    esperados.Single(p => p.Id == fila.Id) is { Tipo: TipoProveedor.Fletero } ? fletero.Transportista!.Nombre : normales.Single(p => p.Id == fila.Id).RazonSocial,
                    fila.RazonSocial));
            }
            var comercial = await repo.ConsultarAsync(new(RazonSocial: "logistica", NombreComercial: "DISTRIBUCION ALMACEN", Estado: "inactivo", Tipo: "proveedorNormal"), default);
            Assert.Equal(1, comercial.Total);
            Assert.Equal(normales[20].Id, Assert.Single(comercial.Items).Id);
            Assert.Equal(normales[20].NombreComercial, Assert.Single(comercial.Items).NombreComercial);
            int[]? ordenComercial = null;
            foreach (var texto in new[] { "distribucion almacen", "distribución almacén", "DISTRIBUCION ALMACEN", "DISTRIBUCIÓN ALMACÉN", "ALMACEN" })
                VerificarPaginas(await repo.ConsultarAsync(new(Q: texto), default),
                    await repo.ConsultarAsync(new(Q: texto, Pagina: 2), default), normales.Select(p => p.Id), p => p.Id, ref ordenComercial);
            Assert.Equal(literal.Id, Assert.Single((await repo.ConsultarAsync(new(Q: "Especial %_[]\\"), default)).Items).Id);
            Assert.Equal(conEne.Id, Assert.Single((await repo.ConsultarAsync(new(Q: "PENA COMERCIAL"), default)).Items).Id);
            Assert.Equal(conEnie.Id, Assert.Single((await repo.ConsultarAsync(new(Q: "PEÑA COMERCIAL"), default)).Items).Id);
            Assert.Equal(normales[0].Id, Assert.Single((await repo.ConsultarAsync(new(Cuit: normales[0].Cuit), default)).Items).Id);
        });
    }

    [Fact]
    public async Task TransportistasYSelectorExternoIgnoranAcentos_AntesDePaginar_SinCambiarNombreNiCuit()
    {
        await app.EnLaBaseAsync(async db =>
        {
            if (!await db.EmpresaEmisora.AnyAsync()) db.EmpresaEmisora.Add(EscenarioProveedores.Emisor());
            var transportistas = Enumerable.Range(0, 23).Select(i =>
                Transportista(i % 2 == 0 ? "Logística Externos Corte" : "Logistica Externos Corte", activo: i != 22)).ToArray();
            db.Transportistas.AddRange(transportistas);
            await db.SaveChangesAsync();
            var proveedores = new RepositorioProveedores(db);
            var repo = new RepositorioTransportistas(db);
            int[]? ordenExternos = null; int[]? ordenTransportistas = null;
            foreach (var texto in new[] { "logistica externos", "logística externos", "LOGISTICA EXTERNOS", "LOGÍSTICA EXTERNOS", "ISTICA EXTER" })
            {
                var primera = await proveedores.ExternosAsync(texto, 1, default);
                var segunda = await proveedores.ExternosAsync(texto, 2, default);
                VerificarPaginas(primera, segunda, transportistas.Select(t => t.Id), t => t.Id, ref ordenExternos);
                Assert.All(primera.Items.Concat(segunda.Items), fila => Assert.Equal(transportistas.Single(t => t.Id == fila.Id).Nombre, fila.RazonSocial));
                var todos = await repo.ConsultarAsync(texto, null, false, default);
                Assert.Equal(transportistas.Select(t => t.Id).Order(), todos.Select(t => t.Transportista.Id).Order());
                var ids = todos.Select(t => t.Transportista.Id).ToArray();
                ordenTransportistas ??= ids;
                Assert.Equal(ordenTransportistas, ids);
                Assert.Equal(22, (await repo.ConsultarAsync(texto, null, true, default)).Count);
            }
            Assert.Equal(transportistas[0].Id, Assert.Single(await repo.ConsultarAsync(transportistas[0].Cuit, transportistas[0].Cuit, false, default)).Transportista.Id);
            var porCuit = Assert.Single((await proveedores.ExternosAsync(transportistas[0].Cuit, 1, default)).Items);
            Assert.Equal(transportistas[0].Id, porCuit.Id);
            Assert.Equal(transportistas[0].Cuit, porCuit.Cuit);
        });
    }

    [Fact]
    public async Task ChoferesFiltraApellidoConYsinAcento_AntesDelTotalYPagina_YConservaDniYEstado()
    {
        await app.EnLaBaseAsync(async db =>
        {
            var transportista = Transportista("Transportista de apellidos");
            db.Transportistas.Add(transportista);
            await db.SaveChangesAsync();
            var choferes = Enumerable.Range(0, 22).Select(i =>
            {
                var persona = Persona("José", i % 2 == 0 ? "Gómez Corte" : "Gomez Corte");
                return new Chofer { Persona = persona, PersonaId = 0, Cuil = GT.IntegrationTests.Choferes.DatosDePrueba.CuilValidoPara(int.Parse(persona.Dni)), Transportista = transportista, TransportistaId = transportista.Id, Activo = i != 21 };
            }).ToArray();
            db.Choferes.AddRange(choferes);
            await db.SaveChangesAsync();
            var repo = new RepositorioChoferes(db);
            int[]? orden = null;
            foreach (var texto in new[] { "gomez corte", "gómez corte", "GOMEZ CORTE", "GÓMEZ CORTE", "OMEZ COR" })
            {
                var primera = await repo.ConsultarAsync(new(Apellido: texto, TransportistaId: transportista.Id), new(2026, 11, 26));
                var segunda = await repo.ConsultarAsync(new(Apellido: texto, TransportistaId: transportista.Id, Pagina: 2), new(2026, 11, 26));
                VerificarPaginas(primera, segunda, choferes.Where(c => c.Activo).Select(c => c.Id), c => c.Id, ref orden);
                Assert.All(primera.Items.Concat(segunda.Items), fila => Assert.Equal(choferes.Single(c => c.Id == fila.Id).Persona!.Apellido, fila.Apellido));
            }
            var inactivo = await repo.ConsultarAsync(new(Apellido: "gomez corte", SoloActivos: false), new(2026, 11, 26));
            Assert.Equal(choferes[21].Id, Assert.Single(inactivo.Items).Id);
            var porDni = await repo.ConsultarAsync(new(Dni: choferes[0].Persona!.Dni), new(2026, 11, 26));
            Assert.Equal(choferes[0].Id, Assert.Single(porDni.Items).Id);
        });
    }

    [Fact]
    public async Task OpcionesDePagoElegiblesEHistoricasFiltranEnSql_AntesDePaginacion_YConservanSusReglas()
    {
        var usuario = await app.ObtenerAdministradorAsync();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GtDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<IRepositorioPagos>();
        var proveedores = Enumerable.Range(0, 23).Select(i => Proveedor(i % 2 == 0 ? "Logística Pagos Corte" : "Logistica Pagos Corte")).ToArray();
        proveedores[0] = EscenarioProveedores.Fletero(Transportista("Logística Pagos Corte"));
        proveedores[21].Activo = false;
        for (var i = 0; i < proveedores.Length; i++) ConFacturaYOrden(db, proveedores[i], usuario.Id, pagada: i == 22);
        var literal = Proveedor("Peña %_[]\\ Pagos");
        ConFacturaYOrden(db, literal, usuario.Id);
        await db.SaveChangesAsync();
        int[]? ordenElegibles = null; int[]? ordenHistoricos = null;
        foreach (var texto in new[] { "logistica pagos", "logística pagos", "LOGISTICA PAGOS", "LOGÍSTICA PAGOS", "ISTICA PAG" })
        {
            var primera = await ElegibilidadPagosSql.ProveedoresAsync(db, texto, 1, default);
            var segunda = await ElegibilidadPagosSql.ProveedoresAsync(db, texto, 2, default);
            VerificarPaginas(primera, segunda, proveedores.Take(22).Select(p => p.Id), p => p.Id, ref ordenElegibles);
            Assert.Contains(primera.Items.Concat(segunda.Items), p => p.Id == proveedores[21].Id && !p.Activo);
            var historica = await repo.ProveedoresHistoricosAsync(texto, 1);
            var siguiente = await repo.ProveedoresHistoricosAsync(texto, 2);
            VerificarPaginas(historica, siguiente, proveedores.Select(p => p.Id), p => p.Id, ref ordenHistoricos);
            Assert.All(historica.Items.Concat(siguiente.Items), fila =>
            {
                var original = proveedores.Single(p => p.Id == fila.Id);
                Assert.Equal(original.Tipo == TipoProveedor.Fletero ? original.Transportista!.Nombre : original.RazonSocial, fila.RazonSocial);
            });
        }
        Assert.Equal(literal.Id, Assert.Single((await ElegibilidadPagosSql.ProveedoresAsync(db, "Peña %_[]\\", 1, default)).Items).Id);
        Assert.Equal(literal.Id, Assert.Single((await repo.ProveedoresHistoricosAsync("Peña %_[]\\", 1)).Items).Id);
        Assert.Empty((await ElegibilidadPagosSql.ProveedoresAsync(db, "pena %_[]\\", 1, default)).Items);
        Assert.Empty((await repo.ProveedoresHistoricosAsync("pena %_[]\\", 1)).Items);
        Assert.Equal(proveedores[1].Id, Assert.Single((await ElegibilidadPagosSql.ProveedoresAsync(db, proveedores[1].Cuit, 1, default)).Items).Id);
    }

    private static void VerificarPaginas<T>(PaginaDe<T> primera, PaginaDe<T> segunda, IEnumerable<int> ids, Func<T, int> id, ref int[]? orden)
    {
        var esperados = ids.Order().ToArray();
        Assert.Equal(esperados.Length, primera.Total); Assert.Equal(esperados.Length, segunda.Total);
        Assert.Equal(20, primera.Items.Count); Assert.Equal(esperados.Length - 20, segunda.Items.Count);
        var encontrados = primera.Items.Concat(segunda.Items).Select(id).ToArray();
        Assert.Equal(esperados, encontrados.Order());
        orden ??= encontrados;
        Assert.Equal(orden, encontrados);
    }

    private static string DatosCuit() => PadronProveedorTests.Cuit(Interlocked.Increment(ref semilla));

    private static Persona Persona(string nombre, string apellido, bool activa = true) => new()
    {
        Nombre = nombre, Apellido = apellido, Dni = (60000000 + Interlocked.Increment(ref semilla)).ToString(),
        Tipo = TipoIntegrante.Empleado, Telefono = "3411234567", Email = "persona@example.com", FechaNacimiento = new(1990, 1, 1), Activa = activa,
    };

    private static Proveedor Proveedor(string nombre) => EscenarioProveedores.Normal(DatosCuit(), nombre);

    private static Transportista Transportista(string nombre, bool activo = true) => new()
    {
        Nombre = nombre, Cuit = DatosCuit(), Tipo = TipoPersona.Juridica, Telefono = "3411234567", Email = "transporte@example.com", Activo = activo,
    };

    private static void ConFacturaYOrden(GtDbContext db, Proveedor proveedor, int usuarioId, bool pagada = false)
    {
        var fletero = proveedor.Tipo == TipoProveedor.Fletero;
        var nombre = fletero ? proveedor.Transportista!.Nombre : proveedor.RazonSocial!;
        var cuit = fletero ? proveedor.Transportista!.Cuit : proveedor.Cuit!;
        db.FacturasProveedor.Add(new()
        {
            Proveedor = proveedor, ProveedorRazonSocial = nombre, ProveedorCuit = cuit,
            TipoProveedorAlRegistrar = proveedor.Tipo, Numero = "F-" + cuit,
            Fecha = new(2026, 11, 20), FechaVencimientoCae = new(2026, 11, 30), Detalle = "Prueba de búsqueda",
            Cae = "123", Neto = 100m, Iva = 21m, ImporteTotal = 121m, Estado = pagada ? EstadoFacturaProveedor.Pagada : EstadoFacturaProveedor.Pendiente,
            Liquidacion = fletero ? EscenarioProveedores.Pendiente(proveedor.Transportista!, 121m) : null,
            ImporteLiquidacionAlRegistrar = fletero ? 121m : null,
        });
        db.OrdenesPagoProveedor.Add(new()
        {
            Proveedor = proveedor, ProveedorRazonSocial = nombre, ProveedorCuit = cuit,
            TipoProveedorAlRegistrar = proveedor.Tipo, ProveedorActivoAlGenerar = proveedor.Activo,
            ImporteTotal = 121m, Fecha = new(2026, 11, 20), RegistradaEn = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc), UsuarioId = usuarioId,
        });
    }
}
