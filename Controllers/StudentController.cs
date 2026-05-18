using GathalaMFS.Data;
using GathalaMFS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using OfficeOpenXml;
using QRCoder;
using LicenseContext = OfficeOpenXml.LicenseContext;

namespace GathalaMFS.Controllers
{
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudentController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int Id)
        {
            var data = await _context.ExcelFileStudents 
                .Include(x=> x.ExcelFile)
                .Include(x=> x.Student)
                .Where(x => x.ExcelFileId == Id)
                .ToListAsync();

            return View(data);
        }
        public IActionResult Download(int id)
        {
            var student = _context.StudentDetails.FirstOrDefault(x => x.Id == id);

            if (student == null)
                return NotFound();

            string qrText = Url.Action("Detail","Student",new { id = student.Id },Request.Scheme );

            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q);

                using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                {
                    byte[] qrCodeBytes = qrCode.GetGraphic(20);

                    ViewBag.QRCodeImage = Convert.ToBase64String(qrCodeBytes);
                }
            }

            return View(student);
        }

        public async Task<IActionResult> Detail(int id)
        {
            var student = await _context.StudentDetails
                .FirstOrDefaultAsync(x => x.Id == id);

            if (student == null)
                return NotFound();

            return View(student);
        }

        public async Task<IActionResult> ExcelUpload()
        {
            var data = await _context.ExcelFiles.ToListAsync();
            return View(data);
        }

        [HttpPost]
        public async Task<IActionResult> UploadExcel(IFormFile file, string InputFileName)
        {
            string finalFileName = InputFileName?.Trim();

            if (string.IsNullOrWhiteSpace(finalFileName))
            {
                finalFileName = Path.GetFileNameWithoutExtension(file.FileName);
            }

            // CHECK DUPLICATE FILE NAME
            bool alreadyExists = await _context.ExcelFiles
                .AnyAsync(x => x.FileName == finalFileName);

            if (alreadyExists)
            {
                //// Optional: delete uploaded physical file
                //if (System.IO.File.Exists(filePath))
                //    System.IO.File.Delete(filePath);

                TempData["Error"] = $"File name '{finalFileName}' already exists. Please use a different name.";

                return RedirectToAction("ExcelUpload");
            }
            if (file == null || file.Length == 0)
                return RedirectToAction("Index");

            var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");

            if (!Directory.Exists(uploads))
                Directory.CreateDirectory(uploads);

            //var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            //var filePath = Path.Combine(uploads, fileName);
            var extension = Path.GetExtension(file.FileName);
            var fileName = $"File_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
            var filePath = Path.Combine(uploads, fileName);

            // 1. SAVE FILE PHYSICALLY
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var list = new List<StudentDetail>();
            int rowCount = 0;

            // 2. READ EXCEL DATA
            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = package.Workbook.Worksheets[0];

                if (worksheet == null)
                    return Content("No sheet found");

                rowCount = worksheet.Dimension.Rows;

                for (int row = 2; row <= rowCount; row++)
                {
                    int candidateId = 0;
                    int.TryParse(worksheet.Cells[row, 1].Value?.ToString(), out candidateId);

                    list.Add(new StudentDetail
                    {
                        CandidateId = candidateId,
                        CandidateName = worksheet.Cells[row, 2].Value?.ToString(),
                        FatherName = worksheet.Cells[row, 3].Value?.ToString(),
                        CourseName = worksheet.Cells[row, 4].Value?.ToString(),
                        Duration = worksheet.Cells[row, 5].Value?.ToString(),
                        InstituteName = worksheet.Cells[row, 6].Value?.ToString(),
                        CreatedDate = DateTime.Now
                    });
                }
            }

            var duplicateInExcel = list
    .GroupBy(x => x.CandidateId)
    .Where(g => g.Count() > 1)
    .Select(g => g.Key)
    .ToList();

            if (duplicateInExcel.Any())
            {
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                TempData["Error"] = $"Duplicate CandidateId found in Excel: {string.Join(", ", duplicateInExcel)}";

                return RedirectToAction("ExcelUpload");
            }
            // ✅ CHECK LIMIT BEFORE INSERT
            int currentCount = await _context.StudentDetails.CountAsync();

            if (currentCount + list.Count > 300)
            {
                int remaining = 300 - currentCount;

                // Optional: delete uploaded file since insert is rejected
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                TempData["Error"] = $"Cannot upload file. Only {remaining} student slots remaining (Max 300).";
                return RedirectToAction("ExcelUpload");
            }

            // 3. SAVE STUDENTS
            _context.StudentDetails.AddRange(list);
            await _context.SaveChangesAsync();

            // 4. SAVE EXCEL FILE RECORD    
            var excelFile = new ExcelFile
            {
                FileName = finalFileName,
                FilePath = "/uploads/" + fileName,
                UploadedDate = DateTime.Now,
                RecordCount = list.Count
            };

            _context.ExcelFiles.Add(excelFile);
            await _context.SaveChangesAsync();

            var mappings = list.Select(s => new ExcelFileStudent
            {
                ExcelFileId = excelFile.Id,
                StudentId = s.Id
            }).ToList();

            _context.ExcelFileStudents.AddRange(mappings);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Excel uploaded successfully!";
            return RedirectToAction("ExcelUpload");
        }


        public IActionResult DownloadTemplate()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("StudentData");
                worksheet.Cells["A1"].Value = "CandidateId";
                worksheet.Cells["B1"].Value = "CandidateName";
                worksheet.Cells["C1"].Value = "FatherName";
                worksheet.Cells["D1"].Value = "CourseName";
                worksheet.Cells["E1"].Value = "Duration";
                worksheet.Cells["F1"].Value = "InstituteName";

                worksheet.Cells["A1:F1"].Style.Font.Bold = true;
                worksheet.Cells["A1:F1"].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                worksheet.Cells["A1:F1"].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);

                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                var stream = new MemoryStream(package.GetAsByteArray());
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "StudentTemplate.xlsx");
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditExcel(int Id, string FileName, IFormFile file)
        {
            var excel = await _context.ExcelFiles
                .FirstOrDefaultAsync(x => x.Id == Id);

            if (excel == null)
                return NotFound();

            bool alreadyExists = await _context.ExcelFiles
        .AnyAsync(x => x.FileName == FileName && x.Id != Id);

            if (alreadyExists)
            {
                TempData["Error"] = $"File name '{FileName}' already exists. Please use different name.";
                return RedirectToAction("ExcelUpload");
            }

            // 1. UPDATE FILE IF NEW UPLOADED
            if (file != null && file.Length > 0)
            {
                var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");

                if (!Directory.Exists(uploads))
                    Directory.CreateDirectory(uploads);

                var extension = Path.GetExtension(file.FileName);
                var newFileName = $"File_{DateTime.Now:yyyyMMdd_HHmmssfff}{extension}";
                var filePath = Path.Combine(uploads, newFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // delete old file
                if (!string.IsNullOrEmpty(excel.FilePath))
                {
                    var oldPath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        excel.FilePath.TrimStart('/')
                    );

                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                excel.FilePath = "/uploads/" + newFileName;
            }

            excel.FileName = FileName;
            excel.UploadedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            // 2. DELETE OLD MAPPINGS + STUDENTS (IMPORTANT FIX)
            var oldMappings = await _context.ExcelFileStudents
                .Where(x => x.ExcelFileId == excel.Id)
                .ToListAsync();

            var oldStudentIds = oldMappings.Select(x => x.StudentId).ToList();

            _context.ExcelFileStudents.RemoveRange(oldMappings);

            var oldStudents = await _context.StudentDetails
                .Where(x => oldStudentIds.Contains(x.Id))
                .ToListAsync();

            _context.StudentDetails.RemoveRange(oldStudents);

            await _context.SaveChangesAsync();

            // 3. RE-IMPORT NEW EXCEL DATA
            var list = new List<StudentDetail>();

            using (var package = new ExcelPackage(new FileInfo(
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", excel.FilePath.TrimStart('/'))
            )))
            {
                var worksheet = package.Workbook.Worksheets[0];

                if (worksheet == null)
                    return Content("No sheet found");

                int rowCount = worksheet.Dimension.Rows;

                for (int row = 2; row <= rowCount; row++)
                {
                    int candidateId = 0;
                    int.TryParse(worksheet.Cells[row, 1].Value?.ToString(), out candidateId);

                    list.Add(new StudentDetail
                    {
                        CandidateId = candidateId,
                        CandidateName = worksheet.Cells[row, 2].Value?.ToString(),
                        FatherName = worksheet.Cells[row, 3].Value?.ToString(),
                        CourseName = worksheet.Cells[row, 4].Value?.ToString(),
                        Duration = worksheet.Cells[row, 5].Value?.ToString(),
                        InstituteName = worksheet.Cells[row, 6].Value?.ToString(),
                        CreatedDate = DateTime.Now
                    });
                }
            }
            var duplicateInExcel = list
            .GroupBy(x => x.CandidateId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

            if (duplicateInExcel.Any())
            {
                TempData["Error"] = $"Duplicate CandidateId found in Excel: {string.Join(", ", duplicateInExcel)}";
                return RedirectToAction("ExcelUpload");
            }

            int currentCount = await _context.StudentDetails.CountAsync();

            if (currentCount + list.Count > 300)
            {
                int remaining = 300 - currentCount;

                TempData["Error"] = $"Cannot upload file. Only {remaining} student slots remaining (Max 300).";
                return RedirectToAction("ExcelUpload");
            }
            // 4. SAVE NEW STUDENTS
            _context.StudentDetails.AddRange(list);
            await _context.SaveChangesAsync();

            // 5. RECREATE MAPPINGS
            var mappings = list.Select(s => new ExcelFileStudent
            {
                ExcelFileId = excel.Id,
                StudentId = s.Id
            }).ToList();

            _context.ExcelFileStudents.AddRange(mappings);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Excel file and student data updated successfully.";

            return RedirectToAction("ExcelUpload");
        }

        public async Task<IActionResult> DownloadExcelFile(int id)
        {
            var excelFile = await _context.ExcelFiles
                .FirstOrDefaultAsync(x => x.Id == id);

            if (excelFile == null)
                return NotFound();

            // Get all students associated with this Excel file
            var mappings = await _context.ExcelFileStudents
                .Include(x => x.Student)
                .Where(x => x.ExcelFileId == id)
                .ToListAsync();

            var students = mappings.Select(m => m.Student).ToList();

            // Create Excel package
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("StudentData");

                // Add headers
                worksheet.Cells["A1"].Value = "CandidateId";
                worksheet.Cells["B1"].Value = "CandidateName";
                worksheet.Cells["C1"].Value = "FatherName";
                worksheet.Cells["D1"].Value = "CourseName";
                worksheet.Cells["E1"].Value = "Duration";
                worksheet.Cells["F1"].Value = "InstituteName";

                // Style headers
                worksheet.Cells["A1:F1"].Style.Font.Bold = true;
                worksheet.Cells["A1:F1"].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                worksheet.Cells["A1:F1"].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);

                // Add data
                int row = 2;
                foreach (var student in students)
                {
                    worksheet.Cells[row, 1].Value = student.CandidateId;
                    worksheet.Cells[row, 2].Value = student.CandidateName;
                    worksheet.Cells[row, 3].Value = student.FatherName;
                    worksheet.Cells[row, 4].Value = student.CourseName;
                    worksheet.Cells[row, 5].Value = student.Duration;
                    worksheet.Cells[row, 6].Value = student.InstituteName;
                    row++;
                }

                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                var stream = new MemoryStream(package.GetAsByteArray());
                string fileName = $"{excelFile.FileName}_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }
    }
}
