export interface PropsEditor<T> {
  inicial?: T
  hoy: string
  onGuardar: (medio: T) => void
  onCancelar: () => void
  erroresServidor?: Record<string, string>
}
