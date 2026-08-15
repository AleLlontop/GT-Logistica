namespace GT.Domain.Facturacion;

/// <summary>
/// Los datos con los que sale toda factura: quién factura (FR-001, FR-002).
///
/// <b>Es una única fila para todo el sistema</b>: se edita, nunca se crea una segunda ni se borra. Lo
/// garantiza un <c>CHECK ([Id] = 1)</c> en la base y no la disciplina del código: una garantía escrita
/// en la base cuesta una línea de configuración y no depende de que nadie escriba nunca un
/// <c>Add</c> de más (research §12).
///
/// <b>La fila no existe hasta el primer guardado.</b> El <c>GET</c> sin fila responde
/// <c>configurada: false</c> con los obligatorios faltantes, y el <c>PUT</c> la crea la primera vez y
/// la actualiza siempre después. Sembrar una fila vacía obligaría a que las cuatro columnas
/// obligatorias fueran anulables, y entonces la base dejaría de garantizar lo que FR-002 exige.
///
/// <b>La factura no la referencia: la copia.</b> Los diez datos de texto se congelan en cada factura
/// al emitirla (FR-034), así que corregir acá un domicilio no cambia ninguna factura ya emitida. El
/// logo es la única excepción declarada: no se congela y se lee siempre de acá (research §5).
/// </summary>
public class EmpresaEmisora
{
    /// <summary>Siempre <c>1</c>. La base lo impone con un <c>CHECK</c>.</summary>
    public const int IdUnico = 1;

    public int Id { get; set; } = IdUnico;

    // ── Los cuatro obligatorios (FR-002) ────────────────────────────────────────────────────────

    public required string RazonSocial { get; set; }

    /// <summary>
    /// Once dígitos con verificador válido. Se <b>normaliza antes de validar y de guardar</b> con
    /// <c>NormalizadorDocumentoNumerico</c> y <c>ValidadorCuit</c> del Módulo 3: escribir
    /// <c>30-71234567-8</c> es válido y se guarda como <c>30712345678</c>.
    /// </summary>
    public required string Cuit { get; set; }

    public required string Domicilio { get; set; }

    /// <summary>Texto libre: la spec no enumera opciones para el emisor, a diferencia del cliente.</summary>
    public required string CondicionIva { get; set; }

    // ── Los seis opcionales ─────────────────────────────────────────────────────────────────────

    public string? IngresosBrutos { get; set; }

    public DateOnly? InicioActividades { get; set; }

    /// <summary>Cuatro dígitos. Se propone en el alta de factura para armar el número (FR-027).</summary>
    public string? PuntoDeVenta { get; set; }

    /// <summary>Vacío ⇒ la banda de CBU no sale impresa en el documento (FR-031, US2 esc. 28).</summary>
    public string? Cbu { get; set; }

    public string? Telefono { get; set; }

    public string? Email { get; set; }

    // ── Logo (FR-003, FR-004) ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ruta relativa dentro del volumen de archivos. El nombre en disco lo genera el sistema, nunca
    /// se usa el que cargó quien sube (convención [003]).
    /// </summary>
    public string? LogoRuta { get; set; }

    /// <summary>Deducido de la <b>firma</b> del archivo, no de la extensión ni del <c>Content-Type</c>.</summary>
    public string? LogoTipoContenido { get; set; }

    /// <summary>Sólo para mostrar y para que "Guardar como" proponga el original.</summary>
    public string? LogoNombreOriginal { get; set; }

    /// <summary>
    /// Los obligatorios que faltan, por nombre y en el orden en que aparecen en el formulario.
    /// Vacía cuando la configuración está completa (FR-002, FR-006).
    ///
    /// El mensaje del rechazo al emitir los nombra: saber que falta configurar la empresa sin saber
    /// qué falta no ayuda a resolverlo (contracts/README §Alta de factura).
    /// </summary>
    public IReadOnlyList<string> ObligatoriosFaltantes()
    {
        var faltantes = new List<string>();

        if (string.IsNullOrWhiteSpace(RazonSocial)) faltantes.Add(NombresDeCampo.RazonSocial);
        if (string.IsNullOrWhiteSpace(Cuit)) faltantes.Add(NombresDeCampo.Cuit);
        if (string.IsNullOrWhiteSpace(Domicilio)) faltantes.Add(NombresDeCampo.Domicilio);
        if (string.IsNullOrWhiteSpace(CondicionIva)) faltantes.Add(NombresDeCampo.CondicionIva);

        return faltantes;
    }

    /// <summary>
    /// Los cuatro obligatorios que hay que nombrar cuando faltan. Con la fila ausente faltan los
    /// cuatro, y la ausencia <b>es</b> el estado "sin configurar" (US1 esc. 1).
    /// </summary>
    public static IReadOnlyList<string> TodosLosObligatorios() =>
    [
        NombresDeCampo.RazonSocial,
        NombresDeCampo.Cuit,
        NombresDeCampo.Domicilio,
        NombresDeCampo.CondicionIva,
    ];

    /// <summary>Cómo se nombra cada obligatorio en los mensajes, en español (contracts/README).</summary>
    public static class NombresDeCampo
    {
        public const string RazonSocial = "razón social";
        public const string Cuit = "CUIT";
        public const string Domicilio = "domicilio";
        public const string CondicionIva = "condición de IVA";
    }
}
