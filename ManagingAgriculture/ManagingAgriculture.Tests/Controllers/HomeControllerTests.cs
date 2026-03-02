using System;
using System.Collections.Generic;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ManagingAgriculture.Tests.Controllers
{
    public class HomeControllerTests
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

        private void SetupController(Controller controller, string userId = null)
        {
            var httpContext = new DefaultHttpContext();
            if (userId != null)
            {
                var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId) };
                httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));
            }
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        }

        [Fact]
        public void Index_ReturnsView()
        {
            // Arrange
            var config = new Mock<IConfiguration>();
            var logger = new Mock<ILogger<HomeController>>();
            using var db = GetInMemoryDbContext();
            var userManager = GetMockUserManager();
            var controller = new HomeController(config.Object, logger.Object, db, userManager.Object);

            // Act
            var result = controller.Index();

            // Assert
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task Contact_Post_SavesToDatabase()
        {
            // Arrange
            var config = new Mock<IConfiguration>();
            var logger = new Mock<ILogger<HomeController>>();
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "u1", Email = "test@test.com" };
            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new HomeController(config.Object, logger.Object, db, userManager.Object);
            SetupController(controller, "u1");

            var form = new ContactForm { Message = "Help" };

            // Act
            var result = await controller.Contact(form);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(1, await db.ContactForms.CountAsync());
            var savedForm = await db.ContactForms.FirstAsync();
            Assert.Equal("test@test.com", savedForm.Email);
        }
    }
}
