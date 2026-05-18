namespace GathalaMFS.Models
{
    public class ExcelFileStudent
    {
        public int Id { get; set; }

        public int ExcelFileId { get; set; }
        public int StudentId { get; set; }

        public ExcelFile ExcelFile { get; set; }
        public StudentDetail Student { get; set; }
    }
}
