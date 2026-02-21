namespace AGILE2024_BE.Models.Requests.EmployeeCardRequests
{
    public class UpdateEmployeeCardRequest
    {
        public Guid Id { get; set; }
        public string? Birth { get; set; }
        public Guid? Location { get; set; }
        public Guid? Department { get; set; }
        public Guid? JobPosition { get; set; }
        public Guid? ContractType { get; set; }
        public Guid? Level { get; set; }
        public int WorkTime { get; set; }
        public string UserId {get; set;}
        public string StartWorkDate { get; set; }
    }
}
