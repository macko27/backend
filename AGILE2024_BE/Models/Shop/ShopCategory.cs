using System.ComponentModel.DataAnnotations.Schema;

namespace AGILE2024_BE.Models.Shop
{
    public class ShopCategory
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string Name { get; set; }
    }
}
