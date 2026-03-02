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
    public class MachineryControllerTests
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

            controller.TempData = new TempDataDictionary(controller.ControllerContext.HttpContext, Mock.Of<ITempDataProvider>());
        }

        [Fact]
        public async Task Index_ReturnsViewWithMachinery()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "user1", CompanyId = 1 };
            db.Machinery.Add(new Machinery { Id = 1, Name = "Tractor", CompanyId = 1 });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new MachineryController(db, userManager.Object);
            SetupController(controller, "user1", "Boss", 1);

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<Machinery>>(viewResult.Model);
            Assert.Single(model);
        }

        [Fact]
        public async Task Add_Post_RedirectsOnSuccess()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "boss1", CompanyId = 1 };
            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new MachineryController(db, userManager.Object);
            SetupController(controller, "boss1", "Boss", 1);

            var newMach = new Machinery { Name = "New Tractor", Type = "Tractor" };

            // Act
            var result = await controller.Add(newMach);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal(1, db.Machinery.Count());
        }

        [Fact]
        public async Task Edit_Get_ReturnsNotFound_WhenMachineryDoesNotExist()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "boss1", CompanyId = 1 };
            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new MachineryController(db, userManager.Object);
            SetupController(controller, "boss1", "Boss", 1);

            // Act
            var result = await controller.Edit(999);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Delete_Post_Fails_IfListedOnMarketplace()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "boss1", CompanyId = 1 };
            db.Machinery.Add(new Machinery { Id = 1, Name = "Tractor", CompanyId = 1 });
            db.MarketplaceListings.Add(new MarketplaceListing { Id = 1, MachineryId = 1, ListingStatus = "Active" });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new MachineryController(db, userManager.Object);
            SetupController(controller, "boss1", "Boss", 1);

            // Act
            var result = await controller.Delete(1);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.True(controller.TempData.ContainsKey("Error"));
            Assert.Equal(1, db.Machinery.Count());
        }
    }
}
