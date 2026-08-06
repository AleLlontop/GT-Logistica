# Quickstart: Gestionar choferes y su documentación (Módulo 3)

Cómo levantar el sistema con este módulo y comprobar, operando la aplicación, que las 7 historias de
usuario y los 11 criterios de éxito se cumplen. Sin leer código, sin mirar logs y sin consultas SQL
(Principio IV).

---

## Requisitos previos

- Podman (desarrollo local) o Docker (CI).
- `.env` completo. Si venís de los módulos anteriores ya lo tenés.

Este módulo agrega **una** variable, con valor por defecto razonable:

```bash
# Carpeta donde se guardan los archivos escaneados de la documentación.
# Es un volumen del compose: queda fuera del repositorio y fuera de la raíz web.
GT_ARCHIVOS_RUTA=/var/gt/archivos
```

---

## Levantar el sistema

```bash
podman compose up -d          # o: docker compose up -d
```

La migración se aplica sola al arrancar. Además de crear las cuatro tablas, siembra el permiso
`choferes.gestionar` y se lo otorga a los roles *Tráfico* y *Administrador del sistema*.

---

## Preparar dos usuarios para el recorrido

Este módulo es el primero cuyo acceso **no** es exclusivo del administrador, así que conviene
verificarlo con las dos cuentas.

Entrá como `admin` y, desde **Gestión de usuarios**, creá un usuario con el rol *Tráfico* —por
ejemplo `mlopez`—. Vas a usar las dos cuentas en el paso 12.

---

## Recorrido de validación

### 1. El módulo aparece en el menú (FR-027)

Ingresá como `admin`. En el menú tienen que estar **Choferes**, **Transportistas** y **Tipos de
documentación**, además de las entradas del Módulo 2.

### 2. Los padrones arrancan vacíos (FR-023)

Entrá a **Transportistas** y a **Tipos de documentación**. Las dos tienen que decir explícitamente
que todavía no hay nada cargado — no una tabla vacía sin explicación.

### 3. No se puede registrar un chofer sin transportista (US2 esc. 4)

Entrá a **Choferes** → *Nuevo chofer*. Como no hay ningún transportista activo, el formulario tiene
que informarlo y **no dejarte completar el alta**, con un enlace a la pantalla de transportistas.

### 4. Registrar transportistas (User Story 1, SC-001)

En **Transportistas** → *Nuevo transportista*, cargá dos:

- **G&T Logística S.A.**, tipo *jurídica*, con su CUIT real.
- Un transportista terciarizado cualquiera.

Probá los rechazos:

| Probá esto | Tiene que pasar |
|---|---|
| El mismo CUIT dos veces | Avisa que ese CUIT ya está registrado |
| Un CUIT con el dígito verificador mal (por ejemplo `20-12345678-0`) | Marca el campo: no alcanza con que sean 11 dígitos |
| El CUIT con guiones y sin guiones | Es el **mismo** transportista: se normaliza antes de validar (FR-025) |
| Sin elegir tipo de persona | Avisa que es obligatorio |

### 5. Registrar un chofer (User Story 2)

**Choferes** → *Nuevo chofer*. Cargá uno asignado a G&T Logística S.A.

| Probá esto | Tiene que pasar |
|---|---|
| Una fecha de nacimiento de menos de 18 años | Rechaza: un chofer tiene que ser mayor de edad (FR-011) |
| Un CUIL con verificador inválido | Marca el campo |
| Sin elegir transportista | Avisa que es obligatorio (FR-008) |

### 6. Reutilizar una persona del padrón (caso límite, research §6)

Este paso es el que verifica que el chofer **no duplica** el padrón del Módulo 2.

1. Como `admin`, entrá a **Personas** (Módulo 2) y cargá una persona con tipo *empleado* — anotate su
   DNI.
2. Volvé a **Choferes** → *Nuevo chofer* y usá **ese mismo DNI**.

El sistema tiene que **reutilizar esa persona** en vez de rechazarla o duplicarla, y avisártelo. Andá
a **Personas** y comprobá que **sigue habiendo una sola** con ese DNI.

Ahora intentá cargar un chofer con ese DNI **otra vez**: ahí sí tiene que rechazarlo, porque esa
persona ya es chofer.

### 7. Cargar el catálogo de tipos (User Story 6)

**Tipos de documentación** → cargá tres:

| Nombre | Días de aviso |
|---|---|
| Licencia de conducir | 30 |
| Psicofísico | 15 |
| ART | 0 |

Probá el rechazo del nombre repetido y el de días negativos.

### 8. Cargar documentación y ver el estado calculado (User Story 3, SC-004)

Abrí la ficha del chofer del paso 5 → *Cargar documento*. Cargá **tres**, todos del tipo *Licencia de
conducir* (30 días de aviso):

| Vencimiento | Estado esperado |
|---|---|
| Dentro de 90 días | `Al día` |
| Dentro de 10 días | `Próxima a vencer` |
| Hace 5 días | `Vencida` |

**Nadie eligió esos estados.** Fijate que el formulario **no tiene ningún campo de estado** — no está
oculto ni deshabilitado: no existe (FR-018).

Probá también:

- Un vencimiento **anterior** a la emisión → se rechaza (FR-016).
- Un documento **sin archivo adjunto** → se acepta, y la ficha lo muestra como *Sin respaldo*, no
  igual que uno con archivo (caso límite).
- Un documento con un PDF adjunto → se puede abrir desde la ficha (US4 esc. 5).

**El archivo adjunto** (FR-015a, SC-011):

| Probá esto | Tiene que pasar |
|---|---|
| Adjuntar un `.docx` o un `.zip` | Se rechaza diciendo que tiene que ser PDF, JPG o PNG de hasta 10 MB, y **el documento no se guarda** |
| Adjuntar un archivo de más de 10 MB | Mismo rechazo, mismo mensaje |
| Copiar la dirección del archivo adjunto, cerrar sesión y pegarla en el navegador | **No se ve el archivo**: se pide ingresar. Conocer la dirección no alcanza (FR-024) |

**Los dos bordes que la spec fija**, y que conviene comprobar a mano:

- Un documento que vence **exactamente hoy** → `Próxima a vencer`, **no** `Vencida`.
- Un documento del tipo **ART** (0 días de aviso) que vence dentro de 5 días → `Al día`, sin período
  de aviso intermedio.

### 8b. Corregir y eliminar un documento (User Story 3, FR-015b a FR-015e)

Sobre los documentos del paso 8, desde la ficha del chofer:

| Probá esto | Tiene que pasar |
|---|---|
| *Corregir* el número de un documento | Queda actualizado, con las mismas validaciones del alta |
| Corregir un vencimiento poniéndolo **anterior** a la emisión | Se rechaza igual que en el alta (FR-016) |
| Corregir un documento **sin** elegir archivo nuevo | Conserva el adjunto que ya tenía |
| *Eliminar* un documento | Pide confirmación **advirtiendo que no se puede deshacer**, y al confirmar desaparece de la ficha junto con su archivo |
| Cancelar esa confirmación | No cambia nada (SC-008) |

**La prueba que importa**, y que verifica FR-020a de punta a punta: cargá dos licencias del mismo
chofer, una que vence dentro de 90 días y otra vencida hace 5. El chofer figura `En regla`, porque
manda la de vencimiento más lejano. Ahora **eliminá la que vence dentro de 90 días**: el chofer tiene
que volver a figurar `Vencida`, porque la vieja pasó a ser la vigente. Nadie tocó ningún estado.

Después eliminá también esa, hasta que el chofer se quede sin documentos: tiene que figurar **Sin
documentación**, no *En regla*.

### 9. Un chofer sin documentos no está en regla (FR-028)

Cargá un segundo chofer y no le pongas ningún documento. En el listado tiene que figurar como **Sin
documentación**, que es distinto de *En regla*. Su ficha tiene que decirlo explícitamente.

### 10. Buscar, filtrar y paginar (User Story 4, SC-006)

En el listado de choferes:

- Escribí un fragmento del apellido: coincidencia parcial, sin distinguir mayúsculas.
- Filtrá por *Estado de documentación* = `Vencida`: tiene que quedar sólo el chofer del paso 8.
- Combiná transportista + estado de documentación: se cumplen **todas** las condiciones a la vez.
- Poné un filtro que no coincida con nadie: mensaje explícito de sin resultados.
- Fijate que el filtro *Estado* venga con `Activo` puesto de entrada, a la vista y modificable
  (FR-022).

**La paginación** (FR-030) se ve recién con volumen. Si querés comprobarla, cargá más de 20 choferes
—o dejala para el test automatizado, que la cubre sin trabajo manual—:

| Tiene que pasar |
|---|
| Se ven 20 filas y el total real de coincidencias, no 20 |
| Se puede avanzar de página, y el chofer que estaba en la página 2 no aparece también en la 1 |
| Cambiar un filtro vuelve a la página 1 |

### 11. El panel de vencimientos (User Story 5, SC-005)

Entrá a *Ver vencimientos*. Tienen que aparecer los dos documentos en problemas del paso 8 —el
próximo a vencer y el vencido— con **cuántos días faltan o pasaron**, y no el que está al día.

Desde una fila, llegá a la ficha del chofer.

Ahora **cargá una renovación** de la licencia vencida, con vencimiento dentro de 90 días. Sin borrar
ni editar nada del documento viejo, tiene que pasar todo esto (FR-020a, SC-010):

| Tiene que pasar |
|---|
| El chofer **desaparece del panel** por ese documento |
| El documento anterior **sigue en la ficha**, atenuado y marcado como *Reemplazado* (FR-020) |
| En el listado, el chofer deja de figurar `Vencida` aunque siga teniendo un documento vencido a la vista |

Ese último punto es el que justifica la regla: lo que cuenta es el documento vigente de cada tipo, no
el historial.

### 12. El acceso por rol (FR-027)

Cerrá sesión e ingresá como `mlopez`, el usuario de Tráfico.

| Tiene que pasar |
|---|
| Ve **Choferes**, **Transportistas** y **Tipos de documentación** en el menú |
| **No** ve *Gestión de usuarios* ni *Personas* |
| Si escribe `/usuarios` a mano, recibe el mensaje de falta de permiso |
| Puede registrar un chofer y cargarle documentación sin problemas |

Es la primera vez que el esquema de permisos del Módulo 1 se usa con un rol que no es el
administrador.

### 13. Cambiar los días de aviso recalcula (User Story 6 esc. 4)

Andá a **Tipos de documentación** y cambiá *Licencia de conducir* de 30 a 5 días de aviso.

Volvé a la ficha del chofer: el documento que vencía dentro de 10 días y figuraba como *Próxima a
vencer* ahora tiene que figurar como **Al día**. Nadie ejecutó nada: el estado se recalcula al
consultarlo.

Dejalo de nuevo en 30 para seguir.

### 14. Bajas con dependencias (User Story 7, SC-007)

| Probá esto | Tiene que pasar |
|---|---|
| Dar de baja el tipo *Licencia de conducir*, que tiene documentos | Se rechaza, diciendo **cuántos** documentos lo usan |
| Dar de baja *ART*, que no tiene ninguno | Procede, y deja de ofrecerse al cargar documentos |
| Dar de baja **G&T Logística S.A.**, que tiene choferes activos | Se rechaza, diciendo cuántos. **No recibe trato especial** (caso límite) |
| Dar de baja el transportista terciarizado, sin choferes | Procede |
| Cancelar cualquiera de esas confirmaciones | No cambia nada (FR-026, SC-008) |

### 15. Reasignar sin perder documentación (User Story 7, SC-009)

Editá el chofer del paso 8 y reasignalo al otro transportista. Su documentación tiene que quedar
**íntegra**: los mismos documentos, con los mismos estados.

### 16. La baja saca al chofer de la vista diaria, no del sistema (FR-005, FR-021, FR-022)

Dale de baja al chofer del paso 8, que tiene documentación próxima a vencer.

| Tiene que pasar |
|---|
| **Desaparece del listado** sin filtros, que muestra sólo activos |
| Filtrando *Estado* = `Inactivo`, **vuelve a aparecer**, con su documentación intacta en la ficha |
| **Deja de figurar en el panel de vencimientos**, aunque su documentación siga próxima a vencer: ya no sale a la ruta |
| Su persona **sigue** en el padrón del Módulo 2 |

---

## Tests automatizados

```bash
cd backend && dotnet test    # GT.UnitTests + GT.IntegrationTests
cd frontend && npm test      # Vitest + React Testing Library
```

Los de integración levantan la aplicación contra el SQL Server del compose.

Hay siete escenarios que los tests cubren mejor que el recorrido manual:

- **Los bordes del cálculo de estado**: vencer exactamente hoy, y un tipo con 0 días de aviso. A mano
  dependen de la fecha del día; en un test se fijan.
- **El filtro por estado de documentación**, que se resuelve en la base y no en memoria.
- **La reutilización de persona** bajo DNI duplicado, incluido el rechazo cuando ya es chofer.
- **El dígito verificador de CUIT y CUIL**, con casos válidos e inválidos conocidos.
- **El documento vigente de cada tipo** (FR-020a): con dos documentos del mismo tipo, manda el de
  vencimiento más lejano; con **la misma fecha de vencimiento**, manda el cargado último. El empate a
  mano es casi imposible de armar y es justo el caso que deja el listado inestable si nadie lo
  resuelve.
- **La paginación** (FR-030): que 25 choferes den 20 + 5 con el total en 25, que ninguna fila
  aparezca en dos páginas, y que el orden sea el mismo entre dos consultas iguales.
- **El rechazo de archivos** (FR-015a): tipo no admitido, tamaño excedido, y un archivo con extensión
  `.pdf` que no es un PDF.
- **La atomicidad de la carga** (FR-015e): con el almacenamiento forzado a fallar, que **no** quede
  el documento creado, y que al corregir un documento con un archivo de reemplazo que falla, el
  documento conserve intacto el adjunto anterior. A mano no se puede provocar esa falla; en un test
  sí, sustituyendo el almacén de archivos.

---

## Problemas frecuentes

| Síntoma | Causa y solución |
|---|---|
| El menú no muestra las entradas del módulo | Esa cuenta no tiene el rol *Tráfico* ni *Administrador del sistema*. Es el comportamiento correcto (FR-027) |
| El formulario de chofer no deja completar nada | No hay transportistas **activos**. Cargá uno primero (US2 esc. 4) |
| No se puede elegir un tipo al cargar un documento | El catálogo está vacío o todos los tipos están inactivos |
| Un documento cambió de estado sin que nadie lo tocara | Es lo esperado: el estado se calcula contra la fecha del día (FR-019) |
| No encuentro un chofer que sé que existe | Si lo diste de baja, el listado no lo muestra por defecto. Poné *Estado* = `Inactivo` (FR-022) |
| Un chofer figura `En regla` y le veo un documento vencido en la ficha | Ese documento está *Reemplazado*: hay una renovación posterior del mismo tipo, y es la que cuenta (FR-020a) |
| El archivo adjunto no se abre | Se sirve por un endpoint que exige sesión: si expiró, volvé a ingresar |
