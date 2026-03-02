using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ManagingAgriculture.Controllers;
using ManagingAgriculture.Data;
using ManagingAgriculture.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ManagingAgriculture.Tests.Controllers
{
    public class EmployeeControllerTests
    {
        private Mock<UserManager<ApplicationUser>> GetMockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
        }

        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private void SetupController(Controller controller, string userId, string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var principal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            var tempData = new TempDataDictionary(controller.ControllerContext.HttpContext, Mock.Of<ITempDataProvider>());
            controller.TempData = tempData;
        }

        [Fact]
        public async Task Index_ReturnsViewWithTasksAndLeaveRequests()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "emp1", LeaveDaysTotal = 20, Salary = 2000, Email = "emp1@test.com" };
            db.Users.Add(user);
            db.TaskAssignments.Add(new TaskAssignment { Id = 1, AssignedToUserId = "emp1", Description = "Task 1", AssignedDate = DateTime.UtcNow });
            db.LeaveRequests.Add(new LeaveRequest { Id = 1, UserId = "emp1", LeaveDate = DateTime.UtcNow.AddDays(7), Status = "Pending" });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new EmployeeController(db, userManager.Object);
            SetupController(controller, "emp1", "Employee");

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<TaskAssignment>>(viewResult.Model);
            Assert.Single(model);
            Assert.Equal(20, controller.ViewBag.LeaveDaysTotal);
        }

        [Fact]
        public async Task CompleteTask_UpdatesTaskStatus()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "emp1" };
            db.Users.Add(user);
            db.TaskAssignments.Add(new TaskAssignment { Id = 1, AssignedToUserId = "emp1", IsCompletedByEmployee = false });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new EmployeeController(db, userManager.Object);
            SetupController(controller, "emp1", "Employee");

            // Act
            var result = await controller.CompleteTask(1);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            var task = await db.TaskAssignments.FindAsync(1);
            Assert.True(task.IsCompletedByEmployee);
        }

        [Fact]
        public async Task RequestLeave_CreatesRequest_WhenValid()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "emp1", LeaveDaysTotal = 20, CompanyId = 1 };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new EmployeeController(db, userManager.Object);
            SetupController(controller, "emp1", "Employee");

            var nextMonday = DateTime.Today;
            while (nextMonday.DayOfWeek != DayOfWeek.Monday || nextMonday.Month == 3 && nextMonday.Day == 3) // Avoid Liberation Day
            {
                nextMonday = nextMonday.AddDays(1);
            }

            // Act
            var result = await controller.RequestLeave(nextMonday, "Vacation");

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(1, await db.LeaveRequests.CountAsync(l => l.UserId == "emp1"));
        }

        [Fact]
        public async Task RequestLeave_Fails_OnWeekend()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "emp1" };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new EmployeeController(db, userManager.Object);
            SetupController(controller, "emp1", "Employee");

            var nextSunday = DateTime.Today;
            while (nextSunday.DayOfWeek != DayOfWeek.Sunday)
            {
                nextSunday = nextSunday.AddDays(1);
            }

            // Act
            var result = await controller.RequestLeave(nextSunday, "Rest");

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.True(controller.TempData.ContainsKey("Error"));
            Assert.Contains("weekends", controller.TempData["Error"].ToString());
        }
    }
}
