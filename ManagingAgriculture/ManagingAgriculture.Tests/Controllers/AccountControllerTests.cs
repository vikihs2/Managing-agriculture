using System.Collections.Generic;
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
    public class AccountControllerTests
    {
        private Mock<UserManager<ApplicationUser>> GetMockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
        }

        private Mock<SignInManager<ApplicationUser>> GetMockSignInManager(UserManager<ApplicationUser> userManager)
        {
            var contextAccessor = new Mock<IHttpContextAccessor>();
            var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            return new Mock<SignInManager<ApplicationUser>>(userManager, contextAccessor.Object, claimsFactory.Object, null, null, null, null);
        }

        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: System.Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public void Login_Get_ReturnsViewResult()
        {
            // Arrange
            var userManager = GetMockUserManager();
            var signInManager = GetMockSignInManager(userManager.Object);
            var controller = new AccountController(userManager.Object, signInManager.Object);

            // Act
            var result = controller.Login();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.IsType<LoginViewModel>(viewResult.Model);
        }

        [Fact]
        public async Task Login_Post_InvalidModel_ReturnsView()
        {
            // Arrange
            var userManager = GetMockUserManager();
            var signInManager = GetMockSignInManager(userManager.Object);
            var controller = new AccountController(userManager.Object, signInManager.Object);
            controller.ModelState.AddModelError("Email", "Required");

            var model = new LoginViewModel();

            // Act
            var result = await controller.Login(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
        }

        [Fact]
        public async Task Login_Post_ValidLogin_ReturnsRedirect_To_Dashboard()
        {
            // Arrange
            var userManager = GetMockUserManager();
            var signInManager = GetMockSignInManager(userManager.Object);
            
            var user = new ApplicationUser { UserName = "test@test.com", Email = "test@test.com" };
            userManager.Setup(u => u.FindByEmailAsync("test@test.com")).ReturnsAsync(user);
            userManager.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });
            
            signInManager.Setup(s => s.PasswordSignInAsync(user.UserName, "password", false, false))
                .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

            var controller = new AccountController(userManager.Object, signInManager.Object);
            var model = new LoginViewModel { Email = "test@test.com", Password = "password" };

            // Act
            var result = await controller.Login(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal("Dashboard", redirectResult.ControllerName);
        }

        [Fact]
        public async Task Register_Post_InvalidModel_ReturnsView()
        {
            // Arrange
            var userManager = GetMockUserManager();
            var signInManager = GetMockSignInManager(userManager.Object);
            var controller = new AccountController(userManager.Object, signInManager.Object);
            controller.ModelState.AddModelError("Email", "Required");
            
            using var context = GetInMemoryDbContext();
            var model = new RegisterViewModel();

            // Act
            var result = await controller.Register(model, context);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
        }
        
        [Fact]
        public async Task Register_Post_ValidRegistration_ReturnsRedirectToLogin()
        {
            // Arrange
            var userManager = GetMockUserManager();
            var signInManager = GetMockSignInManager(userManager.Object);
            
            userManager.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
            userManager.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "User"))
                .ReturnsAsync(IdentityResult.Success);

            var controller = new AccountController(userManager.Object, signInManager.Object);
            
            using var context = GetInMemoryDbContext();
            
            // To be able to set TempData we need to mock HttpContext and TempDataProvider
            var httpContext = new DefaultHttpContext();
            var tempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                httpContext, Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
            controller.TempData = tempData;

            var model = new RegisterViewModel { Email = "test@test.com", Password = "password", ConfirmPassword = "password", RegisterAsCompany = false };

            // Act
            var result = await controller.Register(model, context);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Login", redirectResult.ActionName);
            Assert.True(controller.TempData.ContainsKey("SuccessMessage"));
        }

        [Fact]
        public async Task Logout_Post_RedirectsToHome()
        {
            // Arrange
            var userManager = GetMockUserManager();
            var signInManager = GetMockSignInManager(userManager.Object);
            signInManager.Setup(s => s.SignOutAsync()).Returns(Task.CompletedTask);

            var controller = new AccountController(userManager.Object, signInManager.Object);

            // Act
            var result = await controller.Logout();

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal("Home", redirectResult.ControllerName);
            signInManager.Verify(s => s.SignOutAsync(), Times.Once);
        }
    }
}
