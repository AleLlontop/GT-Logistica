using System.Globalization;
using System.Text;

namespace GT.SeedDemo;

public static class Presentation
{
    public static readonly string[] Clients = [
        "Metalúrgica del Plata", "Aceros del Sur", "Distribuidora Ruta 6", "Comercial San Martín",
        "Industrias del Centro", "Construcciones Pampeanas", "Servicios Industriales del Norte",
        "Hormigones Bonaerenses", "Insumos del Litoral", "Terminal Logística Pilar", "Materiales del Oeste",
        "Industrias Berisso", "Distribuidora Ensenada", "Comercial La Plata", "Aceros del Paraná",
        "Obras y Servicios del Plata", "Logística Metropolitana", "Materiales Ruta 2", "Industria del Paraná",
        "Envases del Litoral", "Cementos de Campana", "Siderúrgica del Puerto", "Alimentos del Sudeste",
        "Cerámicas del Centro", "Papelera del Río", "Maderas del Norte", "Plásticos de Avellaneda",
        "Agroinsumos Pampeanos", "Corralón Los Hornos", "Distribuidora Los Talas", "Bebidas del Puerto",
        "Frigorífico del Sur", "Embalajes del Oeste", "Molinos del Camino", "Equipamientos del Litoral"
    ];
    public static readonly string[] Carriers = [
        "Transportes Ruta Sur", "Logística del Plata", "Cargas Pampeanas", "Transportes San Nicolás",
        "Logística Ruta 9", "Cargas del Litoral", "Transportes del Centro", "Expreso Buenos Aires",
        "Transportes La Plata", "Cargas Industriales del Sur", "Expreso del Paraná", "Transportes El Ombú",
        "Cargas del Camino", "Expreso del Puerto", "Transportes Los Ceibos", "Logística de la Ribera",
        "Transportes Los Talas", "Cargas del Sudeste", "Expreso La Unión", "Transportes del Horizonte"
    ];
    public static readonly string[] Providers = [
        "Neumáticos del Plata", "Repuestos Ruta 2", "Diesel Service La Plata", "Lubricantes del Sur",
        "Taller Integral 44", "Servicios Mecánicos del Oeste", "Seguros del Transporte", "Electromecánica Ruta 6",
        "Repuestos Pesados del Centro", "Gomería Industrial Sur", "Combustibles del Plata", "Servicios Hidráulicos La Plata",
        "Frenos del Camino", "Radiadores del Puerto", "Baterías del Litoral", "Rectificaciones San Martín",
        "Filtros y Correas del Sur", "Auxilio Mecánico La Ribera", "Servicios de Peaje Metropolitanos", "Equipamiento de Talleres del Oeste"
    ];
    public static readonly string[] Banks = ["Banco del Plata", "Banco Regional del Sur", "Banco del Litoral", "Banco de la Ribera", "Banco del Centro", "Banco Pampeano"];
    public static readonly string[] VehicleTypes = ["Tractor", "Camión", "Semirremolque sider", "Semirremolque playo", "Utilitario", "Acoplado", "Equipo especial"];
    public static readonly (string Name, string Scope)[] DocumentTypes = [("Licencia de conducir", "chofer"), ("Seguro de la unidad", "vehiculo"), ("Verificación técnica vehicular", "vehiculo"), ("Habilitación de transporte", "vehiculo")];
    public static readonly string[] Names = ["Martín", "Diego", "Lucas", "Matías", "Nicolás", "Sebastián", "Fernando", "Pablo", "Ezequiel", "Gonzalo"];
    public static readonly string[] Surnames = ["Fernández", "González", "Romero", "Rodríguez", "López", "Martínez", "Torres", "Sánchez", "Herrera", "Castro"];
    public static readonly string[] Addresses = ["Av. 44 1850, La Plata", "Montevideo 1260, Berisso", "Av. Bossinga 730, Ensenada", "Av. Mitre 2840, Avellaneda", "Av. Calchaquí 1520, Quilmes", "Camino General Belgrano 4830, Berazategui", "Av. San Martín 2150, Florencio Varela", "Ruta 8 km 52, Pilar", "Av. 6 de Julio 940, Campana", "Av. Lavalle 1860, Zárate", "Av. Savio 1620, San Nicolás", "Av. Circunvalación 2350, Rosario"];
    public static readonly (string Origin, string Destination)[] Routes = [("La Plata", "Campana"), ("Ensenada", "San Nicolás"), ("Berisso", "Rosario"), ("Campana", "La Plata"), ("Zárate", "Avellaneda"), ("Pilar", "Ensenada"), ("Quilmes", "Campana"), ("Rosario", "La Plata"), ("Berazategui", "Pilar"), ("Florencio Varela", "Zárate")];
    public static readonly string[] Cargo = ["Perfiles de acero", "Bobinas de chapa", "Materiales de construcción", "Pallets de insumos industriales", "Equipamiento de obra", "Envases y embalajes", "Repuestos para maquinaria", "Productos alimenticios envasados"];
    public static readonly string[] Expenses = ["Combustible", "Peajes", "Gastos de ruta", "Mantenimiento preventivo", "Reposición de insumos", "Servicio mecánico"];
    public static string Email(string name, string area = "administracion")
    {
        var slug = new string(name.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsAsciiLetterOrDigit(c)).ToArray()).ToLowerInvariant();
        return area + "@" + slug + ".invalid";
    }
    public static (string Brand, string Model) Vehicle(int type, int index) => type switch
    {
        2 => ("Randon", "Sider de tres ejes"),
        3 => ("Sola y Brusa", "Playo de tres ejes"),
        4 => index % 2 == 0 ? ("Renault", "Master L3H2") : ("Mercedes-Benz", "Sprinter 415"),
        5 => ("Helvética", "Acoplado baranda volcable"),
        6 => ("Randon", "Carretón de carga pesada"),
        _ => new (string, string)[] { ("Scania", "R 450"), ("Volvo", "FH 460"), ("Mercedes-Benz", "Actros 2545"), ("Iveco", "Stralis 480"), ("Volkswagen", "Constellation 25.420"), ("Renault", "Premium 440") }[index % 6]
    };
}
