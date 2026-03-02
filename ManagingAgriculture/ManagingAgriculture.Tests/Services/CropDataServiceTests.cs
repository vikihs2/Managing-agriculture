using System;
using ManagingAgriculture.Services;
using Xunit;

namespace ManagingAgriculture.Tests.Services
{
    public class CropDataServiceTests
    {
        private readonly CropDataService _service;

        public CropDataServiceTests()
        {
            _service = new CropDataService();
        }

        [Fact]
        public void GetCropMaturityDays_ReturnsCorrectDays_ForKnownCrop()
        {
            // Act
            int days = _service.GetCropMaturityDays("Wheat");

            // Assert
            Assert.Equal(150, days);
        }

        [Fact]
        public void GetCropMaturityDays_ReturnsDefault_ForUnknownCrop()
        {
            // Act
            int days = _service.GetCropMaturityDays("Unknown Crop");

            // Assert
            Assert.Equal(100, days);
        }

        [Fact]
        public void CalculateGrowthPercentage_ReturnsZero_WhenNotPlanted()
        {
            // Arrange
            var planted = DateTime.Now.AddDays(1);
            var current = DateTime.Now;

            // Act
            int result = _service.CalculateGrowthPercentage(planted, current, "Wheat", "Loamy", 25, true, 3, "Full Sun");

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public void CalculateGrowthPercentage_ReturnsCappedValue_WhenMatured()
        {
            // Arrange
            var planted = DateTime.Now.AddDays(-200);
            var current = DateTime.Now;

            // Act
            int result = _service.CalculateGrowthPercentage(planted, current, "Wheat", "Loamy", 25, true, 3, "Full Sun");

            // Assert
            Assert.Equal(100, result);
        }

        [Fact]
        public void CalculateGrowthPercentage_ReturnsDeadStatus_WhenConditionsAreCriticallyBad()
        {
            // Arrange
            // Critical bad conditions: temp 0, no water, no sun
            var planted = DateTime.Now.AddDays(-20);
            var current = DateTime.Now;

            // Act
            int result = _service.CalculateGrowthPercentage(planted, current, "Wheat", "Clay", 0, false, 0, "Full Shade");

            // Assert
            Assert.Equal(-1, result);
        }

        [Theory]
        [InlineData(-1, "Dead ☠️")]
        [InlineData(10, "Dying 🌱")]
        [InlineData(50, "Stressed ⚠️")]
        [InlineData(90, "Healthy 🌿")]
        public void GetPlantStatus_ReturnsExpectedStatus(int growth, string expected)
        {
            // Act
            string result = _service.GetPlantStatus(growth, DateTime.Now, DateTime.Now);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetCropRecommendations_ReturnsDefault_ForEmptyCrop()
        {
            // Act
            var recs = _service.GetCropRecommendations("");

            // Assert
            Assert.Contains("Loamy soil", recs.soil);
        }
    }
}
