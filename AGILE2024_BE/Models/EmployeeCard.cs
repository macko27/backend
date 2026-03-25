using AGILE2024_BE.Models.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGILE2024_BE.Models
{
    public class EmployeeCard
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [ForeignKey(nameof(User) + "Id")]
        public required ExtendedIdentityUser? User { get; set; }

        [ForeignKey(nameof(Location) + "Id")]
        public Location? Location { get; set; }

        [ForeignKey(nameof(Department) + "Id")]
        public Department? Department { get; set; }

        [ForeignKey(nameof(Level) + "Id")]
        public Level? Level { get; set; }

        [ForeignKey(nameof(ContractType) + "Id")]
        public ContractType? ContractType { get; set; }

        public int? WorkPercentage { get; set; }
        public DateTime? StartWorkDate { get; set; }
        public DateTime? Birthdate { get; set; }
        public required DateTime LastEdited { get; set; } = DateTime.Now;
        public required DateTime Created { get; set; } = DateTime.Now;
        public required bool Archived { get; set; } = false;
        public int PointsBalance { get; set; } = 0;
    }
}
