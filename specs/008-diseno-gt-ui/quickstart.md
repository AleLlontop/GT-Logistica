# Quickstart — Adopción del sistema de diseño gt-ui (Módulo 8)

**Feature**: `008-diseno-gt-ui` · **Spec**: [spec.md](./spec.md)

Cómo comprobar que la feature está hecha. Todo se verifica **operando la aplicación** —salvo los dos
puntos que exigen una herramienta de medición, que están señalados—, sin leer código ni consultar la
base, como pide el Principio IV de la constitución.

## Antes de empezar

```bash
podman compose up -d          # SQL Server + backend + frontend
cd frontend && npm test       # 285 pruebas, 43 archivos
```

Se necesitan las tres cuentas de siempre: `admin`, una de Tráfico y una de Gerencia. Y a mano, las
cuatro imágenes de `.claude/skills/gt-ui/references/`: `viajes-listado.png`, `choferes- padron.png`,
`nuevo-chofer.png` y `viaje-detalle.png`.

Resolución mínima: **1280 px de ancho**. Tablet y celular siguen fuera de alcance (FR-072).

---

## Escenario 1 — La aplicación se ve como el sistema de diseño (US1, SC-001)

1. Ingresar como `admin` y abrir cualquier pantalla.
2. **El fondo**: el lienzo gris claro con **dos manchas de luz** —una azulada arriba a la derecha, una
   verdosa abajo a la izquierda—. Desplazar la pantalla: las manchas **no se mueven** y no capturan
   el clic.
3. **La navegación**: una **isla flotante** separada de los bordes de la ventana, con la marca arriba
   y el bloque de usuario al pie. **No hay barra superior.**
4. **Las tarjetas**: bordes suaves, sin ninguna línea gris sólida y sin sombra dura.
5. **La tipografía**: el texto en Plus Jakarta Sans. Un CUIT, un DNI, una patente o un número de
   comprobante en **monoespaciada**.
6. Recorrer las **42 pantallas** de [data-model §4](./data-model.md#4-las-42-pantallas). Ninguna quedó
   con la apariencia anterior.

> **Cómo se verifica la tipografía sin adivinar** (FR-003): con la herramienta de desarrollo del
> navegador, panel *Computed* → *Rendered Fonts*. Tiene que decir *Plus Jakarta Sans* y *Geist Mono*.
> Si dice Segoe UI, la familia propia no cargó y la pila de respaldo la tapó en silencio.

---

## Escenario 2 — La acción principal se distingue (US2, SC-002, SC-003)

1. Abrir `/choferes/nuevo`. Comparar con `nuevo-chofer.png`.
2. Arriba a la izquierda, un ***volver*** discreto: pastilla blanca con la flecha en círculo.
3. Al pie, la barra de acciones: la leyenda de obligatorios a la izquierda, *Cancelar* en gris claro
   y ***Guardar chofer* como pastilla oscura** con el ícono en su círculo, último.
4. **Desplazar el formulario hasta la mitad**: la barra sigue visible, anclada al pie de la ventana.
5. Contar los botones rellenos de la pantalla: **exactamente uno**.
6. Repetir en los **16 formularios** de [data-model §6](./data-model.md#6-los-16-formularios-y-sus-secciones-fr-029).
   En *Registrar cobro* (diálogo) y en `/ingresar` la barra va **al pie de su propio contenedor**, no
   anclada a la ventana: esas dos no se desplazan.
7. Abrir `/facturas/:id` de una factura anulada: **ningún botón relleno** inventado para llenar el
   lugar.
8. Abrir una acción destructiva —*Anular*, *Dar de baja*—: es **lo único rojo** de la pantalla.
9. En las cuatro fichas que tienen destructiva —chofer, vehículo, viaje y factura— hay **dos botones
   rellenos**: la oscura, que es la acción principal, y la roja. **Es correcto y no hay que anotarlo
   como falla**: lo que se cuenta es cuántas acciones se presentan como *la* principal, y sigue
   siendo una (SC-002, FR-017).

**Las secciones numeradas**: en los 9 formularios agrupados, cada grupo lleva su chip con el número y
su título. En los 7 de un solo grupo —ingreso, cambio de contraseña, persona, roles, tipo de vehículo,
asignación, registrar cobro— **no hay chip ni título de sección**.

---

## Escenario 3 — Los listados se leen de un vistazo (US3, SC-004 a SC-008)

Sobre `/viajes`, comparando con `viajes-listado.png`:

1. **El buscador va primero y a todo el ancho**; los filtros debajo, como desplegables compactos que
   muestran su valor actual sin desplegarse.
2. Aplicar un filtro: el control que filtra **se distingue** de los que no, y sigue habiendo un texto
   que declara qué se está mostrando.
3. Debajo, la franja de resumen: cuántos resultados, cuánto suman y por qué criterio están ordenados.
4. La tabla **no tiene rayas alternadas**: sólo divisores muy tenues. Encabezados en mayúsculas
   chiquitas sobre fondo suave.
5. **Ninguna celda tiene un guión**: donde falta el dato dice *Sin asignar* en el tono atenuado.
   Recorrer así las **21 tablas de las 20 pantallas con tabla** —la ficha de factura tiene dos—
   (SC-004).
6. **Los importes**: a la derecha, en negrita, en una línea. Recorrer la columna en vertical: los
   puntos de miles y la coma decimal **quedan alineados entre filas** (SC-005).
7. **Las columnas fusionadas**: *Origen* y *Destino* son ahora **Ruta**; *Chofer* y *Vehículo* son
   **Asignación**. Recorrer las 15 fusiones —repartidas en 12 tablas— de
   [data-model §5](./data-model.md#columnas-que-se-fusionan-fr-047).
8. **Ninguna tabla tiene columna *Acciones*** (SC-007). Verificar las 21.

### Cómo se entra a una fila (SC-006)

9. En `/choferes`: lo clickeable es **el apellido y nombre**, subrayado, con el **DNI en mono debajo**.
   Hay **un solo** enlace por fila.
10. Pasar el mouse por la fila: aparece el **chevron `›`**. El `···` del final **ya estaba visible
    antes de pasar**.
11. Hacer clic en cualquier parte de la fila: abre la ficha.
12. Hacer clic en el `···`: **abre el menú y no navega**.
13. Abrir `/personas`, `/transportistas`, `/tipos-vehiculo`, `/tipos-documentacion`, `/viajes/totales`,
    `/facturas/totales`, el selector de viajes de `/facturas/nueva` y el detalle de una ficha:
    **ninguna fila navega y ninguna lleva chevron**. No tienen ficha adonde ir.

### Sólo con el teclado (SC-008, SC-013)

14. En `/tipos-vehiculo`, sin tocar el mouse: tabular hasta el `···` de una fila.
15. Abrir con `Enter`: el foco pasa al **primer ítem**.
16. Recorrer con las flechas.
17. Apretar `Escape`: el menú cierra y **el foco vuelve al `···`**.
18. Volver a abrir y tabular fuera: el menú cierra solo.
19. Ingresar con una cuenta **sin permiso de gestión** en `/clientes`: las filas se ven, las fichas se
    abren y **el `···` no se dibuja** (FR-045).

---

## Escenario 4 — Una ficha pone adelante el dato que importa (US4)

Sobre `/viajes/:id` de un viaje rendido, comparando con `viaje-detalle.png`:

1. **El encabezado**: el título, el número como token y el estado como pastilla, **en la misma línea**.
   Debajo, el contexto: cliente, ruta, fecha.
2. El estado **no aparece como una fila más** de la lista de datos.
3. **Dos columnas**: principal flexible y aside fijo a la derecha.
4. **El importe** en el aside es el elemento más grande del cuerpo de la pantalla.
5. **El callout**: dice que el viaje está cerrado para edición **y explica cómo revertirlo**.
6. **El historial** se lee como línea de tiempo cronológica con el paso actual marcado, no como una
   tabla de cuatro columnas.
7. Abrir las cinco fichas —`/viajes/:id`, `/facturas/:id`, `/choferes/:id`, `/flota/:id`,
   `/usuarios/:id`—. **Ninguna tiene el aside vacío**: donde no hay importe va el semáforo de
   documentación, el estado del vehículo o los roles
   ([data-model §8](./data-model.md#8-el-dato-de-más-valor-del-aside-fr-053)).

---

## Escenario 5 — Nada de lo que funcionaba dejó de funcionar (US5, SC-011, SC-012)

```bash
cd frontend && npm test
```

1. **285 pruebas, 43 archivos, todo en verde** (SC-011).
2. Revisar el diff de la suite: los únicos cambios son **pasos de interacción agregados** —abrir el
   `···` antes de operar una acción de fila— en los **5 archivos** que enumera
   [research §8](./research.md#8-las-21-líneas-de-la-suite-que-fr-069-autoriza). **Ninguna aserción
   sobre textos, roles ni etiquetas accesibles cambió.** Un archivo tocado fuera de esos cinco es
   señal de que algo se rompió, no de que la lista estaba corta.
3. Recorrer los **seis quickstarts** de los Módulos 1 a 6 con las tres cuentas. Cada operación hace lo
   mismo: los mismos mensajes, las mismas confirmaciones, los mismos rechazos, los mismos permisos
   (SC-012).
4. Las **42 direcciones** siguen siendo las mismas (FR-067) y el **título de la pestaña** sigue
   nombrando la pantalla y el sistema (FR-071).

---

## Escenario 6 — Contraste, color y movimiento (SC-009, SC-010, SC-014, SC-015)

1. **Contraste** (SC-009) — *requiere herramienta de medición, no se hace a ojo*. Con el auditor de
   contraste de la herramienta de desarrollo, verificar texto ≥ 4,5:1 y no textual que comunica
   ≥ 3:1. Prestar atención a los cuatro valores recalibrados y a los fondos sobre los que se los
   midió, en [data-model §1.1](./data-model.md#11-neutros).
2. Abrir `.claude/skills/gt-ui/references/tokens.md`: **los cuatro valores nuevos figuran ahí, con su
   medición al lado** (FR-008). Y `SKILL.md` lista `choferes- padron.png` entre sus referencias.
3. **Ningún texto sobre el lienzo** salvo el título de página y su bajada (research §1).
4. **Errores** (SC-010): abrir los 16 formularios. **Ninguno muestra un mensaje de error en su primer
   dibujo.** Enviar uno vacío: los errores aparecen igual que antes, con los mismos textos.
5. **Escala de grises** (SC-014): con un filtro de escala de grises sobre `/viajes`, los estados de
   las filas **se siguen distinguiendo** — cada uno lleva su palabra además del color.
6. **Menos movimiento** (SC-015): activar *Reducir movimiento* en el sistema operativo y recorrer el
   sistema. **Nada anima.**
7. **Emoji**: no hay ninguno en ninguna pantalla (FR-063).

---

## Los bordes que hay que probar a propósito

| Caso | Cómo se provoca | Qué tiene que pasar |
|---|---|---|
| Menú vacío | Cuenta cuyos roles no habilitan ninguna opción | La isla de navegación **no se dibuja**; la pantalla de inicio se usa igual |
| Menú largo | Cuenta con más de quince entradas | El ítem activo pasa a **fondo suave con texto de acento**, no relleno pleno |
| Pantalla sin navegación | `/ingresar` y el cambio de contraseña forzado | Mismo lienzo, misma tipografía, mismo vocabulario, **sin isla de navegación** |
| Estado desconocido | Un valor que el sistema de diseño no nombra | Se dibuja en **tono neutro**, con su palabra, sin romper la pantalla |
| Fila sin acciones | Cuenta sin permiso de escritura | El `···` de esa fila **no se dibuja** |
| Tabla que no entra | `/viajes` a 1280 px | **Se desplaza la tabla dentro de su isla**, nunca la pantalla |
| Fila atenuada | Un viaje o una factura anulados | Sigue llevando **la palabra** que lo explica, y el tono sigue siendo legible |
| Clic en un control de la fila | La casilla del selector de viajes, *Abrir archivo* de una ficha | Opera ese control y **no navega** |
