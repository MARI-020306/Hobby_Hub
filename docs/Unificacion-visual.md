# HobbyHub — unificación visual y verificación

Trabajo local en `rama/Fernando`, 8 de octubre de 2026. No se hicieron commits ni push. No se utilizó la base compartida, ni se modificaron los datos de desarrollo anteriores. El servidor del usuario en 5062 y sus archivos binarios no se tocaron.

## Diseño y pantallas

El exterior de Comunidades se reutiliza en `_SocialLayout`: menú blanco de 248 px en escritorio/tablet, logotipo HobbyHub, encabezado común, tipografía Poppins, botones morados, bordes y espaciados compartidos. Se eliminaron los menús repetidos de Comunidades, Perfil y el inicio antiguo, así como las reglas del antiguo exterior morado de Social.

Las vistas autenticadas heredan esta estructura; el contenido de cada módulo conserva sus controladores, modelos, permisos y formularios. Login, registro y recuperación mantienen su composición pública, con los mismos colores y tipografía.

Pantallas verificadas en los siete anchos:

- Social: Inicio, Explorar, Nueva, Publicacion, Persona, Mensajes (lista y conversación), Notificaciones, Reportes, Seguidores, Guardados, Ajustes, Actividad, Reacciones, MisComentarios y Bloqueados.
- Comunidades: Index, Detalle y Administrar.
- Perfil: Index y Editar; Home/Index como entrada antigua.
- Cuenta: Seguridad; formularios públicos Recuperar y Restablecer.
- Administración: Index, Auditoria y Comunidades; Moderacion/Index.
- Autenticación pública: Login y Registro.

Los formularios de creación y edición de Comunidades, Moderacion/Detalle y AccesoDenegado también se comprobaron en los siete anchos. AccesoDenegado conserva su respuesta HTTP 403 y utiliza el mismo layout compartido.

## Responsive y accesibilidad

Anchos comprobados: **320, 375, 390, 768, 1024, 1366 y 1920 px**. No hubo desbordamiento horizontal en las 35 pantallas/variantes revisadas.

- Desde 768 px: menú lateral de ancho constante, sin desplazamiento interno ni altura máxima; contenido flexible.
- Hasta 767 px: botón desplegable con `aria-controls`, `aria-expanded`, etiquetas de apertura/cierre, soporte nativo de Enter/Espacio, Escape con retorno de foco y cierre al navegar o cambiar de tamaño. Sin JavaScript, la navegación permanece visible.
- Tarjetas y formularios se adaptan a una columna; textos largos se ajustan al ancho disponible. Campos y botones comunes tienen un mínimo de 44 px.
- Feed limitado a 660 px; fotografías proporcionadas, sin recorte ni deformación, con altura máxima de 360 px (280 px en móvil).
- Auditoría convertida en filas con etiquetas visibles en móvil, conservando el marcado de tabla y sus encabezados accesibles.
- Envíos válidos muestran un estado de carga; los formularios inválidos no quedan bloqueados. Se mantiene la validación existente y la protección antiforgery.

## Archivos de esta mejora

Modificados: `Proyecto_HobbyHub/Program.cs`; `Controllers/AdministracionController.cs`; `Views/_ViewStart.cshtml`; `Views/Shared/_SocialLayout.cshtml`, `_SocialNav.cshtml`, `_AccountLayout.cshtml`, `_Layout.cshtml`, `_AuditTable.cshtml`; `Views/Auth/Login.cshtml`, `Registro.cshtml`, `AccesoDenegado.cshtml`; `Views/Comunidades/Index.cshtml`, `Detalle.cshtml`; `Views/Perfil/Index.cshtml`, `Editar.cshtml`; `Views/Home/Index.cshtml`; `wwwroot/css/social.css`; `Proyecto_HobbyHub.Tests/SqlApplicationFactory.cs`, `SqlSocialIntegrationTests.cs`.

Nuevos: `wwwroot/css/design-tokens.css`, `app-layout.css`; `wwwroot/js/navigation.js`; `Proyecto_HobbyHub.Tests/navigation.test.cjs`, `VisualTestSeed.cs`; `tools/Prepare-VisualTestDatabase.ps1`, `Start-VisualReview.ps1`; `tools/browser/capture-before.cjs`, `visual-functional.cjs`, `visual-public.cjs`, `visual-accessibility.cjs`, `visual-secondary.cjs`, `visual-suite.cjs`; este informe y los artefactos de `docs/visual`.

Las modificaciones de las fases anteriores siguen presentes en el árbol de trabajo; este listado describe específicamente la unificación visual.

## Base y ejecución aisladas

Instancia: `(localdb)\HobbyHubPhase1C_20261008`. Nueva base: **HobbyHub_Development_Visual_20261008_6c76fa09**. Solo contiene datos sintéticos. La base de desarrollo original no se reutilizó.

El aprovisionamiento comprobó que el nombre nuevo no existía y aplicó, en orden, 001, 002, 003, 005, 006, 007, 008 y 009, usando copias temporales con guardas específicas de la nueva base. Los scripts originales no se modificaron. No se borraron ni sobrescribieron bases. La configuración y los usuarios de pruebas se almacenan en `.local`, ignorado por Git. La cookie visual tiene nombre propio.

La compilación usa `.local/visual-build` para evitar escribir sobre los binarios del servidor 5062. **Detener únicamente el servidor 5088 antes de recompilar**, porque Windows bloquea los DLL en uso.

Desde la terminal PowerShell de VS Code, en la raíz:

```powershell
dotnet restore Proyecto_HobbyHub.sln --artifacts-path .local/visual-build
dotnet build Proyecto_HobbyHub.sln --artifacts-path .local/visual-build --no-restore
./tools/Start-VisualReview.ps1
```

Abrir **http://127.0.0.1:5088**. Se puede registrar una cuenta sintética desde la aplicación. Las credenciales de los roles privilegiados de pruebas están solo en el archivo local ignorado `.local/visual-privileged-accounts.json`; no se incluyen aquí.

## Pruebas y evidencia

- `dotnet restore`: correcto.
- `dotnet build`: correcto, cero errores y cero advertencias en la compilación final.
- `dotnet test`: **170 aprobadas, ninguna fallida ni omitida**, incluyendo 18 pruebas SQL sobre la nueva base.
- JavaScript: **9 aprobadas**, validación del login y navegación/estado de envío.
- Edge/Playwright: **22 flujos funcionales**, **217 comprobaciones responsive autenticadas**, **28 públicas**; 77 capturas posteriores y 9 anteriores.
- Los flujos cubren registro/login/logout, creación/edición de comunidades, unión/abandono/solicitud/aprobación, galerías, comentarios, likes, guardados, seguidores, privacidad, mensajes/leídos, notificaciones, reportes/resolución, roles/revocación, denegación de permisos, auditoría y persistencia al volver a iniciar sesión.
- La prueba adicional superó **5 comprobaciones**: solicitud/aprobación de seguidores privados, respuestas a comentarios y navegación móvil con teclado.

Repetir tests SQL únicamente sobre la base visual:

```powershell
$env:HOBBYHUB_SQL_TESTS='1'
$env:HOBBYHUB_VISUAL_TEST_DATABASE='HobbyHub_Development_Visual_20261008_6c76fa09'
dotnet test Proyecto_HobbyHub.sln --artifacts-path .local/visual-build --no-build --no-restore
node --test Proyecto_HobbyHub.Tests/navigation.test.cjs Proyecto_HobbyHub.Tests/auth.test.cjs
node tools/browser/visual-suite.cjs
```

La suite de navegador exige el servidor de revisión en 5088 y las cuentas sintéticas preparadas en la base visual. No ejecutar los scripts históricos `smoke.cjs`/`recovery.cjs` para esta revisión: pertenecen al entorno anterior.

Comparación: visor local `docs/visual/comparacion.html`. Resultados: `docs/visual/results.json`. Capturas en `docs/visual/before` y `docs/visual/after`. Las capturas anteriores muestran el estado inicial; las posteriores incluyen el contenido sintético generado por las pruebas, por lo que la comparación evalúa composición y navegación, no igualdad de contenido.

## Problema funcional corregido

El parámetro de filtro `action` de Auditoria estaba capturando el valor de la ruta MVC (`Auditoria`), por lo que el listado inicial quedaba vacío. Ahora el filtro se obtiene exclusivamente de la query mediante `[FromQuery]`. Se agregaron pruebas SQL y de navegador para comprobar el listado real y el filtro; el estado vacío también muestra un mensaje comprensible.

Las capturas y resultados de ejecución permanecen disponibles localmente, pero se excluyen de Git para la entrega. Se regeneran con las herramientas de navegador; no se esperan estos archivos en un clon limpio.

## Límites

La verificación se realizó en Edge con usuarios y datos sintéticos de SQL Server LocalDB. No sustituye pruebas de carga, revisión exhaustiva de accesibilidad, otros navegadores ni validación de despliegue. No se publica el proyecto ni se declara listo para producción. No hay funciones pendientes identificadas dentro de esta unificación visual en los recorridos comprobados.
