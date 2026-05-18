namespace GathalaMFS.Models
{
    public class ExcelFile
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public DateTime UploadedDate { get; set; }
        public int RecordCount { get; set; }

        public string FilePath { get; set; }
    }
}
