using AGILE2024_BE.Models.Enums;
using AGILE2024_BE.Models.Survey;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Principal;

namespace AGILE2024_BE.Models.Shop
{
    public class Order
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string CisloObjednavky { get; set; } = default!;
        public required string Ulica { get; set; }
        public required int CisloDomu { get; set; }
        public required string City { get; set; }
        public required string PSC { get; set; }
        public required string Telefon { get; set; }
        public string? Poznamka { get; set; }
        public int Cena { get; set; }

        [ForeignKey(nameof(EmployeeCard) + "Id")]
        public required EmployeeCard Zakaznik { get; set; }
        public ICollection<OrderItem>? Produkty { get; set; } = new List<OrderItem>();
        public DateTime DateIn { get; set; } = DateTime.UtcNow;
        public EnumOrderState Stav { get; set; } = EnumOrderState.Vytvorena;

    }
}
