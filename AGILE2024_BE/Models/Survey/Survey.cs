using AGILE2024_BE.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGILE2024_BE.Models.Survey
{
    public class Survey
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public required string name { get; set; }
        public required string question { get; set; }
        public string? info { get; set; }
        public EnumSurveyState status { get; set; }

        [ForeignKey(nameof(EmployeeCard) + "Id")]
        public required EmployeeCard createdBy { get; set; }
        public required DateTime end { get; set; } = DateTime.Now;
        public ICollection<SurveyOption> Options { get; set; } = new List<SurveyOption>();
    }
}
