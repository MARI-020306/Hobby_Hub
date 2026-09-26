using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Proyecto_HobbyHub.Models;

public partial class HobbyHubContext : DbContext
{
    public HobbyHubContext()
    {
    }

    public HobbyHubContext(DbContextOptions<HobbyHubContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Comentario> Comentarios { get; set; }

    public virtual DbSet<Comunidade> Comunidades { get; set; }

    public virtual DbSet<Publicacione> Publicaciones { get; set; }

    public virtual DbSet<Reporte> Reportes { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

<<<<<<< Updated upstream
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=127.0.0.1,1439;Database=HobbyHub;User Id=sa;Password=P@ssw0rd;TrustServerCertificate=True;");
=======
    public virtual DbSet<UsuarioImagen> UsuarioImagenes { get; set; }

    public virtual DbSet<MiembroComunidad> MiembrosComunidad { get; set; }
>>>>>>> Stashed changes

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

            entity.HasIndex(e => e.Correo, "UQ__Usuarios__60695A19863F3A17").IsUnique();

            entity.Property(e => e.Celular)
<<<<<<< Updated upstream
                .HasMaxLength(10)
                .IsFixedLength();
=======
                .HasMaxLength(64)
                .IsUnicode(false);
>>>>>>> Stashed changes
            entity.Property(e => e.Correo)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.Direccion)
                .HasMaxLength(10)
                .IsFixedLength();
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
<<<<<<< Updated upstream
            entity.Property(e => e.Password).HasMaxLength(256);
=======
            entity.Property(e => e.Password)
                .HasColumnType("varbinary(256)")
                .HasMaxLength(256);
>>>>>>> Stashed changes

            entity.HasOne(d => d.Rol).WithMany(p => p.Usuarios)
                .HasForeignKey(d => d.RolId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Usuarios_Roles");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
