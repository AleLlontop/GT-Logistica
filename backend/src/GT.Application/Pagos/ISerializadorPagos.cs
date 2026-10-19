namespace GT.Application.Pagos;

/// <summary>El repositorio guarda la misma respuesta HTTP sin depender de GT.Api.</summary>
public interface ISerializadorPagos
{
    string Serializar<T>(T valor);
}
