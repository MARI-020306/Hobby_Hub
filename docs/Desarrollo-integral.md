# Desarrollo integral de HobbyHub

Fecha: 2026-10-08. Rama: `rama/Fernando`. Referencia: PDF válido de 15 páginas `Proyecto_HobbyHub/referencias/HobbyHub_Red_Social_15_Modulos_Maquetas.pdf`; se revisaron sus páginas como PNG temporales sin modificar el original.

Se conservaron ASP.NET Core MVC, EF Core, SQL Server, los controladores/vistas anteriores y la seguridad de las fases 1A–1C. La autorización integral sustituyó las pausas entre fases. No hubo commits, push, cambios de rama, despliegues ni conexiones a la base compartida.

## Tres etapas realizadas

1. **Seguridad y persistencia:** creación comprobada de una instancia y base LocalDB nuevas, aplicación manual de scripts versionados, sesiones revocables, recuperación y auditoría conservadas, validación real de imágenes, autorización y pruebas offline/SQL.
2. **Experiencia social:** feed personalizado y de seguidores/membresías; búsqueda; galerías; comentarios y respuestas; likes/guardados; comunidades públicas/privadas; solicitudes, invitaciones y administración de miembros; perfiles, seguidores y preferencias; mensajes y notificaciones persistentes.
3. **Gobierno e integración:** reportes de publicaciones, comentarios, cuentas y mensajes recibidos; moderación con evidencia y suspensión temporal; administración de roles/cuentas/comunidades; actividad/auditoría; bloqueo mutuo; navegación común morada y vistas móviles; pruebas funcionales reales y guía de ejecución.

## Estado de los 15 módulos

Todos los módulos siguientes tienen lógica MVC, formularios con antiforgery y persistencia SQL comprobada. Este estado corresponde al alcance funcional local; las restricciones de correo y producción se detallan más abajo.

| # | Módulo | Implementación y comprobación |
|---|---|---|
| 1 | Autenticación y recuperación | Registro real de Usuario/Creador, BCrypt, login, bloqueo persistente, recuperación con tokens caducables de un uso, cambio de contraseña y logout POST; pruebas SQL/offline y navegador. |
| 2 | Feed | Para ti según intereses/categorías; siguiendo, publicaciones propias y comunidades; orden y paginación, respetando privacidad y bloqueos; pruebas SQL y Edge. |
| 3 | Explorar | Búsqueda de personas, intereses, comunidades y texto de publicaciones con paginación y visibilidad; consultas EF parametrizadas; pruebas SQL y Edge. |
| 4 | Publicaciones/galerías | Texto, hasta seis JPG/PNG de 5 MB, decodificación y reescritura, edición y retirada, imágenes protegidas por permisos; pruebas SQL, archivos inválidos y carga real en Edge. |
| 5 | Interacciones | Comentarios/respuestas, retirar comentario propio, likes y guardados; autorización sobre la publicación, contadores y notificaciones; persistencia SQL y formularios de navegador. |
| 6 | Comunidades | Crear/editar por Creador; ingreso público, solicitud privada, invitación y abandono; presentación de estado real de membresía; pruebas SQL y creación en Edge. |
| 7 | Administración de comunidad | Propietario administra nombre, descripción, categoría, reglas y privacidad; busca personas para invitar, acepta, expulsa y rehabilita miembros; pruebas de permisos y persistencia. |
| 8 | Perfiles/seguidores | Biografía, intereses, foto/portada, perfil público/privado, seguimiento y solicitudes; privacidad aplicada también a rutas antiguas e imágenes; pruebas SQL/offline y vistas de navegador. |
| 9 | Mensajes | Historial exclusivo de participantes, enviar, marcar lectura, preferencias de recepción, bloqueo mutuo y reportes de mensajes recibidos; pruebas con tres usuarios y SQL. |
| 10 | Notificaciones | Publicaciones, likes, comentarios, respuestas, seguidores, membresía/invitaciones, mensajes y resolución de reportes; filtros y lectura del propio destinatario; preferencias persistentes y pruebas SQL. |
| 11 | Reportes | Motivo validado, consulta de reportes propios y resolución; verificación del recurso y prevención de duplicados pendientes; contenido y mensajes privados; pruebas SQL y RBAC. |
| 12 | Moderación | Moderador/Admin revisan estados y evidencia, resuelven, ocultan contenido o suspenden 1–30 días; concurrencia con rowversion, auditoría y notificación; fin de suspensión sin rehabilitar sesiones antiguas. |
| 13 | Administración | Solo Admin: roles/estado de cuentas, privacidad de comunidades y auditoría; revocación ante cambios, prohibición de cambiar privilegios propios y preservación del último administrador activo; pruebas SQL/RBAC y Edge. |
| 14 | Configuración/seguridad | Perfil, privacidad, mensajes/notificaciones, personas bloqueadas, contraseña actual verificada y revocación de sesiones; persistencia y protección antiforgery. |
| 15 | Actividad/auditoría | Acceso a publicaciones propias, likes, guardados y comentarios; sesiones activas y revocación individual; historial propio y auditoría administrativa filtrable; pruebas SQL y pantallas reales. |

## Correcciones y decisiones

- La aplicación no ejecuta CREATE/ALTER, migraciones, EnsureCreated ni SQL de esquema desde solicitudes o arranque. Las pruebas inspeccionan el código y el esquema versionado.
- Development exige exactamente la instancia/base autorizadas; Production se detiene antes de seleccionar una conexión compartida. Los perfiles HTTP/HTTPS y F5 fijan ambos nombres de entorno.
- Cookies HttpOnly/SameSite, revocación por versión de seguridad y revalidación de cuenta/rol/estado; suspensión temporal y lockout conservados; logout exclusivamente POST.
- Se reforzaron rutas antiguas para no filtrar publicaciones privadas: detalle de comunidad, feed clásico, likes/comentarios y reportes. La edición requiere membresía aceptada o propiedad de comunidad.
- La navegación y el estilo comunes se reutilizan también en las vistas anteriores. Se diferenciaron invitaciones, solicitudes pendientes y membresías bloqueadas. El selector de invitaciones usa nombres, sin pedir identificadores técnicos al usuario.
- Las imágenes se guardan con nombres generados fuera del árbol público y se sirven por controlador con permiso. PNG/JPEG se verifican y decodifican; se reescriben eliminando metadatos. Límites: 6.000 píxeles por lado y 20 megapíxeles; PNG entrelazado no admitido. SVG/ejecutables, datos incompletos y tamaños inválidos se rechazan.
- EF parametriza las consultas; Razor codifica el contenido. Pruebas reales verifican HTML almacenado, cadenas semejantes a SQL y cabeceras anti MIME sniffing/clickjacking. Las respuestas autenticadas no se almacenan en caché.
- Las escrituras sociales tienen límite por IP de 60/minuto, separado de login y recuperación. Conflictos de concurrencia/duplicados y errores de persistencia devuelven respuestas sin detalles SQL.
- La auditoría persistente no contiene contraseñas, tokens, cadenas de conexión o contenido de mensajes. Las acciones administrativas se registran antes del acceso y junto a sus cambios de dominio.
- Los bloqueos eliminan seguimientos entre las dos personas, impiden nuevos mensajes y acceso mutuo a perfiles/publicaciones; conservan el historial privado. Desbloquear no restaura seguimientos anteriores.

## Dependencias

SkiaSharp 3.119.1, gratuita con licencia MIT, proporciona un decodificador real de imágenes compatible con el proyecto .NET 8: [paquete oficial](https://www.nuget.org/packages/SkiaSharp/3.119.1), [repositorio oficial](https://github.com/mono/SkiaSharp). Evita aceptar una imagen solo por su extensión/cabecera. Los componentes nativos Windows se restauran como dependencias transitivas.

Playwright 1.58.2 se usa únicamente en `tools/browser`, con Edge instalado, para formularios/capturas/pruebas móviles. No se incorpora a la aplicación ni utiliza un servicio externo. Se conservaron xUnit y Microsoft.AspNetCore.Mvc.Testing compatibles con .NET 8.

## Verificación

- `dotnet restore Proyecto_HobbyHub.sln`: correcto.
- `dotnet build Proyecto_HobbyHub.sln --no-restore`: correcto, cero errores/advertencias.
- `HOBBYHUB_SQL_TESTS=1` + `dotnet test ... --no-build --no-restore`: **168 aprobadas, cero fallidas y cero omitidas**. Incluye 16 pruebas de integración SQL optativas, además de pruebas unitarias, de MVC, permisos, sesiones, recuperación, esquema y archivos.
- `node --test Proyecto_HobbyHub.Tests/auth.test.cjs`: cuatro pruebas aprobadas de validación y comportamiento del formulario.
- `dotnet list ... package --vulnerable --include-transitive --no-restore`: sin vulnerabilidades conocidas reportadas por las fuentes NuGet consultadas en esta fecha; no equivale a una auditoría de penetración.
- Pruebas de Edge: registro/login, creación de comunidad, galería PNG servida desde almacenamiento protegido, comentarios/guardados, logout POST, administración y diseño móvil. 29 capturas locales en `docs/artifacts`, seis comprobaciones móviles sin desbordamiento y recuperación completa desde el buzón cifrado (restablecer, rechazar reutilización e iniciar sesión con contraseña nueva).
- SQL: 18 tablas, cuatro roles y cero claves foráneas deshabilitadas/no confiables. Usuarios y contenido creados por pruebas son exclusivamente sintéticos; las sucesivas ejecuciones agregan registros sin limpiar bases existentes.

Las pruebas SQL usan un factory con destino constante y opt-in explícito; no utilizan credenciales reales ni la configuración compartida. Las pruebas offline interceptan y bloquean cualquier apertura EF. Las de SQL crean sus propias cuentas y recursos por ejecución, con contraseñas aleatorias que no se imprimen.

Para verificar el buzón real local, ejecutar `tools/browser/smoke.cjs`, exportar el identificador escrito en `.local/browser-mail-id.txt` mediante `LocalMailbox:Export` y ejecutar `node tools/browser/recovery.cjs`, con la aplicación aún escuchando en http://127.0.0.1:5087. La prueba lee el archivo sensible local sin imprimirlo; cambia la contraseña, rechaza la reutilización del token e inicia sesión con la contraseña nueva. No envía correo real.

## Pendientes y límites de la entrega local

No hay módulo obligatorio entregado solo como HTML estático. Los pendientes externos son un proveedor de correo real autorizado, configuración/despliegue de producción, cuenta SQL de aplicación con privilegios separados, estrategia de copias/restauración y comprobaciones de carga/penetración del entorno final. No se instalaron esos servicios ni se publicó la aplicación.

Mensajes y notificaciones se actualizan al abrir/recargar la página; no hay transporte en tiempo real. 2FA aparece en el PDF como evolución futura y no se añadió. Las categorías de hobbies reutilizan el catálogo existente. La ejecución y los decodificadores nativos se comprobaron en Windows, no en Linux. El almacenamiento local de imágenes y LocalDB son para desarrollo personal; un despliegue multiusuario necesita diseñar sus permisos, capacidad y respaldo.

Las capturas certifican vistas y flujos revisados con Edge, no todas las combinaciones de dispositivos/lectores de pantalla. La suite funcional no sustituye una certificación para producción. La aplicación permanece bloqueada fuera de Development/Testing hasta preparar un entorno de producción autorizado.

El detalle de archivos está en `Archivos-desarrollo.txt`. Los documentos de las fases 1A–1C conservan el historial de decisiones anteriores; el estado SQL vigente es el documentado en `Proyecto_HobbyHub/Database/README.md`.
