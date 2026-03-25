using System.ComponentModel.DataAnnotations.Schema;

namespace AGILE2024_BE.Models.Recognition
{
    public class RecognitionAttachment
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string FileName { get; set; }
        public string FileUrl { get; set; }

        public Guid RecognitionId { get; set; }
        public Recognition Recognition { get; set; }
    }
}
