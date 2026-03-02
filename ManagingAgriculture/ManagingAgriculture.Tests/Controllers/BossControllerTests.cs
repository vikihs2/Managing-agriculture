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
    public class BossControllerTests
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
        public async Task ManageCompany_ReturnsViewWithCompany()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var company = new Company { Id = 1, Name = "Test Company" };
            db.Companies.Add(company);
            var user = new ApplicationUser { Id = "boss1", CompanyId = 1 };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new BossController(db, userManager.Object);
            SetupController(controller, "boss1", "Boss", 1);

            // Act
            var result = await controller.ManageCompany();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Company>(viewResult.Model);
            Assert.Equal("Test Company", model.Name);
        }

        [Fact]
        public async Task ManageStaff_ReturnsViewWithEmployees()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "boss1", CompanyId = 1 };
            db.Users.Add(user);
            db.Users.Add(new ApplicationUser { Id = "emp1", CompanyId = 1, UserName = "emp1@test.com" });
            db.Users.Add(new ApplicationUser { Id = "emp2", CompanyId = 2, UserName = "emp2@test.com" });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new BossController(db, userManager.Object);
            SetupController(controller, "boss1", "Boss", 1);

            // Act
            var result = await controller.ManageStaff();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<ApplicationUser>>(viewResult.Model);
            Assert.Single(model);
            Assert.Equal("emp1", model.First().Id);
        }

        [Fact]
        public async Task InviteStaff_CreatesInvitation()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "boss1", CompanyId = 1 };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new BossController(db, userManager.Object);
            SetupController(controller, "boss1", "Boss", 1);

            // Act
            var result = await controller.InviteStaff("new@test.com", "Employee", 2000, 20);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("ManageStaff", redirectResult.ActionName);
            Assert.Equal(1, await db.CompanyInvitations.CountAsync());
            var invite = await db.CompanyInvitations.FirstAsync();
            Assert.Equal("new@test.com", invite.Email);
        }

        [Fact]
        public async Task AssignTask_CreatesTask()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var boss = new ApplicationUser { Id = "boss1", CompanyId = 1 };
            var emp = new ApplicationUser { Id = "emp1", CompanyId = 1 };
            db.Users.Add(boss);
            db.Users.Add(emp);
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(boss);
            userManager.Setup(u => u.FindByIdAsync("emp1")).ReturnsAsync(emp);

            var controller = new BossController(db, userManager.Object);
            SetupController(controller, "boss1", "Boss", 1);

            // Act
            var result = await controller.AssignTask("emp1", "Do work");

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(1, await db.TaskAssignments.CountAsync());
            var task = await db.TaskAssignments.FirstAsync();
            Assert.Equal("Do work", task.Description);
            Assert.Equal("emp1", task.AssignedToUserId);
        }

        [Fact]
        public async Task ApproveLeaveRequest_UpdatesStatusAndCounters()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var boss = new ApplicationUser { Id = "boss1", CompanyId = 1 };
            var emp = new ApplicationUser { Id = "emp1", CompanyId = 1, LeaveDaysUsed = 5 };
            db.Users.Add(boss);
            db.Users.Add(emp);
            
            var request = new LeaveRequest { Id = 1, UserId = "emp1", CompanyId = 1, Status = "Pending", LeaveDate = DateTime.Today.AddDays(1) };
            db.LeaveRequests.Add(request);
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(boss);
            userManager.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

            var controller = new BossController(db, userManager.Object);
            SetupController(controller, "boss1", "Boss", 1);

            // Act
            var result = await controller.ApproveLeaveRequest(1);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            var updatedRequest = await db.LeaveRequests.FindAsync(1);
            Assert.Equal("Approved", updatedRequest.Status);
            Assert.Equal(6, emp.LeaveDaysUsed);
        }

        [Fact]
        public async Task RemoveStaff_ResetsUserCompany()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var emp = new ApplicationUser { Id = "emp1", CompanyId = 1, Salary = 1000 };
            db.Users.Add(emp);
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.FindByIdAsync("emp1")).ReturnsAsync(emp);
            userManager.Setup(u => u.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(new List<string> { "Employee" });
            userManager.Setup(u => u.RemoveFromRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
            userManager.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "User")).ReturnsAsync(IdentityResult.Success);

            var controller = new BossController(db, userManager.Object);
            SetupController(controller, "boss1", "Boss", 1);

            // Act
            var result = await controller.RemoveStaff("emp1");

            // Assert
            Assert.Null(emp.CompanyId);
            Assert.Equal(0, emp.Salary);
            userManager.Verify(u => u.UpdateAsync(emp), Times.Once);
        }
    }
}
