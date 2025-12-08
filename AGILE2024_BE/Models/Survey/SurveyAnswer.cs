using System.ComponentModel.DataAnnotations.Schema;

namespace AGILE2024_BE.Models.Survey
{
    public class SurveyAnswer
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [ForeignKey(nameof(SurveyQuestion) + "Id")]
        public required SurveyQuestion question { get; set; }

        [ForeignKey(nameof(EmployeeCard) + "Id")]
        public required EmployeeCard user { get; set; }
        public ICollection<SurveyOption> selectedOptions { get; set; } = new List<SurveyOption>();
    }
}
