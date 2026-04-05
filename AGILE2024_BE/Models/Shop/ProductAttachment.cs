using System.ComponentModel.DataAnnotations.Schema;

namespace AGILE2024_BE.Models.Shop
{
    public class ProductAttachment
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string FileName { get; set; }
        public string FileUrl { get; set; }

        public Guid ProductId { get; set; }
        public Product Product { get; set; }
    }
}
