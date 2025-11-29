using System.ComponentModel.DataAnnotations.Schema;

namespace AGILE2024_BE.Models.Survey
{
    public class SurveyOption
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public string Answer { get; set; } = string.Empty;
        [ForeignKey(nameof(Question) + "Id")]
        public SurveyQuestion Question { get; set; } = null!;
    }
}
