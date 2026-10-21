namespace GT.Application.Reportes.Analisis;

public enum EstadoSnapshotAnalisis
{
    Desactivado = 0,
    Activado = 1,
    Desactivando = 2,
    Activando = 3,
}

public sealed record DiagnosticoSnapshotAnalisis(EstadoSnapshotAnalisis Estado, string Descripcion, bool ReadCommittedSnapshot)
{
    public bool Disponible => Estado == EstadoSnapshotAnalisis.Activado;
}
