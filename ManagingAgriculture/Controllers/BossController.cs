using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagingAgriculture.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ManagingAgriculture.Controllers
{
    [Authorize(Roles = "Boss")]
    public class BossController : Controller
    {
        private readonly ManagingAgriculture.Data.ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BossController(ManagingAgriculture.Data.ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index() => View();

        public async Task<IActionResult> ManageCompany()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.CompanyId == null) return NotFound("Company not found.");
            var company = await _context.Companies.FindAsync(user.CompanyId);
            return View(company);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCompany(int id, string name, Microsoft.AspNetCore.Http.IFormFile? logo)
        {
            var company = await _context.Companies.FindAsync(id);
            if (company != null)
            {
                company.Name = name;
                
                if (logo != null && logo.Length > 0)
                {
                    // Save Logo
                    var fileName = Guid.NewGuid().ToString() + System.IO.Path.GetExtension(logo.FileName);
                    var filePath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot/images/companies", fileName);
                    
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath)!);
                    
                    using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
                    {
                        await logo.CopyToAsync(stream);
                    }
                    company.LogoPath = "/images/companies/" + fileName;
                }

                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(ManageCompany));
        }

        public async Task<IActionResult> ManageStaff()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.CompanyId == null) return NotFound("Company not found.");

            // Get Employees and Managers (exclude self)
            // Efficient way: query Users directly to include new fields if needed
            var companyUsers = await _context.Users
                                     .Where(u => u.CompanyId == user.CompanyId && u.Id != user.Id)
                                     .ToListAsync();
            
            // Also get pending invitations
            ViewBag.Invitations = await _context.CompanyInvitations
                                        .Where(i => i.CompanyId == user.CompanyId && !i.IsUsed)
                                        .ToListAsync();
            
            // Get Task Assignments for context if needed, or separate view
            // For now, let's keep ManageStaff simple and add tasks there or link to them
            
            return View(companyUsers);
        }

        [HttpPost]
        public async Task<IActionResult> InviteStaff(string email, string role, decimal salary, int leaveDays)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.CompanyId == null) return Challenge();

            var existingInvite = await _context.CompanyInvitations
                .FirstOrDefaultAsync(i => i.Email == email && i.CompanyId == currentUser.CompanyId && !i.IsUsed);

            if (existingInvite == null)
            {
                var invite = new CompanyInvitation
                {
                    Email = email,
                    CompanyId = currentUser.CompanyId.Value,
                    Role = role,
                    Token = Guid.NewGuid().ToString(),
                    Salary = salary, // New
                    LeaveDays = leaveDays // New
                };
                _context.CompanyInvitations.Add(invite);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(ManageStaff));
        }

        [HttpPost]
        public async Task<IActionResult> CancelInvitation(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.CompanyId == null) return Challenge();

            var invite = await _context.CompanyInvitations.FindAsync(id);
            if (invite != null && invite.CompanyId == currentUser.CompanyId)
            {
                _context.CompanyInvitations.Remove(invite);
                await _context.SaveChangesAsync();
            }
             return RedirectToAction(nameof(ManageStaff));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateEmployeeDetails(string userId, decimal salary, int leaveDays, bool isSalaryPaid)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            // Verify boss (Authorize attr handles role, logic checks company)
            var emp = await _userManager.FindByIdAsync(userId);
            
            if (emp != null && emp.CompanyId == currentUser.CompanyId)
            {
                emp.Salary = salary;
                // If leave days total is changed (e.g. raised from 20 to 25)
                emp.LeaveDaysTotal = leaveDays;
                emp.IsSalaryPaidInfo = isSalaryPaid;
                
                await _userManager.UpdateAsync(emp);
            }
            return RedirectToAction(nameof(ManageStaff));
        }

        [HttpPost]
        public async Task<IActionResult> AssignTask(string userId, string description, int? machineryId = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var emp = await _userManager.FindByIdAsync(userId);
            
            if (emp != null && emp.CompanyId == currentUser.CompanyId)
            {
                // Check if machinery is already in use
                if (machineryId.HasValue)
                {
                    var machineryInUse = await _context.TaskAssignments
                        .AnyAsync(t => t.AssignedMachineryId == machineryId && !t.IsApprovedByBoss && t.CompanyId == currentUser.CompanyId);
                    if (machineryInUse)
                    {
                        TempData["Error"] = "That machine is already assigned to an active task.";
                        return RedirectToAction(nameof(ManageStaff));
                    }
                }

                var task = new TaskAssignment
                {
                    AssignedToUserId = userId,
                    CompanyId = currentUser.CompanyId,
                    Description = description,
                    AssignedDate = DateTime.UtcNow,
                    IsCompletedByEmployee = false,
                    IsApprovedByBoss = false,
                    AssignedByUserId = currentUser.Id,
                    AssignedMachineryId = machineryId
                };
                _context.TaskAssignments.Add(task);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(ManageStaff));
        }
        
        [HttpPost]
        public async Task<IActionResult> VerifyTask(int taskId)
        {
             var currentUser = await _userManager.GetUserAsync(User);
             var task = await _context.TaskAssignments.FindAsync(taskId);
             if (task != null && task.CompanyId == currentUser.CompanyId)
             {
                 task.IsApprovedByBoss = true;
                 task.CompletedDate = DateTime.UtcNow;
                 await _context.SaveChangesAsync();
             }
             return RedirectToAction(nameof(ManageStaff)); // Or Task View
        }

        [HttpPost]
        public async Task<IActionResult> RemoveStaff(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.CompanyId = null;
                // clear employment details
                user.Salary = 0;
                user.LeaveDaysUsed = 0; // Reset? Or Keep history? Reset seems safer for clean break.
                
                await _userManager.UpdateAsync(user);
                
                var roles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, roles);
                await _userManager.AddToRoleAsync(user, "User");
            }
            return RedirectToAction(nameof(ManageStaff));
        }

        [HttpPost]
        public async Task<IActionResult> PromoteStaff(string userId, string newRole)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                var roles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, roles);
                await _userManager.AddToRoleAsync(user, newRole);
            }
            return RedirectToAction(nameof(ManageStaff));
        }
        
        // --- TASK MANAGEMENT ---

        [HttpGet]
        public async Task<IActionResult> GetEmployeeTasks(string userId)
        {
            var tasks = await _context.TaskAssignments
                .Where(t => t.AssignedToUserId == userId)
                .OrderByDescending(t => t.AssignedDate)
                .Select(t => new {
                    t.Id,
                    t.Description,
                    AssignedDate = t.AssignedDate.ToString("yyyy-MM-dd"),
                    t.IsCompletedByEmployee,
                    t.IsApprovedByBoss
                })
                .ToListAsync();

            return Json(tasks);
        }

        /// <summary>Returns machinery that is NOT currently assigned to an active (non-approved) task.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAvailableMachinery()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.CompanyId == null) return Json(new object[0]);

            // IDs that are currently locked by an active task (not yet approved = not done)
            var busyMachineryIds = await _context.TaskAssignments
                .Where(t => t.CompanyId == currentUser.CompanyId
                         && t.AssignedMachineryId.HasValue
                         && !t.IsApprovedByBoss)
                .Select(t => t.AssignedMachineryId!.Value)
                .Distinct()
                .ToListAsync();

            var activeListedMachineryIds = await _context.MarketplaceListings
                .Where(ml => ml.ListingStatus == "Active" && ml.MachineryId.HasValue)
                .Select(ml => ml.MachineryId!.Value)
                .Distinct()
                .ToListAsync();

            var available = await _context.Machinery
                .Where(m => m.CompanyId == currentUser.CompanyId && !busyMachineryIds.Contains(m.Id) && !activeListedMachineryIds.Contains(m.Id))
                .Select(m => new { m.Id, m.Name, m.Type, m.Status })
                .ToListAsync();

            return Json(available);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveTask(int taskId)
        {
            var task = await _context.TaskAssignments.FindAsync(taskId);
            if (task == null) return NotFound();
            
            task.IsApprovedByBoss = true;
            task.CompletedDate = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> CancelTask(int taskId)
        {
            var task = await _context.TaskAssignments.FindAsync(taskId);
            if (task == null) return NotFound();
            
            _context.TaskAssignments.Remove(task);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // --- DEMOTION ---
        [HttpPost]
        public async Task<IActionResult> DemoteStaff(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (user.CompanyId != currentUser.CompanyId) return Forbid();

            // Check if is Manager
            if (await _userManager.IsInRoleAsync(user, "Manager"))
            {
                await _userManager.RemoveFromRoleAsync(user, "Manager");
                await _userManager.AddToRoleAsync(user, "Employee");
            }

            return RedirectToAction(nameof(ManageStaff));
        }

        // --- LEAVE MANAGEMENT (CALENDAR) ---
        
        [HttpGet]
        public async Task<IActionResult> GetLeaveDates(string userId)
        {
            var leaves = await _context.LeaveRequests
                .Where(l => l.UserId == userId && l.Status != "Rejected")
                .Select(l => new {
                    title = "", // Removed text
                    start = l.LeaveDate.ToString("yyyy-MM-dd"),
                    color = l.Status == "Approved" ? "#dc3545" : "#ffffff", // Red if approved, white if pending
                    display = "background"
                })
                .ToListAsync();

            return Json(leaves);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleLeaveDate(string userId, DateTime date)
        {
             var user = await _userManager.FindByIdAsync(userId);
             if (user == null) return NotFound();

             var existingLeave = await _context.LeaveRecords
                 .FirstOrDefaultAsync(l => l.UserId == userId && l.LeaveDate.Date == date.Date);

             if (existingLeave != null)
             {
                 // Remove leave (Undo)
                 _context.LeaveRecords.Remove(existingLeave);
                 user.LeaveDaysUsed = Math.Max(0, user.LeaveDaysUsed - 1); 
             }
             else
             {
                 // Add leave
                 var leave = new LeaveRecord
                 {
                     UserId = userId,
                     LeaveDate = date,
                     Reason = "Boss Assigned"
                 };
                 _context.LeaveRecords.Add(leave);
                 user.LeaveDaysUsed++;
             }

             await _context.SaveChangesAsync();
             return Ok(new { newUsed = user.LeaveDaysUsed });
        }

        // --- FIELD MANAGEMENT ---

        [HttpGet]
        public async Task<IActionResult> ManageFields()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.CompanyId == null) return Challenge();

            var fields = await _context.Fields
                .Where(f => f.CompanyId == currentUser.CompanyId)
                .ToListAsync();

            return View(fields);
        }

        [HttpGet]
        public IActionResult CreateField()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateField(Field model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.CompanyId == null) return Challenge();

            var field = new Field
            {
                Name = model.Name,
                SizeInDecars = model.SizeInDecars,
                City = model.City,
                SoilType = model.SoilType,
                SunlightExposure = model.SunlightExposure,
                AverageTemperatureCelsius = model.AverageTemperatureCelsius,
                IsOccupied = false,
                CompanyId = currentUser.CompanyId,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            _context.Fields.Add(field);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ManageFields));
        }

        [HttpGet]
        public async Task<IActionResult> EditField(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.CompanyId == null) return Challenge();

            var field = await _context.Fields.FindAsync(id);
            if (field == null || field.CompanyId != currentUser.CompanyId) return NotFound();

            return View(field);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditField(int id, Field model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.CompanyId == null) return Challenge();

            var field = await _context.Fields.FindAsync(id);
            if (field == null || field.CompanyId != currentUser.CompanyId) return NotFound();

            field.Name = model.Name;
            field.SizeInDecars = model.SizeInDecars;
            field.City = model.City;
            field.SoilType = model.SoilType;
            field.SunlightExposure = model.SunlightExposure;
            field.AverageTemperatureCelsius = model.AverageTemperatureCelsius;
            field.UpdatedDate = DateTime.UtcNow;

            _context.Fields.Update(field);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ManageFields));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteField(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.CompanyId == null) return Challenge();

            var field = await _context.Fields.FindAsync(id);
            if (field == null || field.CompanyId != currentUser.CompanyId) return NotFound();

            if (field.IsOccupied)
            {
                TempData["Error"] = "Cannot delete an occupied field. Harvest the current plant first.";
                return RedirectToAction(nameof(ManageFields));
            }

            _context.Fields.Remove(field);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ManageFields));
        }

        // --- LEAVE REQUEST MANAGEMENT ---

        [HttpGet]
        public async Task<IActionResult> ManageLeaveRequests()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.CompanyId == null) return Challenge();

            var requests = await _context.LeaveRequests
                .Include(r => r.User)
                .Where(r => r.CompanyId == currentUser.CompanyId)
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();

            return View(requests);
        }

        [HttpGet]
        public async Task<IActionResult> GetLeaveRequests()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.CompanyId == null) return Json(new List<object>());

            var requests = await _context.LeaveRequests
                .Include(r => r.User)
                .Where(r => r.CompanyId == currentUser.CompanyId && r.Status == "Pending")
                .OrderBy(r => r.LeaveDate)
                .Select(r => new {
                    r.Id,
                    UserEmail = r.User!.Email,
                    LeaveDate = r.LeaveDate.ToString("yyyy-MM-dd"),
                    r.Reason,
                    r.Status
                })
                .ToListAsync();

            return Json(requests);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveLeaveRequest(int requestId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var request = await _context.LeaveRequests.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == requestId);
            if (request == null || request.CompanyId != currentUser.CompanyId) return NotFound();

            // Check if date is already taken by another approved leave in that company
            var alreadyTaken = await _context.LeaveRequests
                .AnyAsync(r => r.CompanyId == currentUser.CompanyId
                    && r.LeaveDate.Date == request.LeaveDate.Date
                    && r.Status == "Approved"
                    && r.Id != requestId);

            if (alreadyTaken)
            {
                TempData["Error"] = $"Cannot approve: another employee already has {request.LeaveDate:d} approved. Only one person can be off per day.";
                return RedirectToAction(nameof(ManageLeaveRequests));
            }

            request.Status = "Approved";
            request.DecidedDate = DateTime.UtcNow;

            // Update employee's used leave days
            if (request.User != null)
            {
                request.User.LeaveDaysUsed++;
                await _userManager.UpdateAsync(request.User);
            }

            // Reject all other pending requests for the same date in this company
            var conflicting = await _context.LeaveRequests
                .Where(r => r.CompanyId == currentUser.CompanyId
                    && r.LeaveDate.Date == request.LeaveDate.Date
                    && r.Status == "Pending"
                    && r.Id != requestId)
                .ToListAsync();

            foreach (var conflict in conflicting)
            {
                conflict.Status = "Rejected";
                conflict.DecidedDate = DateTime.UtcNow;
                conflict.BossNote = "Another employee was approved for this date.";
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Approved leave for {request.User?.Email} on {request.LeaveDate:d}.";
            return RedirectToAction(nameof(ManageLeaveRequests));
        }

        [HttpPost]
        public async Task<IActionResult> RejectLeaveRequest(int requestId, string? note)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var request = await _context.LeaveRequests.FindAsync(requestId);
            if (request == null || request.CompanyId != currentUser.CompanyId) return NotFound();

            request.Status = "Rejected";
            request.DecidedDate = DateTime.UtcNow;
            request.BossNote = note;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Leave request rejected.";
            return RedirectToAction(nameof(ManageLeaveRequests));
        }
    }
}
