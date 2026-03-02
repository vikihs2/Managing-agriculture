using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ManagingAgriculture.Controllers;
using ManagingAgriculture.Data;
using ManagingAgriculture.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ManagingAgriculture.Tests.Controllers
{
    public class ITSupportControllerTests
    {
        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task Index_ReturnsViewWithMessages()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            db.ContactForms.Add(new ContactForm { Id = 1, Message = "Broken", CreatedDate = DateTime.Now });
            await db.SaveChangesAsync();

            var controller = new ITSupportController(db);

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<ContactForm>>(viewResult.Model);
            Assert.Single(model);
        }

        [Fact]
        public async Task RunDiagnostics_ReturnsSuccessJson()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            db.Users.Add(new ApplicationUser { Id = "u1" });
            await db.SaveChangesAsync();

            var controller = new ITSupportController(db);

            // Act
            var result = await controller.RunDiagnostics();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var successProp = jsonResult.Value.GetType().GetProperty("success");
            var successValue = (bool)successProp.GetValue(jsonResult.Value);
            Assert.True(successValue);
            var diagnosticsProp = jsonResult.Value.GetType().GetProperty("diagnostics");
            Assert.NotNull(diagnosticsProp.GetValue(jsonResult.Value));
        }

        [Fact]
        public async Task ReplyToMessage_UpdatesMessageStatus()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            db.ContactForms.Add(new ContactForm { Id = 1, IsReplied = false });
            await db.SaveChangesAsync();

            var controller = new ITSupportController(db);

            // Act
            var result = await controller.ReplyToMessage(1, "Fixed");

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            var msg = await db.ContactForms.FindAsync(1);
            Assert.True(msg.IsReplied);
            Assert.Equal("Fixed", msg.ReplyMessage);
        }
    }
}
