using System;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ManagingAgriculture.Controllers;
using ManagingAgriculture.Data;
using ManagingAgriculture.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ManagingAgriculture.Tests.Controllers
{
    public class PlantsControllerTests
    {
        private Mock<UserManager<ApplicationUser>> GetMockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
        }

        private void SetupController(Controller controller, ApplicationUser user = null)
        {
            var httpContext = new DefaultHttpContext();
            if (user != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Email ?? "test"),
                    new Claim(ClaimTypes.NameIdentifier, user.Id)
                };
                var identity = new ClaimsIdentity(claims, "TestAuthType");
                httpContext.User = new ClaimsPrincipal(identity);
            }

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
            
            controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                httpContext, Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        }

        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task Index_ReturnsViewWithPlants()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "user1", CompanyId = 1 };
            context.Plants.Add(new Plant { Name = "Plant 1", CompanyId = 1, Status = "Active" });
            context.Plants.Add(new Plant { Name = "Plant 2", CompanyId = 1, Status = "Active" });
            await context.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new PlantsController(context, userManager.Object);
            SetupController(controller, user);

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<Plant>>(viewResult.Model);
            Assert.Equal(2, ((List<Plant>)model).Count);
        }

        [Fact]
        public async Task Add_Post_RedirectsToIndex_WhenSuccessful()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "user1", CompanyId = 1 };
            
            var field = new Field { Id = 1, Name = "Field 1", CompanyId = 1, SizeInDecars = 10, IsOccupied = false };
            context.Fields.Add(field);
            
            // Add some seeds to stock
            context.Resources.Add(new Resource { Name = "Wheat Seeds", Category = "Seed", Quantity = 5000, Unit = "kg", CompanyId = 1 });
            await context.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new PlantsController(context, userManager.Object);
            SetupController(controller, user);
            
            var model = new PlantCreateViewModel 
            { 
                Name = "Test Plant", 
                CropType = "Wheat", 
                FieldId = 1, 
                PlantedDate = DateTime.Today 
            };

            // Act
            var result = await controller.Add(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            
            // Verify plant was added
            Assert.Equal(1, await context.Plants.CountAsync());
            // Verify field is occupied
            var updatedField = await context.Fields.FindAsync(1);
            Assert.True(updatedField.IsOccupied);
        }

        [Fact]
        public async Task Add_Post_ReturnsError_WhenNotEnoughSeeds()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "user1", CompanyId = 1 };
            
            var field = new Field { Id = 2, Name = "Large Field", CompanyId = 1, SizeInDecars = 100, IsOccupied = false };
            context.Fields.Add(field);
            
            // Add only a few seeds
            context.Resources.Add(new Resource { Name = "Wheat Seeds", Category = "Seed", Quantity = 10, Unit = "kg", CompanyId = 1 });
            await context.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new PlantsController(context, userManager.Object);
            SetupController(controller, user);

            var model = new PlantCreateViewModel 
            { 
                Name = "Test Plant", 
                CropType = "Wheat", // Needs 200kg per decar
                FieldId = 2, 
                PlantedDate = DateTime.Today 
            };

            // Act
            var result = await controller.Add(model);

            // Assert
            Assert.IsType<ViewResult>(result);
            Assert.True(controller.TempData.ContainsKey("Error"));
            Assert.Contains("Not enough seeds", controller.TempData["Error"].ToString());
        }

        [Fact]
        public async Task Delete_Post_RemovesPlantAndFreesField_WhenUserIsBoss()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "boss1", CompanyId = 1 };
            var field = new Field { Id = 1, Name = "Field 1", CompanyId = 1, IsOccupied = true };
            context.Fields.Add(field);
            var plant = new Plant { Id = 1, Name = "Plant 1", CompanyId = 1, FieldId = 1 };
            context.Plants.Add(plant);
            await context.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new PlantsController(context, userManager.Object);
            SetupController(controller, user);
            
            // Mock role check for Boss
            var claims = new List<Claim> { new Claim(ClaimTypes.Role, "Boss"), new Claim(ClaimTypes.NameIdentifier, "boss1") };
            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = await controller.Delete(1);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal(0, await context.Plants.CountAsync());
            var updatedField = await context.Fields.FindAsync(1);
            Assert.False(updatedField.IsOccupied);
        }

        [Fact]
        public async Task Harvest_Post_Fails_WhenGrowthNot100()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "user1", CompanyId = 1 };
            var plant = new Plant { Id = 1, Name = "Young Plant", CompanyId = 1, GrowthStagePercent = 50 };
            context.Plants.Add(plant);
            await context.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new PlantsController(context, userManager.Object);
            SetupController(controller, user);

            // Act
            var result = await controller.Harvest(1);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.True(controller.TempData.ContainsKey("Error"));
            Assert.Equal(1, await context.Plants.CountAsync());
        }

        [Fact]
        public async Task Harvest_Post_Succeeds_WhenGrowthIs100()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "user1", CompanyId = 1 };
            var field = new Field { Id = 1, Name = "Field 1", CompanyId = 1, IsOccupied = true, SizeInDecars = 10 };
            context.Fields.Add(field);
            var plant = new Plant { Id = 1, Name = "Mature Plant", CompanyId = 1, PlantType = "Wheat", FieldId = 1, GrowthStagePercent = 100 };
            context.Plants.Add(plant);
            await context.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new PlantsController(context, userManager.Object);
            SetupController(controller, user);

            // Act
            var result = await controller.Harvest(1);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal(0, await context.Plants.CountAsync());
            Assert.Equal(1, await context.HarvestRecords.CountAsync());
            
            var updatedField = await context.Fields.FindAsync(1);
            Assert.False(updatedField.IsOccupied);
        }

        [Fact]
        public async Task Edit_Get_ReturnsViewWithPlantAndFields()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "user1", CompanyId = 1 };
            var field = new Field { Id = 1, Name = "Field 1", CompanyId = 1 };
            context.Fields.Add(field);
            var plant = new Plant { Id = 1, Name = "Plant 1", CompanyId = 1, FieldId = 1 };
            context.Plants.Add(plant);
            await context.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new PlantsController(context, userManager.Object);
            SetupController(controller, user);

            // Act
            var result = await controller.Edit(1);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<PlantCreateViewModel>(viewResult.Model);
            Assert.Equal("Plant 1", model.Name);
            Assert.NotEmpty((IEnumerable<Field>)controller.ViewBag.Fields);
        }

        [Fact]
        public async Task Edit_Get_ReturnsNotFound_WhenPlantDoesNotExist()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "user1", CompanyId = 1 };
            
            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new PlantsController(context, userManager.Object);
            SetupController(controller, user);

            // Act
            var result = await controller.Edit(999);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Add_Post_Fails_WhenRoleIsEmployee()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var user = new ApplicationUser { Id = "emp1", CompanyId = 1 };
            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new PlantsController(context, userManager.Object);
            SetupController(controller, user);
            
            // Mock role check for Employee
            var claims = new List<Claim> { new Claim(ClaimTypes.Role, "Employee"), new Claim(ClaimTypes.NameIdentifier, "emp1") };
            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            var model = new PlantCreateViewModel { Name = "New Plant" };

            // Act
            var result = await controller.Add(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.True(controller.TempData.ContainsKey("Error"));
            Assert.Contains("Employees cannot add plants", controller.TempData["Error"].ToString());
        }
    }
}
