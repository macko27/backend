using System.ComponentModel.DataAnnotations.Schema;

namespace AGILE2024_BE.Models.Survey
{
    public class Recipient
    {
        public Guid Id { get; set; }

        [ForeignKey(nameof(Survey) + "Id")]
        public Survey Survey { get; set; }

        public required Guid EmployeeCardId { get; set; }
        public string Type { get; set; } // "employee" alebo "department"
    }

}
