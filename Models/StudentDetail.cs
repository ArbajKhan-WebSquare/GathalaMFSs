using System;

namespace GathalaMFS.Models
{
    public class StudentDetail
    {
        public int Id { get; set; }
        public int CandidateId { get; set; }

        public string CandidateName { get; set; }

        public string FatherName { get; set; }

        public string CourseName { get; set; }

        public string Duration { get; set; }

        public string InstituteName { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? UpdatedDate { get; set; }
    }
}