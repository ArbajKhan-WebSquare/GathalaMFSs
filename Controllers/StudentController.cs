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

        //public async Task<IActionResult> Index()
        //{
        //    var data = await _context.StudentDetails.ToListAsync();
        //    return View(data);
        //}

        public async Task<IActionResult> Index(int Id)
        {
            var data = await _context.ExcelFileStudents 
                .Include(x=> x.ExcelFile)
                .Include(x=> x.Student)
                .Where(x => x.ExcelFileId == Id)
                .ToListAsync();

            return View(data);
        }

        //[HttpPost]
        //public async Task<IActionResult> Upload(IFormFile file)
        //{
        //    if (file == null || file.Length == 0)
        //        return Content("File not selected");

        //    var list = new List<StudentDetail>();

        //    using (var stream = new MemoryStream())
        //    {
        //        await file.CopyToAsync(stream);

        //        using (var package = new ExcelPackage(stream))
        //        {
        //            var worksheet = package.Workbook.Worksheets[0];

        //            if (worksheet == null)
        //                return Content("No sheet found in Excel");

        //            int rowCount = worksheet.Dimension.Rows;

        //            for (int row = 2; row <= rowCount; row++)
        //            {
        //                list.Add(new StudentDetail
        //                {
        //                    CandidateName = worksheet.Cells[row, 1].Value?.ToString(),
        //                    FatherName = worksheet.Cells[row, 2].Value?.ToString(),
        //                    CourseName = worksheet.Cells[row, 3].Value?.ToString(),
        //                    Duration = worksheet.Cells[row, 4].Value?.ToString(),
        //                    InstituteName = worksheet.Cells[row, 5].Value?.ToString(),
        //                    CreatedDate = DateTime.Now
        //                });
        //            }
        //        }
        //    }

        //    _context.StudentDetails.AddRange(list);
        //    await _context.SaveChangesAsync();

        //    return RedirectToAction("Index");
        //}
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
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return RedirectToAction("Index");

            var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");

            if (!Directory.Exists(uploads))
                Directory.CreateDirectory(uploads);

            var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
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

            // ✅ CHECK LIMIT BEFORE INSERT
            int currentCount = await _context.StudentDetails.CountAsync();

            if (currentCount + list.Count > 300)
            {
                int remaining = 300 - currentCount;

                // Optional: delete uploaded file since insert is rejected
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                TempData["Error"] = $"Cannot upload file. Only {remaining} student slots remaining (Max 250).";
                return RedirectToAction("ExcelUpload");
            }

            // 3. SAVE STUDENTS
            _context.StudentDetails.AddRange(list);
            await _context.SaveChangesAsync();

            // 4. SAVE EXCEL FILE RECORD    
            var excelFile = new ExcelFile
            {
                FileName = file.FileName,
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

                var stream = new MemoryStream(package.GetAsByteArray());
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "StudentTemplate.xlsx");
            }
        }

        //public IActionResult BulkUpload()
        //{
        //    return View();
        //}

        //[HttpPost]
        //public async Task<IActionResult> PreviewUpload(IFormFile file)
        //{
        //    if (file == null || file.Length == 0)
        //        return Json(new { success = false, message = "File not selected" });

        //    var previewData = new List<Dictionary<string, string>>();

        //    using (var stream = new MemoryStream())
        //    {
        //        await file.CopyToAsync(stream);

        //        using (var package = new ExcelPackage(stream))
        //        {
        //            var worksheet = package.Workbook.Worksheets[0];

        //            if (worksheet == null)
        //                return Json(new { success = false, message = "No sheet found in Excel" });

        //            int rowCount = worksheet.Dimension.Rows;
        //            int colCount = worksheet.Dimension.Columns;

        //            for (int row = 2; row <= rowCount; row++)
        //            {
        //                var rowData = new Dictionary<string, string>();
        //                for (int col = 1; col <= colCount; col++)
        //                {
        //                    var header = worksheet.Cells[1, col].Value?.ToString() ?? $"Column{col}";
        //                    var value = worksheet.Cells[row, col].Value?.ToString() ?? "";
        //                    rowData[header] = value;
        //                }
        //                previewData.Add(rowData);
        //            }
        //        }
        //    }

        //    return Json(new { success = true, data = previewData, count = previewData.Count });
        //}

        //[HttpPost]
        //public async Task<IActionResult> ConfirmUpload(IFormFile file)
        //{
        //    if (file == null || file.Length == 0)
        //        return Json(new { success = false, message = "File not selected" });

        //    var list = new List<StudentDetail>();

        //    using (var stream = new MemoryStream())
        //    {
        //        await file.CopyToAsync(stream);

        //        using (var package = new ExcelPackage(stream))
        //        {
        //            var worksheet = package.Workbook.Worksheets[0];

        //            if (worksheet == null)
        //                return Json(new { success = false, message = "No sheet found in Excel" });

        //            int rowCount = worksheet.Dimension.Rows;

        //            for (int row = 2; row <= rowCount; row++)
        //            {
        //                list.Add(new StudentDetail
        //                {
        //                    CandidateName = worksheet.Cells[row, 1].Value?.ToString(),
        //                    FatherName = worksheet.Cells[row, 2].Value?.ToString(),
        //                    CourseName = worksheet.Cells[row, 3].Value?.ToString(),
        //                    Duration = worksheet.Cells[row, 4].Value?.ToString(),
        //                    InstituteName = worksheet.Cells[row, 5].Value?.ToString(),
        //                    CreatedDate = DateTime.Now
        //                });
        //            }
        //        }
        //    }

        //    _context.StudentDetails.AddRange(list);
        //    await _context.SaveChangesAsync();

        //    return Json(new { success = true, message = $"{list.Count} records imported successfully" });
        //}
    }
}
