using System.ComponentModel.DataAnnotations.Schema;

namespace AGILE2024_BE.Models.Recognition
{
    public class RecognitionRecipient
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [ForeignKey(nameof(Recognition) + "Id")]
        public Recognition Recognition { get; set; }

        public required Guid EmployeeCardId { get; set; }
    }
}
