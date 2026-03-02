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
    public class ManagerControllerTests
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

        private void SetupController(Controller controller, string userId, string role, int? companyId = null)
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
        public async Task Index_ReturnsViewWithTasks()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "mgr1", CompanyId = 1, Salary = 3000, LeaveDaysTotal = 25 };
            db.Users.Add(user);
            db.TaskAssignments.Add(new TaskAssignment { Id = 1, CompanyId = 1, Description = "Task 1", AssignedDate = DateTime.UtcNow, AssignedToUserId = "mgr1" });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
            userManager.Setup(u => u.GetUsersInRoleAsync("Employee")).ReturnsAsync(new List<ApplicationUser>());

            var controller = new ManagerController(db, userManager.Object);
            SetupController(controller, "mgr1", "Manager", 1);

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<TaskAssignment>>(viewResult.Model);
            Assert.Single(model);
        }

        [Fact]
        public async Task AssignTaskToEmployee_CreatesTask_WhenSuccessful()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var manager = new ApplicationUser { Id = "mgr1", CompanyId = 1 };
            var emp = new ApplicationUser { Id = "emp1", CompanyId = 1 };
            db.Users.Add(manager);
            db.Users.Add(emp);
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(manager);
            userManager.Setup(u => u.FindByIdAsync("emp1")).ReturnsAsync(emp);

            var controller = new ManagerController(db, userManager.Object);
            SetupController(controller, "mgr1", "Manager", 1);

            // Act
            var result = await controller.AssignTaskToEmployee("emp1", "Manager Assign Task", null);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(1, await db.TaskAssignments.CountAsync(t => t.Description == "Manager Assign Task"));
        }

        [Fact]
        public async Task RequestLeave_CreatesRequest_WhenValid()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var manager = new ApplicationUser { Id = "mgr1", CompanyId = 1, LeaveDaysTotal = 20 };
            db.Users.Add(manager);
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(manager);

            var controller = new ManagerController(db, userManager.Object);
            SetupController(controller, "mgr1", "Manager", 1);

            var mondayDate = new DateTime(2025, 3, 10); // A Monday, not a holiday

            // Act
            var result = await controller.RequestLeave(mondayDate, "Rest");

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(1, await db.LeaveRequests.CountAsync(l => l.UserId == "mgr1"));
        }

        [Fact]
        public async Task RequestLeave_Fails_OnWeekend()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var manager = new ApplicationUser { Id = "mgr1", CompanyId = 1 };
            db.Users.Add(manager);
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(manager);

            var controller = new ManagerController(db, userManager.Object);
            SetupController(controller, "mgr1", "Manager", 1);

            var sunday = new DateTime(2025, 3, 2); // A Sunday

            // Act
            var result = await controller.RequestLeave(sunday, "Rest");

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.True(controller.TempData.ContainsKey("Error"));
            Assert.Contains("weekends", controller.TempData["Error"].ToString());
            Assert.Equal(0, await db.LeaveRequests.CountAsync());
        }
    }
}
