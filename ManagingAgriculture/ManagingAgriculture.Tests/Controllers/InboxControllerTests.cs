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
    public class InboxControllerTests
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
        public async Task Index_ReturnsAdminInboxForAdmin()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var admin = new ApplicationUser { Id = "admin1" };
            db.ContactForms.Add(new ContactForm { Id = 1, Message = "Test" });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(admin);

            var controller = new InboxController(db, userManager.Object);
            SetupController(controller, "admin1", "SystemAdmin");

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("AdminInbox", viewResult.ViewName);
            var model = Assert.IsAssignableFrom<IEnumerable<ContactForm>>(viewResult.Model);
            Assert.Single(model);
        }

        [Fact]
        public async Task Index_ReturnsUserInboxForUser()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "user1", Email = "bob@test.com" };
            db.ContactForms.Add(new ContactForm { Id = 1, Email = "bob@test.com", Message = "My Message" });
            db.ContactForms.Add(new ContactForm { Id = 2, Email = "other@test.com", Message = "Other Message" });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new InboxController(db, userManager.Object);
            SetupController(controller, "user1", "User");

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<ContactForm>>(viewResult.Model);
            Assert.Single(model);
            Assert.Equal("My Message", model.First().Message);
        }

        [Fact]
        public async Task Reply_UpdatesMessageStatus()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var admin = new ApplicationUser { Id = "admin1" };
            db.ContactForms.Add(new ContactForm { Id = 1, Message = "Test", IsReplied = false });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(admin);

            var controller = new InboxController(db, userManager.Object);
            SetupController(controller, "admin1", "SystemAdmin");

            // Act
            var result = await controller.Reply(1, "Fixed");

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            var msg = await db.ContactForms.FindAsync(1);
            Assert.True(msg.IsReplied);
            Assert.Equal("Fixed", msg.ReplyMessage);
            Assert.Equal("Admin", msg.RepliedBy);
        }
    }
}
