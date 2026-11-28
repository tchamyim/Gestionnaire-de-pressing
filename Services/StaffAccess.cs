using Microsoft.AspNetCore.Authorization;

namespace Pressings.Services;

public sealed record StaffRoleOption(string Code, string Name, string Description);

public static class StaffRoleCatalog
{
    public static readonly IReadOnlyList<StaffRoleOption> All =
    [
        new("super_admin", "Administrateur général", "Comptes, rôles, paramètres, rapports et contrôle complet."),
        new("manager", "Responsable du pressing", "Supervision des commandes, équipes, incidents et rapports."),
        new("reception", "Accueil / réception", "Clients, dépôts, tickets et suivi client."),
        new("washer", "Agent de lavage", "File de lavage, tri, nettoyage et signalement des anomalies."),
        new("finisher", "Repassage et finition", "Repassage, pliage, emballage et transmission au contrôle qualité."),
        new("cashier", "Caissier / comptable", "Paiements, reçus, caisse et rapports financiers."),
        new("delivery", "Livreur", "Missions de collecte et de remise qui lui sont attribuées."),
        new("quality", "Contrôle qualité", "Contrôle des articles, reprises et validation avant remise."),
        new("stockkeeper", "Gestionnaire de stock", "Consommables, mouvements et alertes de stock.")
    ];

    public static bool IsKnown(string? code) => All.Any(role => role.Code == code);
    public static string NameOf(string code) => All.FirstOrDefault(role => role.Code == code)?.Name ?? code;
}

public static class StaffPolicies
{
    public const string Employee = "Employee";
    public const string UserAdministration = "UserAdministration";
    public const string ActivityLog = "ActivityLog";
    public const string Dashboard = "Dashboard";
    public const string FinancialReports = "FinancialReports";
    public const string ClientManagement = "ClientManagement";
    public const string OrderRead = "OrderRead";
    public const string OrderCreate = "OrderCreate";
    public const string OrderWorkflow = "OrderWorkflow";
    public const string OrderBatchWorkflow = "OrderBatchWorkflow";
    public const string Payments = "Payments";
    public const string TicketPrinting = "TicketPrinting";
    public const string Inventory = "Inventory";
    public const string Tariffs = "Tariffs";
    public const string Communications = "Communications";
    public const string EmailConfiguration = "EmailConfiguration";
    public const string DeliveryManagement = "DeliveryManagement";
    public const string DeliveryWork = "DeliveryWork";
    public const string IncidentManagement = "IncidentManagement";

    private static readonly string[] OrderReaders = ["super_admin", "manager", "reception", "washer", "finisher", "cashier", "quality"];

    public static void Configure(AuthorizationOptions options)
    {
        Add(options, Employee, "staff");
        Add(options, UserAdministration, "super_admin");
        Add(options, ActivityLog, "super_admin");
        Add(options, Dashboard, "super_admin", "manager");
        Add(options, FinancialReports, "super_admin", "manager", "cashier");
        Add(options, ClientManagement, "super_admin", "manager", "reception");
        Add(options, OrderRead, OrderReaders);
        Add(options, OrderCreate, "super_admin", "manager", "reception");
        Add(options, OrderWorkflow, "super_admin", "manager", "washer", "finisher", "quality", "reception");
        Add(options, OrderBatchWorkflow, "super_admin", "manager");
        Add(options, Payments, "super_admin", "manager", "cashier");
        Add(options, TicketPrinting, "super_admin", "manager", "reception", "cashier");
        Add(options, Inventory, "super_admin", "manager", "stockkeeper");
        Add(options, Tariffs, "super_admin", "manager");
        Add(options, Communications, "super_admin", "manager", "reception");
        Add(options, EmailConfiguration, "super_admin", "manager");
        Add(options, DeliveryManagement, "super_admin", "manager", "reception");
        Add(options, DeliveryWork, "super_admin", "manager", "delivery", "reception");
        Add(options, IncidentManagement, "super_admin", "manager");
    }

    private static void Add(AuthorizationOptions options, string policy, params string[] roles) =>
        options.AddPolicy(policy, builder => builder.RequireRole(roles));

    public static bool IsManagerOrAdmin(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole("super_admin") || user.IsInRole("manager");

    public static bool CanSeeFinances(System.Security.Claims.ClaimsPrincipal user) =>
        IsManagerOrAdmin(user) || user.IsInRole("cashier");

    public static string LandingController(System.Security.Claims.ClaimsPrincipal user)
    {
        if (IsManagerOrAdmin(user) || user.IsInRole("cashier")) return "Dashboard";
        if (user.IsInRole("delivery")) return "Deliveries";
        if (user.IsInRole("stockkeeper")) return "Inventory";
        if (user.IsInRole("reception")) return "Orders";
        if (user.IsInRole("quality") || user.IsInRole("finisher") || user.IsInRole("washer")) return "Orders";
        return "Orders";
    }

    public static IReadOnlyList<string> AllowedNextStatuses(System.Security.Claims.ClaimsPrincipal user, string current)
    {
        if (IsManagerOrAdmin(user)) return ["déposé", "lavage", "repassage", "contrôle qualité", "à reprendre", "prêt", "retiré"];
        if (user.IsInRole("washer") && (current is "déposé" or "à reprendre")) return ["lavage"];
        if (user.IsInRole("washer") && current == "lavage") return ["repassage"];
        if (user.IsInRole("finisher") && current == "en traitement") return ["repassage"];
        if (user.IsInRole("finisher") && current == "repassage") return ["contrôle qualité"];
        if (user.IsInRole("quality") && current == "contrôle qualité") return ["prêt", "à reprendre"];
        if ((user.IsInRole("reception") || user.IsInRole("delivery")) && current == "prêt") return ["retiré"];
        return [];
    }
}
