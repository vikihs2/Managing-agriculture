using System;
using ManagingAgriculture.Controllers;
using ManagingAgriculture.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ManagingAgriculture.Tests.Controllers
{
    public class SensorsControllerTests
    {
        [Fact]
        public void Index_ReturnsDisconnectedView_WhenNotConnected()
        {
            // Arrange
            var arduinoMock = new Mock<ArduinoService>();
            arduinoMock.Setup(a => a.IsConnected()).Returns(false);
            var controller = new SensorsController(arduinoMock.Object);

            // Act
            var result = controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Disconnected", viewResult.ViewName);
        }

        [Fact]
        public void Index_ReturnsDefaultView_WhenConnected()
        {
            // Arrange
            var arduinoMock = new Mock<ArduinoService>();
            arduinoMock.Setup(a => a.IsConnected()).Returns(true);
            var controller = new SensorsController(arduinoMock.Object);

            // Act
            var result = controller.Index();

            // Assert
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public void GetWaterLevel_ReturnsJsonValue()
        {
            // Arrange
            var arduinoMock = new Mock<ArduinoService>();
            arduinoMock.Setup(a => a.GetValue()).Returns(75);
            var controller = new SensorsController(arduinoMock.Object);

            // Act
            var result = controller.GetWaterLevel();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var valueProp = jsonResult.Value.GetType().GetProperty("value");
            Assert.Equal(75, valueProp.GetValue(jsonResult.Value));
        }
    }
}
