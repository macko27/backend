using System.ComponentModel.DataAnnotations.Schema;

namespace AGILE2024_BE.Models.Survey
{
    public class SurveyOptionAnswer
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        // FK na SurveyAnswer
        public Guid SurveyAnswerId { get; set; }
        public SurveyAnswer SurveyAnswer { get; set; }

        // iba ID zvolenej možnosti
        public Guid OptionId { get; set; }
    }
}
