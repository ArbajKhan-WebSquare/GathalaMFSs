using GathalaMFS.Data;
using GathalaMFS.Models;
using Microsoft.AspNetCore.Mvc;

namespace GathalaMFS.Controllers
{
    public class LoginController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LoginController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Index(User model)
        {
            var user = _context.Users
                .FirstOrDefault(x =>
                    x.Username == model.Username &&
                    x.Password == model.Password);

            if (user != null)
            {
                HttpContext.Session.SetString("User", user.Username);

                return RedirectToAction("ExcelUpload", "Student");
            }

            ViewBag.Error = "Invalid Username or Password";

            return View();
        }

        // GET
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (model.NewPassword != model.ConfirmPassword)
            {
                ViewBag.Error = "Passwords do not match";
                return View();
            }

            var user = _context.Users
                .FirstOrDefault(x => x.Username == model.Username);

            if (user == null)
            {
                ViewBag.Error = "User not found";
                return View();
            }

            user.Password = model.NewPassword;

            _context.SaveChanges();

            ViewBag.Success = "Password updated successfully";

            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            return RedirectToAction("Index");
        }
    }
}
