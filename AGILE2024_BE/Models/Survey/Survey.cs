using AGILE2024_BE.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGILE2024_BE.Models.Survey
{
    public class Survey
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public required string name { get; set; }
        public string? info { get; set; }
        public EnumSurveyState status { get; set; }
        public string SurveyType { get; set; } = "anonymous";

        [ForeignKey(nameof(EmployeeCard) + "Id")]
        public required EmployeeCard createdBy { get; set; }
        public required DateTime start { get; set; } = DateTime.Now;
        public required DateTime end { get; set; } = DateTime.Now;
        public ICollection<Recipient>? Recipients { get; set; } = new List<Recipient>();
        public ICollection<SurveyQuestion> Questions { get; set; } = new List<SurveyQuestion>();
    }
}
