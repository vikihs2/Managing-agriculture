using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagingAgriculture.Models;
using System.Linq;
using System.Threading.Tasks;

namespace ManagingAgriculture.Controllers
{
    [Authorize(Roles = "Manager")]
    public class ManagerController : Controller
    {
        private readonly ManagingAgriculture.Data.ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ManagerController(ManagingAgriculture.Data.ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Manager sees their own tasks + tasks they created for employees
            var myTasks = await _context.TaskAssignments
                .Include(t => t.AssignedMachinery)
                .Include(t => t.AssignedToUser)
                .Where(t => t.CompanyId == user.CompanyId)
                .OrderByDescending(t => t.AssignedDate)
                .ToListAsync();

            // Get leave requests for this manager
            var leaveRequests = await _context.LeaveRequests
                .Where(l => l.UserId == user.Id)
                .OrderByDescending(l => l.LeaveDate)
                .ToListAsync();

            var approvedDaysOff = leaveRequests.Count(l => l.Status == "Approved");
            ViewBag.LeaveDaysUsed = approvedDaysOff;
            ViewBag.LeaveDaysTotal = user.LeaveDaysTotal;
            ViewBag.LeaveDaysRemaining = user.LeaveDaysTotal - approvedDaysOff;

            ViewBag.LeaveRequests = leaveRequests;
            ViewBag.Salary = user.Salary;
            ViewBag.IsSalaryPaid = user.IsSalaryPaidInfo;
            ViewBag.UserName = user.Email;

            // Get employees in this company for task assignment
            var employees = await _userManager.GetUsersInRoleAsync("Employee");
            ViewBag.Employees = employees.Where(e => e.CompanyId == user.CompanyId).ToList();

            // Get available machinery (not currently assigned to an active task)
            var busyMachineryIds = await _context.TaskAssignments
                .Where(t => t.CompanyId == user.CompanyId
                         && t.AssignedMachineryId.HasValue
                         && !t.IsApprovedByBoss)
                .Select(t => t.AssignedMachineryId!.Value)
                .Distinct()
                .ToListAsync();

            ViewBag.Machinery = await _context.Machinery
                .Where(m => m.CompanyId == user.CompanyId && !busyMachineryIds.Contains(m.Id))
                .ToListAsync();

            return View(myTasks);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTaskToEmployee(string userId, string description, int? machineryId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var emp = await _userManager.FindByIdAsync(userId);
            if (emp == null || emp.CompanyId != user.CompanyId)
            {
                TempData["Error"] = "Employee not found in your company.";
                return RedirectToAction(nameof(Index));
            }

            // Check if machinery is already assigned to a pending task
            if (machineryId.HasValue)
            {
                var machineryInUse = await _context.TaskAssignments
                    .AnyAsync(t => t.AssignedMachineryId == machineryId && !t.IsApprovedByBoss && t.CompanyId == user.CompanyId);
                if (machineryInUse)
                {
                    TempData["Error"] = "That machine is already assigned to another task. Choose a different machine.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var task = new TaskAssignment
            {
                AssignedToUserId = userId,
                CompanyId = user.CompanyId,
                Description = description,
                AssignedDate = DateTime.UtcNow,
                IsCompletedByEmployee = false,
                IsApprovedByBoss = false,
                AssignedMachineryId = machineryId,
                AssignedByUserId = user.Id
            };
            _context.TaskAssignments.Add(task);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Task assigned to employee.";
            return RedirectToAction(nameof(Index));
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
                TempData["Success"] = "Task marked as complete. Boss will review it.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveEmployeeTask(int taskId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var task = await _context.TaskAssignments.FindAsync(taskId);
            if (task != null && task.CompanyId == user.CompanyId && task.AssignedByUserId == user.Id)
            {
                // Manager can approve tasks they created for employees
                task.IsApprovedByBoss = true;
                task.CompletedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Task approved.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestLeave(DateTime leaveDate, string? reason)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (leaveDate.DayOfWeek == DayOfWeek.Saturday || leaveDate.DayOfWeek == DayOfWeek.Sunday)
            {
                TempData["Error"] = "You cannot request leave on weekends.";
                return RedirectToAction(nameof(Index));
            }

            if (IsBulgarianHoliday(leaveDate))
            {
                TempData["Error"] = "You cannot request leave on official Bulgarian holidays.";
                return RedirectToAction(nameof(Index));
            }

            var existing = await _context.LeaveRequests
                .FirstOrDefaultAsync(l => l.UserId == user.Id && l.LeaveDate.Date == leaveDate.Date);
            if (existing != null)
            {
                TempData["Error"] = "You already have a leave request for that date.";
                return RedirectToAction(nameof(Index));
            }

            var approvedDays = await _context.LeaveRequests
                .CountAsync(l => l.UserId == user.Id && l.Status == "Approved");
            if (approvedDays >= user.LeaveDaysTotal)
            {
                TempData["Error"] = "You have no remaining leave days.";
                return RedirectToAction(nameof(Index));
            }

            if (user.CompanyId.HasValue)
            {
                var sameDayApproved = await _context.LeaveRequests
                    .AnyAsync(l => l.CompanyId == user.CompanyId && l.LeaveDate.Date == leaveDate.Date && l.Status == "Approved");
                if (sameDayApproved)
                {
                    TempData["Error"] = "That date is already taken by another employee/manager.";
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

            TempData["Success"] = "Leave request submitted. Awaiting boss approval.";
            return RedirectToAction(nameof(Index));
        }

        private bool IsBulgarianHoliday(DateTime date)
        {
            var holidays = new HashSet<(int month, int day)>
            {
                (1, 1), (3, 3), (5, 1), (5, 6), (5, 24),
                (9, 6), (9, 22), (11, 1), (12, 24), (12, 25), (12, 26),
            };
            return holidays.Contains((date.Month, date.Day));
        }
    }
}
