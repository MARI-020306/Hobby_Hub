using Microsoft.EntityFrameworkCore;

namespace Proyecto_HobbyHub.Models;

public partial class HobbyHubContext
{
    public DbSet<Seguimiento> Seguimientos { get; set; }
    public DbSet<PublicacionGuardada> PublicacionesGuardadas { get; set; }
    public DbSet<PublicacionImagen> PublicacionImagenes { get; set; }
    public DbSet<MensajePrivado> MensajesPrivados { get; set; }
    public DbSet<Notificacion> Notificaciones { get; set; }
    public DbSet<BloqueoUsuario> BloqueosUsuario { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        var model=modelBuilder;
        model.Entity<Usuario>().Property(u=>u.SuspensionHastaUtc).HasColumnType("datetime2");
        model.Entity<Reporte>().Property(r=>r.VersionFila).IsRowVersion();
        model.Entity<Reporte>().HasOne(r=>r.Mensaje).WithMany().HasForeignKey(r=>r.IdMensaje).OnDelete(DeleteBehavior.Restrict);
        model.Entity<BloqueoUsuario>(e=>{
            e.ToTable("BloqueosUsuario");e.HasKey(x=>new{x.IdActor,x.IdBloqueado});
            e.Property(x=>x.FechaUtc).HasColumnType("datetime2");
            e.HasOne<Usuario>().WithMany().HasForeignKey(x=>x.IdActor).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x=>x.IdBloqueado).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Usuario>().Property(u => u.Biografia).HasMaxLength(500).IsUnicode(false);
        model.Entity<Usuario>().Property(u => u.Intereses).HasMaxLength(200).IsUnicode(false);
        model.Entity<Comunidade>().Property(c => c.Reglas).HasMaxLength(2000).IsUnicode(false);
        model.Entity<MiembroComunidad>().Property(m => m.Estado).HasMaxLength(20).IsUnicode(false);
        model.Entity<Comentario>().Property(c => c.IdComentarioPadre);
        model.Entity<Comentario>().HasOne<Comentario>().WithMany().HasForeignKey(c=>c.IdComentarioPadre).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Reporte>().Property(r => r.Resolucion).HasMaxLength(500).IsUnicode(false);
        model.Entity<Seguimiento>(e => {
            e.HasKey(x => new { x.IdSeguidor, x.IdSeguido });
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.IdSeguidor).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.IdSeguido).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.FechaUtc).HasColumnType("datetime2");
        });
        model.Entity<PublicacionGuardada>(e => {
            e.HasKey(x => new { x.IdUsuario, x.IdPublicacion });
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.IdUsuario).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Publicacione>().WithMany().HasForeignKey(x => x.IdPublicacion).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.FechaUtc).HasColumnType("datetime2");
        });
        model.Entity<PublicacionImagen>(e => {
            e.HasKey(x => x.IdImagen); e.Property(x => x.IdImagen).ValueGeneratedNever();
            e.Property(x => x.Archivo).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.Tipo).HasMaxLength(20).IsUnicode(false);
            e.HasOne<Publicacione>().WithMany().HasForeignKey(x => x.IdPublicacion).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<MensajePrivado>(e => {
            e.Property(x=>x.Estado).HasMaxLength(15).IsUnicode(false);
            e.HasKey(x => x.IdMensaje); e.Property(x => x.Contenido).HasMaxLength(2000).IsUnicode(false);
            e.Property(x => x.FechaUtc).HasColumnType("datetime2"); e.Property(x => x.LeidoUtc).HasColumnType("datetime2");
            e.HasIndex(x => new { x.IdDestinatario, x.FechaUtc });
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.IdRemitente).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.IdDestinatario).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Notificacion>(e => {
            e.HasKey(x => x.IdNotificacion); e.Property(x => x.Tipo).HasMaxLength(40).IsUnicode(false);
            e.Property(x => x.FechaUtc).HasColumnType("datetime2"); e.HasIndex(x => new { x.IdUsuario, x.Leida, x.FechaUtc });
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.IdUsuario).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
