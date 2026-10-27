using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Xml.Linq;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.Security;

namespace Proyecto_HobbyHub.Tests;

public sealed class OfflineDatabaseInterceptor : DbConnectionInterceptor
{
    public const string Message = "Database access is disabled in offline tests.";

    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result) => throw new InvalidOperationException(Message);

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
        ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(Message);
}

public sealed class OfflineApplicationFactory : WebApplicationFactory<Program>
{
    public FakeAccountSecurityStore SecurityStore { get; } = new();
    public ManualTimeProvider Clock { get; } = new();
    public MemorySecurityAudit Audit { get; } = new();
    public MemoryRecoveryMailbox Mailbox { get; } = new();
    public FakeAccountRecoveryStore RecoveryStore { get; }
    public OfflineApplicationFactory()
    {
        SecurityStore.Audit = Audit;
        RecoveryStore = new(SecurityStore, Audit);
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Offline tests must not use the Windows event log or another user's key directory.
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["DevelopmentDatabase:ConnectionString"] =
                    "Server=localhost;Database=HobbyHub_Development_OfflineTests;Integrated Security=True;Connect Timeout=1"
            }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAccountSecurityStore>();
            services.RemoveAll<ISecurityAudit>();
            services.AddSingleton<ISecurityAudit>(Audit);
            services.RemoveAll<IAccountRecoveryStore>();
            services.AddSingleton<IAccountRecoveryStore>(RecoveryStore);
            services.RemoveAll<IRecoveryMailSender>();
            services.AddSingleton<IRecoveryMailSender>(Mailbox);
            services.AddSingleton<IAccountSecurityStore>(SecurityStore);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddControllersWithViews().AddApplicationPart(typeof(AdministracionController).Assembly);
            services.Configure<KeyManagementOptions>(options =>
            {
                options.XmlRepository = new MemoryKeyRepository();
                options.XmlEncryptor = null;
            });
            // Replace the original options factory, rather than composing provider callbacks.
            services.RemoveAll<HobbyHubContext>();
            services.RemoveAll<DbContextOptions<HobbyHubContext>>();
            services.AddScoped<DbContextOptions<HobbyHubContext>>(_ => new DbContextOptionsBuilder<HobbyHubContext>()
                .UseSqlServer("Server=localhost;Database=HobbyHub_Development_OfflineTests;Integrated Security=True;Connect Timeout=1")
                .AddInterceptors(new OfflineDatabaseInterceptor()).Options);
            services.AddScoped<HobbyHubContext>();
        });
    }

    private sealed class MemoryKeyRepository : IXmlRepository
    {
        private readonly List<XElement> _elements = new();
        public IReadOnlyCollection<XElement> GetAllElements()
        {
            lock (_elements) return _elements.Select(e => new XElement(e)).ToArray();
        }
        public void StoreElement(XElement element, string friendlyName)
        {
            lock (_elements) _elements.Add(new XElement(element));
        }
    }
}
