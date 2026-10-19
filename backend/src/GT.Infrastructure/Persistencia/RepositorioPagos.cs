using GT.Application.Pagos;
using Microsoft.Extensions.DependencyInjection;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioPagos(GtDbContext contexto, ISerializadorPagos serializador, IServiceScopeFactory scopes) : IRepositorioPagos;
