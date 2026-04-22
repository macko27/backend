using System.ComponentModel.DataAnnotations.Schema;

namespace AGILE2024_BE.Models.Shop
{
    public class OrderItem
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product Product { get; set; }
        public int Quantity { get; set; }
        public int Price { get; set; }
    }
}
