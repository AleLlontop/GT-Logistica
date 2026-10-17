using GT.Application.Proveedores;
using Microsoft.Extensions.Logging;

namespace GT.Infrastructure.Persistencia;

public class RegistroErroresFacturaProveedor(ILogger<RegistroErroresFacturaProveedor> log) : IRegistroErroresFacturaProveedor
{
    public void Error(Exception error,string mensaje)=>log.LogError(error,"{Mensaje}",mensaje);
}
