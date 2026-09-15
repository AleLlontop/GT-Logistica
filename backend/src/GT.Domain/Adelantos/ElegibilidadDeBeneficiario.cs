using GT.Domain.Personas;

namespace GT.Domain.Adelantos;

/// <summary>
/// Lo que la regla de elegibilidad necesita saber de una persona, leído de tres módulos: el padrón del
/// Módulo 2, la ficha y el transportista del Módulo 3.
/// </summary>
/// <param name="TieneFicha">Tiene ficha de chofer, activa o no.</param>
/// <param name="CuitTransportista">El del transportista de su ficha, o <c>null</c> sin ficha.</param>
public record PersonaParaAdelanto(
    int Id,
    string Apellido,
    string Nombre,
    string Dni,
    bool Activa,
    TipoIntegrante Tipo,
    bool TieneFicha,
    bool FichaActiva,
    string? CuitTransportista);

/// <summary>Por qué una persona no puede recibir un adelanto del tipo pedido (FR-009).</summary>
public enum MotivoNoElegible
{
    Inexistente,
    Inactiva,

    /// <summary>Sólo con tipo chofer: sin su CUIT no se distingue un chofer propio de uno externo (FR-002a).</summary>
    EmpresaEmisoraNoConfigurada,

    TipoDistinto,
    ChoferExterno,
}

/// <summary>
/// <b>La única escritura de FR-002 y FR-009</b> (research §1). El desplegable de beneficiarios la aplica
/// en memoria sobre las personas activas y el registro la aplica sobre la persona pedida: lo que se ofrece
/// y lo que se acepta no pueden separarse, porque son la misma función.
///
/// No filtrar en SQL "para optimizar": eso obliga a escribir la regla dos veces y a sostenerla con un test
/// que las compare. El desplegable es de decenas de personas y no se pagina.
/// </summary>
public static class ElegibilidadDeBeneficiario
{
    /// <summary>
    /// <c>null</c> si es elegible; si no, el motivo. <b>El orden de las comprobaciones es parte de la
    /// regla</b>: una persona dada de baja que además es chofer externo se informa como dada de baja.
    /// </summary>
    /// <param name="cuitEmpresaEmisora">Once dígitos, o <c>null</c> si la empresa emisora no está configurada.</param>
    public static MotivoNoElegible? Evaluar(
        TipoBeneficiario tipo,
        PersonaParaAdelanto? persona,
        string? cuitEmpresaEmisora)
    {
        if (persona is null)
        {
            return MotivoNoElegible.Inexistente;
        }

        if (!persona.Activa)
        {
            return MotivoNoElegible.Inactiva;
        }

        if (tipo is TipoBeneficiario.Chofer)
        {
            // Chofer lo decide la ficha y no `Persona.Tipo` (research §1 del Módulo 3).
            if (cuitEmpresaEmisora is null)
            {
                return MotivoNoElegible.EmpresaEmisoraNoConfigurada;
            }

            if (!persona.TieneFicha)
            {
                return MotivoNoElegible.TipoDistinto;
            }

            if (!persona.FichaActiva)
            {
                return MotivoNoElegible.Inactiva;
            }

            // Los dos CUIT ya están normalizados a once dígitos: la comparación es exacta (Módulo 9).
            return persona.CuitTransportista == cuitEmpresaEmisora ? null : MotivoNoElegible.ChoferExterno;
        }

        // Empleado exige no tener ficha, activa o no: así nadie aparece bajo los dos tipos, y quien tiene
        // la ficha dada de baja no aparece bajo ninguno (spec §Edge Cases).
        return persona.TieneFicha || persona.Tipo is not TipoIntegrante.Empleado
            ? MotivoNoElegible.TipoDistinto
            : null;
    }
}
