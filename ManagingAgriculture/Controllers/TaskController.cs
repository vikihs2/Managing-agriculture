using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagingAgriculture.Models;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace ManagingAgriculture.Controllers
{
    [Authorize]
    public class TaskController : Controller
    {
        private readonly ManagingAgriculture.Data.ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TaskController(ManagingAgriculture.Data.ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            List<TaskAssignment> tasks = new List<TaskAssignment>();

            if (User.IsInRole("Boss"))
            {
                // Boss sees all tasks in their company
                tasks = await _context.TaskAssignments
                    .Where(t => t.CompanyId == user.CompanyId)
                    .OrderByDescending(t => t.AssignedDate)
                    .ToListAsync();
            }
            else if (User.IsInRole("Manager"))
            {
                // Manager sees:
                // 1. Tasks assigned to them
                // 2. Tasks assigned to their employees (where they are the assigner's manager)
                var managerTasks = await _context.TaskAssignments
                    .Where(t => t.AssignedToUserId == user.Id && t.CompanyId == user.CompanyId)
                    .ToListAsync();

                // Get employees managed by this manager (simplified - assumes manager is in same company)
                var employees = await _userManager.GetUsersInRoleAsync("Employee");
                var employeeIds = employees
                    .Where(e => e.CompanyId == user.CompanyId)
                    .Select(e => e.Id)
                    .ToList();

                var employeeTasks = await _context.TaskAssignments
                    .Where(t => employeeIds.Contains(t.AssignedToUserId) && t.CompanyId == user.CompanyId)
                    .ToListAsync();

                tasks = managerTasks.Concat(employeeTasks)
                    .OrderByDescending(t => t.AssignedDate)
                    .ToList();
            }
            else
            {
                // Employee sees only tasks assigned to them
                tasks = await _context.TaskAssignments
                    .Where(t => t.AssignedToUserId == user.Id)
                    .OrderByDescending(t => t.AssignedDate)
                    .ToListAsync();
            }

            return View(tasks);
        }

        [HttpPost]
        public async Task<IActionResult> CompleteTask(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var task = await _context.TaskAssignments.FindAsync(id);

            if (task != null && task.AssignedToUserId == user.Id)
            {
                task.IsCompletedByEmployee = true;
                // If boss auto-approval is not required, we stop here. 
                // Currently Boss needs to approve.
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
