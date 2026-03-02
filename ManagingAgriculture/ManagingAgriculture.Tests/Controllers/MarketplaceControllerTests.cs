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
    public class MarketplaceControllerTests
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
        public async Task Index_ReturnsActiveListings()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            db.MarketplaceListings.Add(new MarketplaceListing { Id = 1, ItemName = "Tractor", ListingStatus = "Active", CreatedDate = DateTime.Now });
            db.MarketplaceListings.Add(new MarketplaceListing { Id = 2, ItemName = "Plow", ListingStatus = "Sold", CreatedDate = DateTime.Now });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            var controller = new MarketplaceController(db, userManager.Object);
            SetupController(controller, "user1", "User");

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<MarketplaceListing>>(viewResult.Model);
            Assert.Single(model);
            Assert.Equal("Tractor", model.First().ItemName);
        }

        [Fact]
        public async Task RequestToBuy_CreatesRequest_WhenValid()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var buyer = new ApplicationUser { Id = "buyer1", CompanyId = 1 };
            db.Users.Add(buyer);
            db.MarketplaceListings.Add(new MarketplaceListing { Id = 1, ItemName = "Tractor", ListingStatus = "Active", SellerUserId = "seller1" });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(buyer);

            var controller = new MarketplaceController(db, userManager.Object);
            SetupController(controller, "buyer1", "Boss", 1);

            // Act
            var result = await controller.RequestToBuy(1);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(1, await db.MarketplacePurchaseRequests.CountAsync(r => r.BuyerUserId == "buyer1"));
        }

        [Fact]
        public async Task ApproveRequest_TransfersMachineryAndClosesListing()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var seller = new ApplicationUser { Id = "seller1", CompanyId = 1 };
            var buyer = new ApplicationUser { Id = "buyer1", CompanyId = 2 };
            db.Users.Add(seller);
            db.Users.Add(buyer);
            
            var machinery = new Machinery { Id = 10, Name = "Big Tractor", CompanyId = 1 };
            db.Machinery.Add(machinery);
            
            var listing = new MarketplaceListing { Id = 1, ItemName = "Big Tractor", SellerUserId = "seller1", ListingStatus = "Active", MachineryId = 10 };
            db.MarketplaceListings.Add(listing);
            
            var request = new MarketplacePurchaseRequest { Id = 100, ListingId = 1, BuyerUserId = "buyer1", Status = "Pending" };
            db.MarketplacePurchaseRequests.Add(request);
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(seller);
            userManager.Setup(u => u.FindByIdAsync("buyer1")).ReturnsAsync(buyer);

            var controller = new MarketplaceController(db, userManager.Object);
            SetupController(controller, "seller1", "Boss", 1);

            // Act
            var result = await controller.ApproveRequest(100);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            
            var updatedListing = await db.MarketplaceListings.FindAsync(1);
            Assert.Equal("Sold", updatedListing.ListingStatus);
            
            var updatedMachine = await db.Machinery.FindAsync(10);
            Assert.Equal(2, updatedMachine.CompanyId); // Transferred to buyer
            
            var approvedRequest = await db.MarketplacePurchaseRequests.FindAsync(100);
            Assert.Equal("Approved", approvedRequest.Status);
        }

        [Fact]
        public async Task Delete_Post_Succeeds_WhenNoRequests()
        {
            // Arrange
            using var db = GetInMemoryDbContext();
            var seller = new ApplicationUser { Id = "seller1" };
            db.Users.Add(seller);
            db.MarketplaceListings.Add(new MarketplaceListing { Id = 1, SellerUserId = "seller1" });
            await db.SaveChangesAsync();

            var userManager = GetMockUserManager();
            userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(seller);

            var controller = new MarketplaceController(db, userManager.Object);
            SetupController(controller, "seller1", "Boss");

            // Act
            var result = await controller.Delete(1);

            // Assert
            Assert.Equal(0, await db.MarketplaceListings.CountAsync());
        }
    }
}
