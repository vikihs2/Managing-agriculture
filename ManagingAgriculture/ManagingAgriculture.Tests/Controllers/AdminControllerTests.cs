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
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ManagingAgriculture.Tests.Controllers
{
    public class AdminControllerTests
    {
        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private UserManager<ApplicationUser> GetRealUserManager(ApplicationDbContext context)
        {
            var userStore = new UserStore<ApplicationUser>(context);
            var options = new Mock<Microsoft.Extensions.Options.IOptions<IdentityOptions>>();
            options.Setup(o => o.Value).Returns(new IdentityOptions());
            var userManager = new UserManager<ApplicationUser>(userStore, options.Object, new PasswordHasher<ApplicationUser>(), null, null, null, null, null, null);
            return userManager;
        }

        private RoleManager<IdentityRole> GetRealRoleManager(ApplicationDbContext context)
        {
            var roleStore = new RoleStore<IdentityRole>(context);
            var roleManager = new RoleManager<IdentityRole>(roleStore, null, null, null, null);
            return roleManager;
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

            controller.TempData = new TempDataDictionary(controller.ControllerContext.HttpContext, Mock.Of<ITempDataProvider>());
        }

        [Fact]
        public async Task Index_ReturnsDashboardData_Wait_ThisMightStillNeedAsyncWorkaroundButLetsTry()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var userManager = GetRealUserManager(db);
            var roleManager = GetRealRoleManager(db);

            var admin = new ApplicationUser { Id = "admin1", UserName = "admin@test.com", Email = "admin@test.com", FirstName = "Admin" };
            await userManager.CreateAsync(admin);
            await roleManager.CreateAsync(new IdentityRole("SystemAdmin"));
            await userManager.AddToRoleAsync(admin, "SystemAdmin");

            db.Companies.Add(new Company { Id = 1, Name = "Test Co" });
            db.Plants.Add(new Plant { Id = 1, Name = "Corn", CompanyId = 1 });
            await db.SaveChangesAsync();

            var controller = new AdminController(db, userManager, roleManager);
            SetupController(controller, "admin1", "SystemAdmin");

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AdminDashboardViewModel>(viewResult.Model);
            Assert.Equal(1, model.TotalUsers);
            Assert.Equal(1, model.TotalCompanies);
        }

        [Fact]
        public async Task Users_ReturnsListWithRoles()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var userManager = GetRealUserManager(db);
            var roleManager = GetRealRoleManager(db);

            var admin = new ApplicationUser { Id = "admin1", UserName = "admin@test.com", Email = "admin@test.com", FirstName = "Admin" };
            await userManager.CreateAsync(admin);
            await roleManager.CreateAsync(new IdentityRole("SystemAdmin"));
            await userManager.AddToRoleAsync(admin, "SystemAdmin");

            var user = new ApplicationUser { Id = "user1", UserName = "user@test.com", Email = "user@test.com", FirstName = "User" };
            await userManager.CreateAsync(user);

            var controller = new AdminController(db, userManager, roleManager);
            SetupController(controller, "admin1", "SystemAdmin");

            // Act
            var result = await controller.Users(null, null);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<UserManagementViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
        }
    }
}
