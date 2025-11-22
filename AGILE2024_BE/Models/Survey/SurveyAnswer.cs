using System.ComponentModel.DataAnnotations.Schema;

namespace AGILE2024_BE.Models.Survey
{
    public class SurveyAnswer
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        [ForeignKey(nameof(Survey) + "Id")]
        public required Survey survey { get; set; }
        public string answer { get; set; }
        public ICollection<SurveyOption> selectedOptions { get; set; } = new List<SurveyOption>();
    }
}
