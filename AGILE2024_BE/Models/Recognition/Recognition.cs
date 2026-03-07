using AGILE2024_BE.Models.Enums;
using AGILE2024_BE.Models.Survey;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGILE2024_BE.Models.Recognition
{
    public class Recognition
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public required string Predmet { get; set; }
        public required string Text { get; set; }
        public int Odmena { get; set; }

        [ForeignKey(nameof(EmployeeCard) + "Id")]
        public required EmployeeCard createdBy { get; set; }
        public required DateTime DateIn { get; set; } = DateTime.UtcNow;
        public ICollection<RecognitionRecipient>? Recipients { get; set; } = new List<RecognitionRecipient>();
        public int anoPlatny { get; set; }
    }
}
