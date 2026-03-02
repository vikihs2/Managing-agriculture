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
    public class ResourcesControllerTests
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
        public async Task Index_ReturnsViewWithResources()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "user1", CompanyId = 1 };
            db.Resources.Add(new Resource { Id = 1, Name = "Seeds", CompanyId = 1, Category = "Seeds" });
            db.Resources.Add(new Resource { Id = 2, Name = "Fuel", CompanyId = 2, Category = "Fuel" });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new ResourcesController(db, userManager.Object);
            SetupController(controller, "user1", "Boss", 1);

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<Resource>>(viewResult.Model);
            Assert.Single(model);
            Assert.Equal("Seeds", model.First().Name);
        }

        [Fact]
        public async Task Add_Post_RedirectsOnSuccess()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "boss1", CompanyId = 1 };
            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new ResourcesController(db, userManager.Object);
            SetupController(controller, "boss1", "Boss", 1);

            var newResource = new Resource { Name = "Fertilizer", Quantity = 100 };

            // Act
            var result = await controller.Add(newResource);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal(1, db.Resources.Count());
        }

        [Fact]
        public async Task AdjustQuantityAjax_UpdatesValue()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            db.Resources.Add(new Resource { Id = 1, Name = "Seeds", Quantity = 10m });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            var controller = new ResourcesController(db, userManager.Object);
            
            var req = new ResourcesController.AdjustRequest { Id = 1, Delta = 5 };

            // Act
            var result = await controller.AdjustQuantityAjax(req);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var successProp = jsonResult.Value.GetType().GetProperty("success");
            var quantityProp = jsonResult.Value.GetType().GetProperty("quantity");
            Assert.True((bool)successProp.GetValue(jsonResult.Value));
            Assert.Equal(15m, (decimal)quantityProp.GetValue(jsonResult.Value));
        }

        [Fact]
        public async Task Delete_Post_ReturnsForbid_WhenNotBoss()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "emp1", CompanyId = 1 };
            db.Resources.Add(new Resource { Id = 1, Name = "Seeds", CompanyId = 1 });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new ResourcesController(db, userManager.Object);
            SetupController(controller, "emp1", "Employee", 1);

            // Act
            var result = await controller.Delete(1);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.True(controller.TempData.ContainsKey("Error"));
            Assert.Equal(1, db.Resources.Count());
        }
    }
}
