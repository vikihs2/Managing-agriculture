using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagingAgriculture.Models;
using System.Linq;
using System.Threading.Tasks;

namespace ManagingAgriculture.Controllers
{
    [Authorize(Roles = "Employee")]
    public class EmployeeController : Controller
    {
        private readonly ManagingAgriculture.Data.ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmployeeController(ManagingAgriculture.Data.ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Get tasks assigned to this employee
            var tasks = await _context.TaskAssignments
                .Include(t => t.AssignedMachinery)
                .Where(t => t.AssignedToUserId == user.Id)
                .OrderByDescending(t => t.AssignedDate)
                .ToListAsync();

            // Get leave requests for this employee
            var leaveRequests = await _context.LeaveRequests
                .Where(l => l.UserId == user.Id)
                .OrderByDescending(l => l.LeaveDate)
                .ToListAsync();

            // Days off remaining
            var approvedDaysOff = leaveRequests.Count(l => l.Status == "Approved");
            ViewBag.LeaveDaysUsed = approvedDaysOff;
            ViewBag.LeaveDaysTotal = user.LeaveDaysTotal;
            ViewBag.LeaveDaysRemaining = user.LeaveDaysTotal - approvedDaysOff;

            ViewBag.LeaveRequests = leaveRequests;
            ViewBag.Salary = user.Salary;
            ViewBag.IsSalaryPaid = user.IsSalaryPaidInfo;
            ViewBag.UserName = user.Email;

            return View(tasks);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteTask(int taskId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var task = await _context.TaskAssignments.FindAsync(taskId);
            if (task != null && task.AssignedToUserId == user.Id)
            {
                task.IsCompletedByEmployee = true;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Task marked as complete. Awaiting boss approval.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestLeave(DateTime leaveDate, string? reason)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Validate: not weekend
            if (leaveDate.DayOfWeek == DayOfWeek.Saturday || leaveDate.DayOfWeek == DayOfWeek.Sunday)
            {
                TempData["Error"] = "You cannot request leave on weekends.";
                return RedirectToAction(nameof(Index));
            }

            // Check Bulgarian holidays
            if (IsBulgarianHoliday(leaveDate))
            {
                TempData["Error"] = "You cannot request leave on official Bulgarian holidays.";
                return RedirectToAction(nameof(Index));
            }

            // Check if already has a request for that date
            var existing = await _context.LeaveRequests
                .FirstOrDefaultAsync(l => l.UserId == user.Id && l.LeaveDate.Date == leaveDate.Date);
            if (existing != null)
            {
                TempData["Error"] = "You already have a leave request for that date.";
                return RedirectToAction(nameof(Index));
            }

            // Check if days remaining
            var approvedDays = await _context.LeaveRequests
                .CountAsync(l => l.UserId == user.Id && l.Status == "Approved");
            if (approvedDays >= user.LeaveDaysTotal)
            {
                TempData["Error"] = "You have no remaining leave days.";
                return RedirectToAction(nameof(Index));
            }

            // Max 2 requests for same date in company (but only 1 can be approved)
            if (user.CompanyId.HasValue)
            {
                var sameDayApproved = await _context.LeaveRequests
                    .AnyAsync(l => l.CompanyId == user.CompanyId && l.LeaveDate.Date == leaveDate.Date && l.Status == "Approved");
                if (sameDayApproved)
                {
                    TempData["Error"] = "That date is already taken by another employee. Max 1 person can have a day off per date.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var request = new LeaveRequest
            {
                UserId = user.Id,
                CompanyId = user.CompanyId,
                LeaveDate = leaveDate.Date,
                Reason = reason,
                Status = "Pending",
                RequestedDate = DateTime.UtcNow
            };

            _context.LeaveRequests.Add(request);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Leave request submitted. Waiting for boss approval.";
            return RedirectToAction(nameof(Index));
        }

        private bool IsBulgarianHoliday(DateTime date)
        {
            var holidays = new HashSet<(int month, int day)>
            {
                (1, 1),   // New Year's Day
                (3, 3),   // Liberation Day
                (5, 1),   // Labour Day
                (5, 6),   // St. George's Day
                (5, 24),  // Culture and Literacy Day
                (9, 6),   // Unification Day
                (9, 22),  // Independence Day
                (11, 1),  // National Awakeners Day
                (12, 24), // Christmas Eve
                (12, 25), // Christmas Day
                (12, 26), // Christmas Day 2
            };
            return holidays.Contains((date.Month, date.Day));
        }
    }
}
