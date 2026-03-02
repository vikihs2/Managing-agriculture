using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ManagingAgriculture.Controllers;
using ManagingAgriculture.Data;
using ManagingAgriculture.Models;
using ManagingAgriculture.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ManagingAgriculture.Tests.Controllers
{
    public class DashboardControllerTests
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
        public async Task Index_ReturnsViewWithDashboardStats()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "user1", Email = "user1@test.com", CompanyId = 1 };
            db.Users.Add(user);
            db.Companies.Add(new Company { Id = 1, Name = "Test Company" });
            db.Plants.Add(new Plant { Id = 1, CompanyId = 1, Status = "Growing" });
            db.Resources.Add(new Resource { Id = 1, CompanyId = 1, Quantity = 5, LowStockThreshold = 10 }); // Low stock
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new DashboardController(db, userManager.Object);
            SetupController(controller, "user1", "User");

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<DashboardViewModel>(viewResult.Model);
            Assert.Equal(1, model.ActivePlantsCount);
            Assert.Equal(1, model.LowStockCount);
            Assert.Equal("Test Company", controller.ViewBag.CompanyName);
        }

        [Fact]
        public async Task AcceptInvite_UpdatesUserCompanyAndRole()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "user1", Email = "user1@test.com" };
            db.Users.Add(user);
            
            var invite = new CompanyInvitation 
            { 
                Id = 1, 
                Email = "user1@test.com", 
                CompanyId = 10, 
                Role = "Manager", 
                Salary = 5000, 
                LeaveDays = 25,
                IsUsed = false 
            };
            db.CompanyInvitations.Add(invite);
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
            userManager.Setup(u => u.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
            userManager.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });
            userManager.Setup(u => u.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
            userManager.Setup(u => u.AddToRoleAsync(user, "Manager")).ReturnsAsync(IdentityResult.Success);

            var controller = new DashboardController(db, userManager.Object);
            SetupController(controller, "user1", "User");

            // Act
            var result = await controller.AcceptInvite(1);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(10, user.CompanyId);
            Assert.Equal(5000, user.Salary);
            Assert.True(invite.IsUsed);
        }
    }
}
