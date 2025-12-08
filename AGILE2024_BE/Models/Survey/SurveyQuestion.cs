using System.ComponentModel.DataAnnotations.Schema;

namespace AGILE2024_BE.Models.Survey
{
    public class SurveyQuestion
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public required string question { get; set; }

        public Guid SurveyId { get; set; }
        public Survey Survey { get; set; } = null!;

        public ICollection<SurveyOption> Options { get; set; } = new List<SurveyOption>();
        public String answerType { get; set; }
    }
}
