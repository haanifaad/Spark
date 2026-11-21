// <summary>
// Registers WinUI-independent presentation services and their intended lifetimes. App-owned
// instances stay outside container ownership; navigation-scoped view models are transient.
// Usage:
//   var services = new ServiceCollection();
//   services.AddOpenClawTrayCore(context);
//   using var provider = services.BuildServiceProvider();
// </summary>

using OpenClaw.Shared.ExecApprovals;
using OpenClaw.Shared.Divisions;
using OpenClaw.Shared.Inference;
using OpenClaw.Shared.Channels;
using OpenClaw.Shared.Inference.Routing;
using OpenClaw.Shared.Media;
using OpenClaw.Shared.Memory;
using OpenClaw.Shared.Persona;
using OpenClaw.Shared.Security.Approvals;
using OpenClaw.Shared.Security.Audit;
using OpenClaw.Shared.Security.Sentinel;
using OpenClaw.Shared.Tools;
using OpenClaw.Shared.Tools.Reversal;
using OpenClaw.Shared.Orchestration;
using OpenClaw.Shared.Specialists.Finance;
using OpenClaw.Shared.Browser;
using OpenClaw.Shared.Verification;
using OpenClaw.Shared.Background;
using OpenClaw.Shared.Cron.Autonomous;
using Microsoft.Extensions.DependencyInjection;
using OpenClawTray.Chat;
using OpenClawTray.Services;

namespace OpenClawTray.Presentation;

/// <summary>
/// The WinUI-free core of the App composition root. Registers the presentation-layer
/// infrastructure and the App-owned singletons that view models depend on.
/// </summary>
/// <remarks>
/// Ownership rules encoded here:
/// <list type="bullet">
/// <item>App-owned singletons (<see cref="IUiDispatcher"/>, <see cref="IAppCommands"/>,
/// <see cref="SettingsManager"/>) are registered as <b>pre-built instances</b>, so the
/// container never disposes them — App remains their sole owner (no double-dispose).</item>
/// <item><see cref="NavigationScopeManager"/> is a container-created singleton, so the
/// container disposes it when the root provider is disposed.</item>
/// <item>Page view models are transient and are resolved from a per-navigation scope,
/// so they are disposed when navigation moves away.</item>
/// </list>
/// WinUI-bound registrations (the dispatcher/page-activator/navigation adapters) are
/// added separately by App so this method stays unit-testable in a pure net10 project.
/// </remarks>
internal static class AppServiceRegistration
{
    /// <summary>Registers core presentation services using the App-owned instances in <paramref name="context"/>.</summary>
    public static IServiceCollection AddOpenClawTrayCore(this IServiceCollection services, AppServiceContext context)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(context);

        // App-owned singletons: pre-built instances → container does not dispose them.
        services.AddSingleton(context.Dispatcher);
        services.AddSingleton(context.AppCommands);
        services.AddSingleton(context.Settings);
        services.AddSingleton(context.ExecApprovalsStore);
        services.AddSingleton(context.PermissionsRuntimeHost);
        if (context.LocalAiRuntime is not null)
            services.AddSingleton(context.LocalAiRuntime);
        if (context.LocalAiModelSwitcher is not null)
            services.AddSingleton(context.LocalAiModelSwitcher);
        if (context.DefaultModelGateway is not null)
            services.AddSingleton(context.DefaultModelGateway);
        if (context.ModelProviderGateway is not null)
            services.AddSingleton(context.ModelProviderGateway);
        // Settings facade over the App-owned SettingsManager. Container-owned so it can dispose
        // its Saved-event subscription during shutdown.
        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<SandboxStatusStore>();
        services.AddSingleton<IPermissionsPageRuntimeSource, PermissionsPageRuntimeSource>();
        services.AddSingleton<IHostHardwareProbe, CudaHostHardwareProbe>();

        // Container-owned navigation lifetime manager (disposed with the root provider).
        services.AddSingleton<NavigationScopeManager>();

        // Per-host-mount composer sessions borrow the dispatcher and settings store.
        services.AddSingleton<IChatComposerFactory, ChatComposerFactory>();

        // SPARK Division Infrastructure
        services.AddSingleton<IDivisionManager, DivisionManager>();
        services.AddSingleton<OpenClaw.Shared.VoiceMode.IVoiceModeDivisionService, OpenClaw.Shared.VoiceMode.VoiceModeDivisionService>();

        // SPARK Autonomous Personal AI Agent infrastructure
        services.AddSingleton<ISparkMemoryStore, SparkMemoryStore>();
        services.AddSingleton<ISparkPersonaManager, SparkPersonaManager>();
        services.AddSingleton<ISparkChannelCredentialStore, SparkChannelCredentialStore>();
        services.AddSingleton<ISparkChannelManager, SparkChannelManager>();
        services.AddSingleton<ISparkToolRegistry, SparkToolRegistry>();
        services.AddSingleton<ISparkModelRouter, SparkModelRouter>();
        services.AddSingleton<ISparkApprovalManager, SparkApprovalManager>();
        services.AddSingleton<ISparkAuditLogger, SparkAuditLogger>();
        services.AddSingleton<ISparkMediaProcessor, SparkMediaProcessor>();

        // SPARK Autonomous Orchestration, Sentinel, Specialists, Financial & Background Autonomy
        services.AddSingleton<ITaskStateManager, SparkTaskStateManager>();
        services.AddSingleton<ITaskDecomposer, SparkTaskDecomposer>();
        services.AddSingleton<ITaskPlanner, SparkTaskPlanner>();
        services.AddSingleton<ISentinelReviewer, SentinelReviewer>();
        services.AddSingleton<ISpendingLimitsEngine, SpendingLimitsEngine>();
        services.AddSingleton<ISparkReversalManager, SparkReversalManager>();
        services.AddSingleton<ISparkBrowserWorker, SparkBrowserWorker>();
        services.AddSingleton<IResultVerifier, SparkResultVerifier>();
        services.AddSingleton<IAgentOrchestrator, SparkAgentOrchestrator>();
        services.AddSingleton<ISparkAutonomousScheduler, SparkAutonomousScheduler>();
        services.AddSingleton<ISparkBackgroundWorker, SparkBackgroundWorker>();

        // Transient page view models resolved per navigation scope.
        services.AddTransient<SettingsPageViewModel>();
        services.AddTransient<PermissionsPageViewModel>();
        services.AddTransient<SparkAutonomousAgentViewModel>();
        if (context.LocalAiRuntime is not null && context.LocalAiModelSwitcher is not null)
            services.AddTransient<LocalAiPageViewModel>();
        if (context.LocalAiRuntime is not null && context.DefaultModelGateway is not null &&
            context.ModelProviderGateway is not null)
            services.AddTransient<DefaultModelPageViewModel>();

        return services;
    }
}
