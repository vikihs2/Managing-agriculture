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
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ManagingAgriculture.Tests.Controllers
{
    public class TaskControllerTests
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
        }

        [Fact]
        public async Task Index_ReturnsViewForBoss()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var boss = new ApplicationUser { Id = "boss1", CompanyId = 1 };
            db.Users.Add(boss);
            db.TaskAssignments.Add(new TaskAssignment { Id = 1, CompanyId = 1, Description = "Boss Task", AssignedDate = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(boss);

            var controller = new TaskController(db, userManager.Object);
            SetupController(controller, "boss1", "Boss");

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<TaskAssignment>>(viewResult.Model);
            Assert.Single(model);
        }

        [Fact]
        public async Task Index_ReturnsViewForEmployee()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var emp = new ApplicationUser { Id = "emp1", CompanyId = 1 };
            db.Users.Add(emp);
            db.TaskAssignments.Add(new TaskAssignment { Id = 2, AssignedToUserId = "emp1", Description = "Emp Task", AssignedDate = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(emp);

            var controller = new TaskController(db, userManager.Object);
            SetupController(controller, "emp1", "Employee");

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<TaskAssignment>>(viewResult.Model);
            Assert.Single(model);
        }

        [Fact]
        public async Task CompleteTask_UpdatesTask()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var emp = new ApplicationUser { Id = "emp1" };
            db.TaskAssignments.Add(new TaskAssignment { Id = 1, AssignedToUserId = "emp1", IsCompletedByEmployee = false });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(emp);

            var controller = new TaskController(db, userManager.Object);
            SetupController(controller, "emp1", "Employee");

            // Act
            var result = await controller.CompleteTask(1);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            var task = await db.TaskAssignments.FindAsync(1);
            Assert.True(task.IsCompletedByEmployee);
        }
    }
}
