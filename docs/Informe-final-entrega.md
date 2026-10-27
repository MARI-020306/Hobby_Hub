# HobbyHub: revisión final de entrega

Fecha: 8 de octubre de 2026. Rama: `rama/Fernando`. Esta revisión prepara una versión local revisable; no certifica un despliegue de producción. No se realizaron commits, push, merge ni despliegues.

## Alcance y aislamiento

Se mantuvo el diseño aprobado y la arquitectura ASP.NET Core MVC, EF Core y SQL Server. Se utilizó exclusivamente la instancia `(localdb)\HobbyHubPhase1C_20261008`, base **`HobbyHub_Development_Visual_20261008_6c76fa09`**, con usuarios y contenido sintéticos. No se accedió a SQL compartido, ni a la base de desarrollo original. No se borraron datos existentes, se ejecutaron migraciones ni se alteró el esquema durante esta revisión. Las pruebas agregan registros sintéticos nuevos y conservan los anteriores.

El servidor de revisión está en `http://127.0.0.1:5088`, con cookies, claves y compilación separados. El servidor del usuario en 5062 no fue detenido ni reemplazado. Los archivos de configuración local y cuentas de pruebas permanecen en `.local`, excluidos de Git.

## Estado de los 15 módulos

Todos disponen de recorridos implementados y verificados en el entorno local. La comprobación combina pruebas .NET de seguridad e integración SQL, pruebas JavaScript y recorridos de navegador; no implica cubrir cada combinación posible ni requisitos de producción.

| Módulo | Funcionamiento comprobado | Límite o alcance |
| --- | --- | --- |
| 1. Autenticación y recuperación | Registro, login, logout POST, BCrypt, bloqueo por intentos, tokens caducables de un uso y revocación | Recuperación mediante buzón local cifrado; sin proveedor de correo real |
| 2. Inicio y feed | Publicaciones visibles según privacidad y membresía, navegación y paginación | Actualización al navegar o recargar |
| 3. Explorar | Búsqueda de personas, hobbies y comunidades; acceso y estado de membresía | Resultados acotados; sin buscador externo |
| 4. Publicaciones | Creación con comunidad válida, imágenes revalidadas y galerías | Hasta seis imágenes de 5 MB cada una; almacenamiento local privado |
| 5. Interacciones | Comentarios, respuestas, likes y guardados persistentes | Sujetos a visibilidad y permisos del contenido |
| 6. Comunidades | Creación, edición, unión pública, solicitud privada, aprobación y abandono | No se borran comunidades de prueba al finalizar |
| 7. Gestión de comunidades | Propiedad, administración de solicitudes y miembros; bloqueo de acceso ajeno | El rol Creador no otorga propiedad sobre comunidades ajenas |
| 8. Perfiles y seguidores | Edición, perfiles públicos/privados, seguimiento, aprobación y bloqueo | Privacidad aplicada también a publicaciones e imágenes |
| 9. Mensajes | Envío, lectura, bloqueo, reportes y acceso exclusivo de participantes | Sin tiempo real ni cifrado de extremo a extremo |
| 10. Notificaciones | Generación por actividad y marcado de lectura | Sin push ni correo externo |
| 11. Reportes | Reportar contenido, consultar reportes propios y estado | Revisión humana por roles autorizados |
| 12. Moderación | Consulta de reportes, resolución, ocultación y suspensión con auditoría | No sustituye una política operativa de moderación |
| 13. Administración | Cuentas, roles, estados, comunidades y auditoría; revocación de sesiones | Solo Administrador; cambios sintéticos durante las pruebas |
| 14. Configuración y seguridad | Privacidad, cambio de contraseña, bloqueo y sesiones revocables | Sin MFA ni eliminación integral de cuenta |
| 15. Actividad y auditoría | Actividad propia, sesiones e historial administrativo filtrable | Protección de auditoría en aplicación; permisos SQL de producción pendientes |

## Correcciones de esta revisión

- Paginación SQL de comunidades: veinte resultados por página, consultas proyectadas y orden estable. La edición permite recuperar una comunidad fuera de la página actual. Moderación dispone de navegación entre páginas.
- Las comunidades privadas sin membresía llevan al flujo de solicitud, evitando enlaces que terminaban en acceso denegado. Explorar refleja estados de creador, miembro, solicitud pendiente y bloqueo.
- Errores 400/403/404/405/413/429 y 500 presentan mensajes útiles, conservan el código HTTP y no muestran trazas internas. La política de privacidad describe honestamente el prototipo y sus límites.
- Auditoría valida filtros y rangos de fechas, incluso la fecha máxima; moderación rechaza estados desconocidos.
- Se asociaron etiquetas con campos de formularios administrativos, mensajes, perfiles y comentarios. Las galerías desplazables admiten foco por teclado sin cambiar su apariencia.
- Se reemplazó jQuery Validation 1.19.5 por **1.22.1**, corrigiendo [CVE-2025-3573 / GHSA-rrj2-ph5q-jxw2](https://github.com/advisories/GHSA-rrj2-ph5q-jxw2). Los mensajes de validación se tratan como texto mediante `escapeHtml`; existe una comprobación de navegador contra HTML inyectado.
- Se endureció `.gitignore`, se agregó un verificador de archivos candidatos a Git y documentación de ejecución, demostración y entrega. El helper de navegador respeta `Retry-After` y utiliza la ruta real del formulario de login; no debilita la protección contra intentos.

## Validación

- `dotnet restore`: correcto, con auditoría NuGet de dependencias directas y transitivas.
- `dotnet build`: correcto, cero errores y cero advertencias, con salida aislada en `.local/visual-build`.
- `dotnet test`: **172 aprobadas, cero fallos y cero omitidas**: 152 sin SQL y 20 de integración SQL sobre la base de pruebas indicada.
- `node --test`: **9 aprobadas**, cero fallos y cero omitidas; validación del login, estados de carga y menú accesible.
- Playwright utiliza Microsoft Edge real en modo headless. La suite funcional cubre registro, comunidades públicas/privadas, galerías, interacciones, perfiles, seguidores, mensajes, notificaciones, reportes, moderación, administración, revocación y persistencia después de logout y nuevo contexto.
- La revisión adicional recorrió **79 páginas con los cuatro roles**, comprobó **662 enlaces, 882 formularios y 882 botones**, y completó **12 verificaciones de permisos y seguridad**, incluyendo antiforgery y validación frente a HTML inyectado. No hubo enlaces rotos ni errores de JavaScript. Los totales finales y resultados de accesibilidad se registran en `.local/final-review/browser-review.json`, no se publican datos de las cuentas.
- La suite completó **22 recorridos principales, 5 comprobaciones adicionales de privacidad/teclado y 245 comprobaciones responsive** (189 autenticadas principales, 28 públicas y 28 secundarias). Generó **77 capturas posteriores**. Los anchos de prueba son **320, 375, 390, 768, 1024, 1366 y 1920 px**. Las capturas y resultados regenerables permanecen en `docs/visual`, excluidos de Git; consulte también `Unificacion-visual.md`.
- Rendimiento medido: tiempo hasta el primer byte de HTML en entorno local. La ejecución final de 79 páginas obtuvo p50 **9 ms**, p95 **32 ms** y máximo **53 ms**. No es una prueba de carga, de red móvil ni de rendimiento de producción.
- Accesibilidad: axe-core sobre una selección de pantallas y roles con reglas WCAG 2 A/AA y 2.1 AA, más comprobaciones de teclado/ARIA en móvil. El hallazgo inicial de galerías sin foco se corrigió; la repetición terminó con **cero hallazgos** en las 24 evaluaciones realizadas. Estas pruebas no certifican conformidad WCAG integral; se recomienda revisión manual con lector de pantalla.

## Dependencias y archivos para Git

Las auditorías NuGet y npm no reportaron vulnerabilidades conocidas después de la actualización del validador. También se revisaron las bibliotecas distribuidas directamente en `wwwroot/lib`, que no aparecen en la auditoría del paquete de herramientas de navegador. Los resultados describen los avisos disponibles en la fecha de revisión; no garantizan ausencia de vulnerabilidades nuevas.

El verificador `tools/Verify-RepositorySafety.py` revisa archivos rastreados y archivos nuevos no ignorados, detecta configuraciones privadas, patrones de secretos y archivos excluidos que ya estuvieran rastreados. El resultado final revisó **251 archivos candidatos: 249 de texto y dos recursos binarios, sin hallazgos**. El detalle está en `.local/final-review/repository-safety.json`. Los dos recursos binarios candidatos son el PDF válido de quince páginas y el favicon existente; se revisaron su formato y metadatos. Las comprobaciones automáticas no sustituyen una revisión humana de contenido antes de publicar.

Se recomienda registrar los archivos de código, pruebas, scripts revisables, dependencias con sus licencias, recursos estáticos y documentación enumerados en `Archivos-entrega.txt`. **No se ejecutó `git add`**. Deben permanecer excluidos `.local`, cuentas sintéticas, claves, buzón, imágenes subidas, configuraciones locales, bases/respaldos, `bin`, `obj`, `node_modules`, logs, capturas y resultados temporales. La configuración versionable contiene una conexión predeterminada vacía y una plantilla de LocalDB; no incluye credenciales.

## Recomendaciones antes de producción

1. Actualizar a una versión .NET con soporte suficiente y repetir pruebas. [.NET 8 termina su soporte el 10 de noviembre de 2026](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
2. Aprovisionar SQL Server autorizado con usuario de aplicación de privilegios mínimos, sin DDL y con protección SQL de auditoría; planificar respaldo, restauración, retención e índices bajo carga real.
3. Configurar HTTPS, cookies y claves protegidas y persistentes, secretos fuera del repositorio, monitoreo y estrategia de almacenamiento de imágenes. El HTTP de 5088 es exclusivamente una revisión local.
4. Autorizar y configurar un proveedor de correo antes de usar recuperación con usuarios reales. Revisar MFA, gestión de cuentas y privacidad/retención conforme al contexto de despliegue.
5. Ejecutar pruebas de carga, accesibilidad manual, análisis de seguridad independiente y controles operativos de moderación. La aplicación no se presenta como lista para producción.

La instalación segura y los comandos están en el README raíz. La guía `Guia-exposicion.md` propone una demostración universitaria con datos sintéticos y separación de los cuatro roles.
