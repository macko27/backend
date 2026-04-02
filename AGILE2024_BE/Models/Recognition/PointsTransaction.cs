namespace AGILE2024_BE.Models.Recognition
{
    public class PointsTransaction
    {
        public Guid Id { get; set; }

        public Guid EmployeeCardId { get; set; }
        public EmployeeCard EmployeeCard { get; set; }

        public int Points { get; set; }

        public string Type { get; set; } // RECEIVED, SENT, ADJUSTMENT

        public string Description { get; set; }

        public DateTime CreatedAt { get; set; }

        public Guid? RecognitionId { get; set; }
    }
}
