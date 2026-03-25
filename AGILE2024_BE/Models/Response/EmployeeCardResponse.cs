namespace AGILE2024_BE.Models.Response
{
    public class EmployeeCardResponse
    {
        public Guid EmployeeId { get; set; }
        public string Email { get; set; }
        public string Birthdate { get; set; }
        public string Position { get; set; }
        public string JobPosition { get; set; }
        public string StartWorkDate { get; set; }
        public string Name { get; set; }
        public string TitleBefore { get; set; }
        public string TitleAfter { get; set; }
        public string Department { get; set; }
        public string DepartmentId { get; set; }
        public string Organization { get; set; }
        public string ContractType { get; set; }
        public int? WorkPercentage { get; set; }
        public string Surname { get; set; }
        public string Location { get; set; }
        public bool Archived { get; set; }
        public string EmploymentDuration { get; set; }
        public string? MiddleName { get; set; }
        public string? FullName { get; set; }
        public int Points { get; set; }
    }

}
