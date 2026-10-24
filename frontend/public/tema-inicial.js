/* Se ejecuta antes de CSS/React: un solo controlador para arranque y cambios de tema. */
;(function () {
  var key = 'gyt.tema'
  var listeners = new Set()
  var media = typeof window.matchMedia === 'function' ? window.matchMedia('(prefers-color-scheme: dark)') : null
  var preferred = null
  try { preferred = window.localStorage.getItem(key) } catch (_) { /* Modo privado/storage bloqueado. */ }
  var explicit = preferred === 'light' || preferred === 'dark'
  var current = explicit ? preferred : media && media.matches ? 'dark' : 'light'
  function apply(theme) {
    current = theme
    document.documentElement.dataset.theme = theme
    document.documentElement.style.colorScheme = theme
    listeners.forEach(function (notify) { notify() })
  }
  apply(current)
  window.gytTema = {
    get: function () { return current },
    set: function (theme) {
      if (theme !== 'light' && theme !== 'dark') return
      explicit = true
      try { window.localStorage.setItem(key, theme) } catch (_) { /* La elección sigue funcionando en memoria. */ }
      apply(theme)
    },
    subscribe: function (notify) { listeners.add(notify); return function () { listeners.delete(notify) } },
  }
  window.addEventListener('storage', function (event) {
    if (event.key !== key && event.key !== null) return
    explicit = event.newValue === 'light' || event.newValue === 'dark'
    apply(explicit ? event.newValue : media && media.matches ? 'dark' : 'light')
  })
  if (media && media.addEventListener) media.addEventListener('change', function (event) {
    if (!explicit) apply(event.matches ? 'dark' : 'light')
  })
})()
