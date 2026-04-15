using System.ComponentModel.DataAnnotations.Schema;

namespace AGILE2024_BE.Models.Shop
{
    public class Product
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Info { get; set; }
        public int Price { get; set; }
        public ShopCategory ShopCategory { get; set; }
        public ProductAttachment ProductAttachment { get; set; }
        public bool AnoPlatny { get; set; } = true;
    }
}
