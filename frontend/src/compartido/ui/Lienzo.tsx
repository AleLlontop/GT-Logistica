/**
 * La luz ambiental del sistema: dos elipses radiales detrás de todo el contenido (FR-002).
 *
 * Va **una sola vez, en la raíz**, y nunca dentro de un contenedor con desplazamiento: adentro de
 * uno los orbes se moverían con el contenido y dejarían de ser luz de fondo para pasar a ser dos
 * manchas que viajan.
 *
 * `pointer-events-none` es lo que la mantiene decorativa: no intercepta ningún clic. Y `-z-10` la
 * deja atrás de todo sin necesidad de que nada más declare su propio `z-index`.
 *
 * Los valores son los de `references/tokens.md`, sin cambios.
 */
export function Lienzo() {
  return (
    <div aria-hidden="true" className="pointer-events-none fixed inset-0 -z-10 overflow-hidden">
      {/* Orbe 1 — arriba a la derecha, índigo */}
      <div
        className="absolute"
        style={{
          width: '760px',
          height: '760px',
          top: '-260px',
          right: '-80px',
          background:
            'radial-gradient(circle, rgba(107,125,255,0.16) 0%, rgba(107,125,255,0) 100%)',
        }}
      />

      {/* Orbe 2 — abajo a la izquierda, verde */}
      <div
        className="absolute"
        style={{
          width: '620px',
          height: '620px',
          bottom: '-100px',
          left: '-160px',
          background: 'radial-gradient(circle, rgba(51,191,158,0.10) 0%, rgba(51,191,158,0) 100%)',
        }}
      />
    </div>
  )
}
