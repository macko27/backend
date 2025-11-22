using System.ComponentModel.DataAnnotations.Schema;

namespace AGILE2024_BE.Models.Survey
{
    public class SurveyOption
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [ForeignKey(nameof(Survey) + "Id")]
        public required Survey survey { get; set; }
        public required string answer { get; set; }
    }
}
