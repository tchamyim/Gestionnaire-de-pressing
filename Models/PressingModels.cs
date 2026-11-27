using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Pressings.Models;

public sealed class AdminAccount
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

public sealed class PortalAccount
{
    public int ClientId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string WhatsApp { get; set; } = "";
    public string PinHash { get; set; } = "";
}

public sealed class ClientRecord
{
    public int ClientId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string WhatsApp { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public decimal Balance { get; set; }
}

public sealed class ActivityHistoryEntry
{
    public string EventType { get; set; } = "";
    public string Details { get; set; } = "";
    public DateTime EventAt { get; set; }
}

public sealed class ReadyOrderNotice
{
    public int OrderId { get; set; }
    public int ClientId { get; set; }
    public string TicketNo { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string WhatsApp { get; set; } = "";
    public string Email { get; set; } = "";
    public int ReadyItems { get; set; }
    public DateTime? NextPickup { get; set; }
}

public sealed class ComposeEmailInput
{
    public int OrderId { get; set; }
    [Required, EmailAddress, StringLength(255)] public string Recipient { get; set; } = "";
    [Required, StringLength(180)] public string Subject { get; set; } = "";
    [Required, StringLength(4000)] public string Body { get; set; } = "";
}

public sealed class CommunicationHistoryEntry
{
    public int Id { get; set; }
    public string ClientName { get; set; } = "";
    public string TicketNo { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Status { get; set; } = "";
    public string Error { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class ClientMessageInput
{
    [Required, StringLength(160)] public string Subject { get; set; } = "Échange avec Fresh & Care";
    [Required, StringLength(4000)] public string Body { get; set; } = "";
}

public sealed class ClientConversationMessage
{
    public int MessageId { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public int? OrderId { get; set; }
    public string TicketNo { get; set; } = "";
    public string SenderRole { get; set; } = "";
    public string SenderName { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
}

public sealed class ClientConversationThread
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string LastSubject { get; set; } = "";
    public string LastBody { get; set; } = "";
    public DateTime LastMessageAt { get; set; }
    public int UnreadMessages { get; set; }
}

public sealed class ClientConversationViewModel
{
    public ClientRecord Client { get; set; } = new();
    public bool IsStaffView { get; set; }
    public List<ClientConversationMessage> Messages { get; set; } = [];
    public ClientMessageInput Draft { get; set; } = new();
}

public sealed class CommunicationsInboxViewModel
{
    public bool EmailConfigured { get; set; }
    public string SearchQuery { get; set; } = "";
    public bool UnreadOnly { get; set; }
    public List<ReadyOrderNotice> ReadyOrders { get; set; } = [];
    public List<CommunicationHistoryEntry> RecentMessages { get; set; } = [];
    public List<ClientConversationThread> Conversations { get; set; } = [];
    public List<ClientRecord> AllClients { get; set; } = [];
    public EmailSettingsInput EmailSettings { get; set; } = new();
}

public sealed class EmailSettingsInput
{
    [Required, StringLength(180)] public string Host { get; set; } = "";
    [Range(1, 65535)] public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    [StringLength(255)] public string User { get; set; } = "";
    [StringLength(255)] public string Password { get; set; } = "";
    public bool ClearPassword { get; set; }
    [Required, EmailAddress, StringLength(255)] public string FromAddress { get; set; } = "";
    [StringLength(120)] public string FromName { get; set; } = "Fresh & Care — Pressing";
    [StringLength(500)] public string AdditionalEmails { get; set; } = "";
}

public sealed class FreeEmailInput
{
    public int? ClientId { get; set; }
    [Required, StringLength(500)] public string Recipient { get; set; } = "";
    [Required, StringLength(180)] public string Subject { get; set; } = "";
    [Required, StringLength(4000)] public string Body { get; set; } = "";
}

public sealed class ClientInput
{
    [Required, StringLength(100)] public string FirstName { get; set; } = "";
    [Required, StringLength(100)] public string LastName { get; set; } = "";
    [Required, StringLength(30)] public string Phone { get; set; } = "";
    [StringLength(30)] public string? WhatsApp { get; set; }
    [EmailAddress, StringLength(255)] public string? Email { get; set; }
}

public sealed class TariffCategory
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public decimal BasePrice { get; set; }
    public decimal ExpressPercent { get; set; }
    public bool IsActive { get; set; }
}

public sealed class TariffInput
{
    public int CategoryId { get; set; }
    [Required, StringLength(100)] public string CategoryName { get; set; } = "";
    [Range(0, 100000000)] public decimal BasePrice { get; set; }
    [Range(0, 500)] public decimal ExpressPercent { get; set; } = 30;
    public bool IsActive { get; set; } = true;
}

public sealed class DashboardViewModel
{
    public int ClientCount { get; set; }
    public int ActiveOrders { get; set; }
    public int ReadyItems { get; set; }
    public decimal Outstanding { get; set; }
    public decimal TodayPayments { get; set; }
    public decimal TillBalance { get; set; }
    public decimal RevenueLast30Days { get; set; }
    public int UnreadClientMessages { get; set; }
    public int LowStockItems { get; set; }
    public int OverduePickupCount { get; set; }
    public List<DailyRevenuePoint> DailyRevenue { get; set; } = [];
    public List<ItemStageCount> ItemStages { get; set; } = [];
    public List<OrderSummary> RecentOrders { get; set; } = [];
    public List<OverduePickup> OverduePickups { get; set; } = [];
    public List<TariffCategory> Tariffs { get; set; } = [];
}

public sealed class OverduePickup
{
    public int OrderId { get; set; }
    public string TicketNo { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime ExpectedPickupAt { get; set; }
    public int DaysOverdue => Math.Max(0, (DateTime.Today - ExpectedPickupAt.Date).Days);
}

public sealed class DailyRevenuePoint
{
    public DateTime Day { get; set; }
    public decimal Amount { get; set; }
    public string Label => Day.ToString("ddd dd/MM", CultureInfo.GetCultureInfo("fr-FR"));
}

public sealed class ItemStageCount
{
    public string Status { get; set; } = "";
    public int Count { get; set; }
}

public sealed class OrderSummary
{
    public int OrderId { get; set; }
    public int ClientId { get; set; }
    public string TicketNo { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string OrderStatus { get; set; } = "";
    public string ClientName { get; set; } = "";
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    public decimal Remaining => Math.Max(0, Total - Paid);
    public int ReadyItems { get; set; }
}

public sealed class OrderItemInput
{
    public int CategoryId { get; set; }
    [Range(1, 100)] public int Quantity { get; set; } = 1;
    public string? Description { get; set; }
    public string? ConditionNotes { get; set; }
    public string? ExpectedPickup { get; set; }
    public string Service { get; set; } = "normal";
}

public sealed class NewOrderInput
{
    [Range(1, int.MaxValue)] public int ClientId { get; set; }
    [StringLength(255)] public string? Notes { get; set; }
    [Range(0, 100000000)] public decimal DiscountAmount { get; set; }
    [Range(0, 100000000)] public decimal InitialPaymentAmount { get; set; }
    [StringLength(30)] public string InitialPaymentMethod { get; set; } = "Espèces";
    public List<OrderItemInput> Items { get; set; } = [];
}

public sealed class OrderItemViewModel
{
    public int ItemId { get; set; }
    public string CategoryName { get; set; } = "";
    public string Description { get; set; } = "";
    public string ConditionNotes { get; set; } = "";
    public string StorageLocation { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public decimal ExpressFee { get; set; }
    public bool IsExpress { get; set; }
    public string ItemStatus { get; set; } = "";
    public DateTime DepositAt { get; set; }
    public DateTime? ExpectedPickupAt { get; set; }
    public DateTime? WithdrawnAt { get; set; }
}

public sealed class PaymentViewModel
{
    public int PaymentId { get; set; }
    public string ReceiptNo { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime PaymentAt { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string Reference { get; set; } = "";
}

public sealed class OrderDetailViewModel
{
    public OrderSummary Order { get; set; } = new();
    public ClientRecord Client { get; set; } = new();
    public string Notes { get; set; } = "";
    public List<OrderItemViewModel> Items { get; set; } = [];
    public List<PaymentViewModel> Payments { get; set; } = [];
    public decimal CashBalance { get; set; }
}

public sealed class PortalHomeViewModel
{
    public ClientRecord Client { get; set; } = new();
    public decimal Balance { get; set; }
    public List<OrderSummary> Orders { get; set; } = [];
    public int UnreadMessages { get; set; }
}

public sealed class InventoryItem
{
    public int InventoryItemId { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "unité";
    public decimal QuantityOnHand { get; set; }
    public decimal ReorderAt { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }
    public decimal StockValue => QuantityOnHand * UnitCost;
    public bool IsLow => QuantityOnHand <= ReorderAt;
}

public sealed class InventoryViewModel
{
    public List<InventoryItem> Items { get; set; } = [];
    public decimal StockValue { get; set; }
    public int LowStockCount { get; set; }
}

public sealed class InventoryItemInput
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(24)] public string Unit { get; set; } = "unité";
    [Range(0, 1000000)] public decimal InitialQuantity { get; set; }
    [Range(0, 1000000)] public decimal ReorderAt { get; set; }
    [Range(0, 100000000)] public decimal UnitCost { get; set; }
}

public sealed class InventoryMovementInput
{
    [Required] public string Direction { get; set; } = "entrée";
    [Range(typeof(decimal), "0.001", "1000000")] public decimal Quantity { get; set; }
    [Range(0, 100000000)] public decimal? UnitCost { get; set; }
    [Required, StringLength(150)] public string Reason { get; set; } = "";
}

public sealed class CashMovementInput
{
    [Required] public string Direction { get; set; } = "sortie";
    [Range(1, 100000000)] public decimal Amount { get; set; }
    [Required, StringLength(150)] public string Description { get; set; } = "";
}

public sealed class PaymentInput
{
    [Range(1, 100000000)] public decimal Amount { get; set; }
    [Required] public string PaymentMethod { get; set; } = "Espèces";
    [StringLength(100)] public string? Reference { get; set; }
}

public sealed class UpdateItemInput
{
    [Required] public string ItemStatus { get; set; } = "déposé";
    public DateTime? ExpectedPickupAt { get; set; }
    [StringLength(60)] public string? StorageLocation { get; set; }
}

public sealed class DailyClosingReport
{
    public DateTime Date { get; set; }
    public int PaymentCount { get; set; }
    public decimal CashPayments { get; set; }
    public decimal MobileMoneyPayments { get; set; }
    public decimal CardAndTransferPayments { get; set; }
    public decimal TotalPayments { get; set; }
    public decimal ManualCashIn { get; set; }
    public decimal ManualCashOut { get; set; }
    public decimal TheoreticalTillBalance { get; set; }
}

public sealed class PortalLoginInput
{
    [Required] public string Phone { get; set; } = "";
    [Required, StringLength(12)] public string Pin { get; set; } = "";
}

public sealed class AdminLoginInput
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

public sealed class SetupAdminInput
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, StringLength(128)] public string Password { get; set; } = "";
    [Required, Compare(nameof(Password))] public string ConfirmPassword { get; set; } = "";
}

public sealed class ChangePinInput
{
    [Required] public string CurrentPin { get; set; } = "";
    [Required, MinLength(6), StringLength(12)] public string NewPin { get; set; } = "";
}
