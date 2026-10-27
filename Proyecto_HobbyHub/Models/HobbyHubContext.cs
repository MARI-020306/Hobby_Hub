using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Proyecto_HobbyHub.Models;

public partial class HobbyHubContext : DbContext
{
    public HobbyHubContext()
    {
    }

    private readonly IHttpContextAccessor? _http;
    private readonly TimeProvider _time = TimeProvider.System;
    public HobbyHubContext(DbContextOptions<HobbyHubContext> options, IHttpContextAccessor? http = null, TimeProvider? time = null)
        : base(options)
    {
        _http = http;
        _time = time ?? TimeProvider.System;
    }

    public virtual DbSet<Comentario> Comentarios { get; set; }

    public virtual DbSet<Comunidade> Comunidades { get; set; }

    public virtual DbSet<Publicacione> Publicaciones { get; set; }

    public virtual DbSet<PublicacionLike> PublicacionLikes { get; set; }

    public virtual DbSet<Reporte> Reportes { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    public virtual DbSet<UsuarioImagen> UsuarioImagenes { get; set; }

    public virtual DbSet<MiembroComunidad> MiembrosComunidad { get; set; }
    public virtual DbSet<SesionUsuario> SesionesUsuario { get; set; }
    public virtual DbSet<RecuperacionCuenta> RecuperacionesCuenta { get; set; }
    public virtual DbSet<AuditoriaSeguridad> AuditoriasSeguridad { get; set; }

    private readonly Dictionary<AuditoriaSeguridad, Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry> auditResources = new();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RotateSecurityVersions();
        AddContentAudit();
        var audits = PendingAudits();
        using var transaction = Database.CurrentTransaction is null && auditResources.Count > 0 ? Database.BeginTransaction() : null;
        int result = base.SaveChanges(acceptAllChangesOnSuccess);
        if (CompleteResourceKeys()) base.SaveChanges(acceptAllChangesOnSuccess);
        transaction?.Commit();
        auditResources.Clear();
        LogAudits(audits, transaction is null && Database.CurrentTransaction is not null);
        return result;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RotateSecurityVersions();
        AddContentAudit();
        var audits = PendingAudits();
        await using var transaction = Database.CurrentTransaction is null && auditResources.Count > 0
            ? await Database.BeginTransactionAsync(cancellationToken) : null;
        int result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        if (CompleteResourceKeys()) await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        auditResources.Clear();
        LogAudits(audits, transaction is null && Database.CurrentTransaction is not null);
        return result;
    }

    private bool CompleteResourceKeys()
    {
        bool changed = false;
        foreach (var pair in auditResources)
            if (pair.Key.IdRecurso is null && ResourceKey(pair.Value) is { } key)
            {
                // Internal completion of generated IDs inside the original transaction.
                pair.Key.IdRecurso = key;
                changed = true;
            }
        return changed;
    }

    private List<AuditoriaSeguridad> PendingAudits()
    {
        var audits = ChangeTracker.Entries<AuditoriaSeguridad>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList();
        var http = _http?.HttpContext;
        foreach (var audit in audits)
        {
            audit.FechaUtc = _time.GetUtcNow().UtcDateTime;
            audit.CorrelacionId ??= http?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
            if (audit.TipoRecurso is null)
            {
                bool accountEvent = audit.Accion.StartsWith("Login", StringComparison.Ordinal) || audit.Accion.StartsWith("Logout", StringComparison.Ordinal) ||
                    audit.Accion.StartsWith("Password", StringComparison.Ordinal) || audit.Accion.StartsWith("Recovery", StringComparison.Ordinal) ||
                    audit.Accion.StartsWith("Account", StringComparison.Ordinal) || audit.Accion.StartsWith("Session", StringComparison.Ordinal);
                audit.TipoRecurso = accountEvent ? "Account" : http?.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>()?.ControllerName ?? "Account";
                if (accountEvent) audit.IdRecurso ??= audit.IdObjetivo?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            if (audit.TipoRecurso is "Publication" or "Comment" or "Community" or "Report" or "Membership" or "Message" or "ProfileImage" or "Account" or "Profile") continue;
            audit.IdRecurso ??= http?.Request.RouteValues.Where(v => v.Key.StartsWith("id", StringComparison.OrdinalIgnoreCase))
                .Select(v => long.TryParse(v.Value?.ToString(), out var n) ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : null).FirstOrDefault(v => v != null)
                ?? audit.IdObjetivo?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return audits;
    }

    private void LogAudits(List<AuditoriaSeguridad> audits, bool pendingTransaction)
    {
        var http = _http?.HttpContext;
        var log = http?.RequestServices?.GetService<Proyecto_HobbyHub.Security.RequestSecurityLog>();
        if (http is null || log is null) return;
        foreach (var audit in audits) log.Persisted(http, audit, pendingTransaction);
    }

    private void AddContentAudit()
    {
        int? actor = int.TryParse(_http?.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
        {
            (string? kind, int? owner) = entry.Entity switch
            {
                Publicacione p => ("Publication", p.IdUsuario),
                Comentario c => ("Comment", c.IdUsuario),
                Comunidade c => ("Community", c.IdCreador),
                Reporte r => ("Report", r.IdUsuarioReporta),
                MiembroComunidad m => ("Membership", m.IdUsuario),
                MensajePrivado m => ("Message", m.IdRemitente),
                UsuarioImagen image => ("ProfileImage", image.IdUsuario),
                Usuario user when entry.State == EntityState.Added => ("Account", (int?)null),
                Usuario user when entry.State == EntityState.Modified &&
                    new[] { nameof(Usuario.Nombre), nameof(Usuario.Biografia), nameof(Usuario.Intereses), nameof(Usuario.Privado),
                        nameof(Usuario.PermitirMensajes), nameof(Usuario.RecibirNotificaciones), nameof(Usuario.Celular), nameof(Usuario.Direccion) }
                        .Any(name => entry.Property(name).IsModified) => ("Profile", user.IdUsuario),
                _ => ((string?)null, (int?)null)
            };
            if (kind is null) continue;
            string action = kind + (entry.State == EntityState.Added ? "Created" : entry.State == EntityState.Deleted ? "Deleted" : "Changed");
            // Reuse the existing user target semantics; resource IDs are not user IDs.
            if (!ChangeTracker.Entries<AuditoriaSeguridad>().Any(a => a.State == EntityState.Added &&
                a.Entity.Accion == action && a.Entity.IdObjetivo == owner && a.Entity.TipoRecurso == kind &&
                a.Entity.IdRecurso == ResourceKey(entry) && (a.Entity.IdRecurso != null ||
                    auditResources.TryGetValue(a.Entity, out var source) && ReferenceEquals(source.Entity, entry.Entity))))
            {
                var audit = new AuditoriaSeguridad { Accion = action, IdActor = actor, IdObjetivo = owner,
                    FechaUtc = _time.GetUtcNow().UtcDateTime, Exito = true, TipoRecurso = kind, IdRecurso = ResourceKey(entry) };
                AuditoriasSeguridad.Add(audit);
                if (audit.IdRecurso is null) auditResources[audit] = entry;
            }
        }
        // Audit and domain changes share SaveChanges and any enclosing transaction.
    }

    private static string? ResourceKey(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var keys = entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).ToArray();
        // Identity values do not exist until SQL saves the new row. Correlation links
        // its creation event; never confuse an owner's ID with a resource ID.
        if (keys.Length == 0 || keys.Any(p => p.IsTemporary)) return null;
        return string.Join(":", keys.Select(p => Convert.ToString(p.CurrentValue, System.Globalization.CultureInfo.InvariantCulture)));
    }

    private void RotateSecurityVersions()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<AuditoriaSeguridad>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Los registros de auditoría no pueden editarse ni eliminarse desde la aplicación.");
        foreach (var entry in ChangeTracker.Entries<Usuario>().Where(e => e.State == EntityState.Modified).ToArray())
        {
            if (new[] { nameof(Usuario.Password), nameof(Usuario.RolId), nameof(Usuario.Estado) }
                .Any(name => entry.Property(name).IsModified &&
                    !Equals(entry.Property(name).OriginalValue, entry.Property(name).CurrentValue)))
                entry.Entity.VersionSeguridad = Guid.NewGuid();
            foreach (var property in new[] { nameof(Usuario.RolId), nameof(Usuario.Estado), nameof(Usuario.Password) })
                if (entry.Property(property).IsModified && !Equals(entry.Property(property).OriginalValue, entry.Property(property).CurrentValue))
                {
                    int? actor = int.TryParse(_http?.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;
                    string action = property switch { nameof(Usuario.RolId) => "AccountRoleChanged", nameof(Usuario.Estado) => "AccountStateChanged", _ => "AccountPasswordChanged" };
                    if (!ChangeTracker.Entries<AuditoriaSeguridad>().Any(a => a.State == EntityState.Added &&
                        a.Entity.Accion == action && a.Entity.IdObjetivo == entry.Entity.IdUsuario))
                        AuditoriasSeguridad.Add(new() { Accion = action, IdActor = actor, IdObjetivo = entry.Entity.IdUsuario,
                            Exito = true, FechaUtc = _time.GetUtcNow().UtcDateTime });
                }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Comentario>(entity =>
        {
            entity.HasKey(e => e.IdComentario).HasName("PK__Comentar__DDBEFBF92880005E");

            entity.Property(e => e.Contenido)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Visible");
            entity.Property(e => e.FechaComentario)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.IdPublicacionNavigation).WithMany(p => p.Comentarios)
                .HasForeignKey(d => d.IdPublicacion)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Comentarios_Publicacion");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.Comentarios)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Comentarios_Usuario");
        });

        modelBuilder.Entity<Comunidade>(entity =>
        {
            entity.HasKey(e => e.IdComunidad).HasName("PK__Comunida__04AF00582957B830");

            entity.HasIndex(e => e.Nombre, "UQ__Comunida__75E3EFCF05C52C5A").IsUnique();

            entity.Property(e => e.Descripcion)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Categoria)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ImagenPortada)
                .HasMaxLength(500)
                .IsUnicode(false);

            entity.HasOne(d => d.IdCreadorNavigation).WithMany(p => p.Comunidades)
                .HasForeignKey(d => d.IdCreador)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Comunidades_Creador");
        });

        modelBuilder.Entity<MiembroComunidad>(entity =>
        {
            entity.ToTable("MiembrosComunidad");

            entity.HasKey(e => new { e.IdComunidad, e.IdUsuario });

            entity.Property(e => e.FechaUnion)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Comunidad).WithMany(p => p.Miembros)
                .HasForeignKey(d => d.IdComunidad)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_MiembrosComunidad_Comunidades");

            entity.HasOne(d => d.Usuario).WithMany(p => p.MembresiasComunidad)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_MiembrosComunidad_Usuarios");
        });

        modelBuilder.Entity<Publicacione>(entity =>
        {
            entity.HasKey(e => e.IdPublicacion).HasName("PK__Publicac__24F1B7D3B329D7A3");

            entity.Property(e => e.Contenido)
                .HasMaxLength(2000)
                .IsUnicode(false);
            entity.Property(e => e.ImagenUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Visible");
            entity.Property(e => e.FechaPublicacion)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.IdComunidadNavigation).WithMany(p => p.Publicaciones)
                .HasForeignKey(d => d.IdComunidad)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Publicaciones_Comunidad");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.Publicaciones)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Publicaciones_Usuario");
        });

        modelBuilder.Entity<PublicacionLike>(entity =>
        {
            entity.HasKey(e => e.IdLike);

            entity.HasIndex(e => new { e.IdPublicacion, e.IdUsuario }, "UQ_PublicacionLikes_Publicacion_Usuario")
                .IsUnique();

            entity.Property(e => e.FechaLike)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.IdPublicacionNavigation).WithMany(p => p.PublicacionLikes)
                .HasForeignKey(d => d.IdPublicacion)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PublicacionLikes_Publicaciones");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.PublicacionLikes)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PublicacionLikes_Usuarios");
        });


        modelBuilder.Entity<Reporte>(entity =>
        {
            entity.HasKey(e => e.IdReporte).HasName("PK__Reportes__F9561136B28F79EC");
            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Pendiente");
            entity.Property(e => e.FechaReporte)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Motivo)
                .HasMaxLength(255)
                .IsUnicode(false);

            entity.HasOne(d => d.IdComentarioNavigation).WithMany(p => p.Reportes)
                .HasForeignKey(d => d.IdComentario)
                .HasConstraintName("FK_Reportes_Comentario");

            entity.HasOne(d => d.IdPublicacionNavigation).WithMany(p => p.Reportes)
                .HasForeignKey(d => d.IdPublicacion)
                .HasConstraintName("FK_Reportes_Publicacion");

            entity.HasOne(d => d.IdUsuarioReportaNavigation).WithMany(p => p.ReporteIdUsuarioReportaNavigations)
                .HasForeignKey(d => d.IdUsuarioReporta)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reportes_UsuarioReporta");

            entity.HasOne(d => d.IdUsuarioReportadoNavigation).WithMany(p => p.ReporteIdUsuarioReportadoNavigations)
                .HasForeignKey(d => d.IdUsuarioReportado)
                .HasConstraintName("FK_Reportes_UsuarioReportado");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.IdRol).HasName("PK__Roles__2A49584CEF8E4531");

            entity.HasIndex(e => e.Nombre, "UQ__Roles__75E3EFCFEFBBFED8").IsUnique();

            entity.Property(e => e.Nombre)
                .HasMaxLength(30)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.IdUsuario).HasName("PK__Usuarios__5B65BF978880EF8E");
            entity.Property(e => e.VersionSeguridad).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.IntentosFallidos).HasDefaultValue(0);
            entity.Property(e => e.BloqueoHastaUtc).HasColumnType("datetime2");

            entity.HasIndex(e => e.Correo, "UQ__Usuarios__60695A19863F3A17").IsUnique();

            entity.Property(e => e.Celular)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.Correo)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.Direccion)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Activo");
            entity.Property(e => e.FechaRegistro)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false);

            entity.HasOne(d => d.Rol).WithMany(p => p.Usuarios)
                .HasForeignKey(d => d.RolId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Usuarios_Roles");
        });

        modelBuilder.Entity<UsuarioImagen>(entity =>
        {
            entity.ToTable("UsuarioImagenes");

            entity.HasKey(e => e.IdImagen);

            entity.Property(e => e.Url)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Tipo)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.EsPrincipal)
                .HasDefaultValue(true);
            entity.Property(e => e.FechaSubida)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.UsuarioImagenes)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UsuarioImagenes_Usuarios");
        });

        modelBuilder.Entity<SesionUsuario>(entity =>
        {
            entity.ToTable("SesionesUsuario");
            entity.HasKey(s => s.IdSesion);
            entity.Property(s => s.IdSesion).ValueGeneratedNever();
            entity.Property(s => s.FechaCreacionUtc).HasColumnType("datetime2");
            entity.Property(s => s.ExpiraUtc).HasColumnType("datetime2");
            entity.Property(s => s.RevocadaUtc).HasColumnType("datetime2");
            entity.HasIndex(s => new { s.IdUsuario, s.RevocadaUtc, s.ExpiraUtc });
            entity.HasOne(s => s.Usuario).WithMany().HasForeignKey(s => s.IdUsuario).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RecuperacionCuenta>(entity =>
        {
            entity.ToTable("RecuperacionesCuenta");
            entity.HasKey(t => t.IdRecuperacion);
            entity.Property(t => t.IdRecuperacion).ValueGeneratedNever();
            entity.Property(t => t.TokenHash).HasMaxLength(64).IsUnicode(false);
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasIndex(t => new { t.IdUsuario, t.ConsumidaUtc });
            entity.Property(t => t.CreadaUtc).HasColumnType("datetime2");
            entity.Property(t => t.ExpiraUtc).HasColumnType("datetime2");
            entity.Property(t => t.ConsumidaUtc).HasColumnType("datetime2");
            entity.HasOne(t => t.Usuario).WithMany().HasForeignKey(t => t.IdUsuario).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AuditoriaSeguridad>(entity =>
        {
            entity.ToTable("AuditoriasSeguridad");
            entity.HasKey(a => a.IdAuditoria);
            entity.Property(a => a.Accion).HasMaxLength(64).IsUnicode(false);
            entity.Property(a => a.CorrelacionId).HasMaxLength(64).IsUnicode(false);
            entity.Property(a => a.TipoRecurso).HasMaxLength(64).IsUnicode(false);
            entity.Property(a => a.IdRecurso).HasMaxLength(128).IsUnicode(false);
            entity.Property(a => a.FechaUtc).HasColumnType("datetime2");
            entity.HasIndex(a => new { a.FechaUtc, a.IdObjetivo });
        });
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
