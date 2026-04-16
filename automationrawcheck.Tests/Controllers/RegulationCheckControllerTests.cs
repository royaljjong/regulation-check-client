// =============================================================================
// RegulationCheckControllerTests.cs
// RegulationCheckController 단위 테스트
// - IRegulationCheckService를 Mock으로 교체합니다.
// =============================================================================

using AutomationRawCheck.Api.Controllers;
using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AutomationRawCheck.Tests.Controllers;

#region RegulationCheckControllerTests 클래스

/// <summary>
/// <see cref="RegulationCheckController"/> 단위 테스트입니다.
/// </summary>
public sealed class RegulationCheckControllerTests
{
    #region 픽스처

    private readonly Mock<IRegulationCheckService> _serviceMock         = new();
    private readonly Mock<IParcelSearchProvider>   _parcelProviderMock  = new();

    /// <summary>테스트용 컨트롤러 인스턴스를 생성합니다.</summary>
    private RegulationCheckController CreateController() =>
        new RegulationCheckController(
            _serviceMock.Object,
            _parcelProviderMock.Object,
            NullLogger<RegulationCheckController>.Instance);

    /// <summary>용도지역 발견 케이스 더미 결과를 생성합니다.</summary>
    private static RegulationCheckResult MakeFoundResult(double lon = 127.0276, double lat = 37.5796)
    {
        var query  = new CoordinateQuery(lon, lat);
        var zoning = new ZoningFeature("제2종일반주거지역", "UQ140", "test.shp",
            new Dictionary<string, object?> { ["CODE"] = "UQ140" });
        var meta   = SpatialLayerMeta.NoData();

        return new RegulationCheckResult(
            input:             query,
            zoning:            zoning,
            regulationSummary: new RegulationSummary(RegulationStatus.Preliminary, "참고용 1차 판정"),
            lawReferences:     Array.Empty<LawReference>(),
            extraLayers:       new ExtraLayerInfo(null, null),
            layerMeta:         meta);
    }

    /// <summary>용도지역 미발견 케이스 더미 결과를 생성합니다.</summary>
    private static RegulationCheckResult MakeNotFoundResult(double lon = 0.0, double lat = 0.0)
    {
        var query = new CoordinateQuery(lon, lat);
        var meta  = SpatialLayerMeta.NoData();

        return new RegulationCheckResult(
            input:             query,
            zoning:            null,
            regulationSummary: new RegulationSummary(RegulationStatus.NotFound, "용도지역 없음"),
            lawReferences:     Array.Empty<LawReference>(),
            extraLayers:       new ExtraLayerInfo(null, null),
            layerMeta:         meta);
    }

    #endregion

    #region POST /coordinate 테스트

    [Fact]
    public async Task PostCoordinateAsync_WithValidRequest_Returns200()
    {
        // Arrange
        var request = new CoordinateRequestDto { Longitude = 127.0276, Latitude = 37.5796 };
        _serviceMock
            .Setup(x => x.CheckAsync(
                It.Is<CoordinateQuery>(q => q.Longitude == 127.0276 && q.Latitude == 37.5796),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeFoundResult(127.0276, 37.5796));

        var controller = CreateController();

        // Act
        var actionResult = await controller.PostCoordinateAsync(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(200, okResult.StatusCode);

        var response = Assert.IsType<RegulationCheckResponseDto>(okResult.Value);
        Assert.Equal(127.0276, response.Input.Longitude);
        Assert.NotNull(response.Zoning);
        Assert.Equal("Preliminary", response.RegulationSummary.Status);
    }

    [Fact]
    public async Task PostCoordinateAsync_WhenZoningNotFound_Returns200WithNotFoundStatus()
    {
        // Arrange: 용도지역 미발견은 404가 아닌 200 + NotFound 상태로 반환
        var request = new CoordinateRequestDto { Longitude = 0.0, Latitude = 0.0 };
        _serviceMock
            .Setup(x => x.CheckAsync(It.IsAny<CoordinateQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeNotFoundResult());

        var controller = CreateController();

        // Act
        var actionResult = await controller.PostCoordinateAsync(request, CancellationToken.None);

        // Assert: 200 OK with NotFound status in body (404 아님)
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(200, okResult.StatusCode);

        var response = Assert.IsType<RegulationCheckResponseDto>(okResult.Value);
        Assert.Equal("NotFound", response.RegulationSummary.Status);
        Assert.Null(response.Zoning);
    }

    [Fact]
    public async Task PostCoordinateAsync_CallsServiceWithCorrectCoordinates()
    {
        // Arrange
        var request = new CoordinateRequestDto { Longitude = 126.9784, Latitude = 37.5665 };
        _serviceMock
            .Setup(x => x.CheckAsync(It.IsAny<CoordinateQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeFoundResult(126.9784, 37.5665));

        var controller = CreateController();

        // Act
        await controller.PostCoordinateAsync(request, CancellationToken.None);

        // Assert: 서비스에 정확한 좌표가 전달되었는지 확인
        _serviceMock.Verify(
            x => x.CheckAsync(
                It.Is<CoordinateQuery>(q =>
                    Math.Abs(q.Longitude - 126.9784) < 0.0001 &&
                    Math.Abs(q.Latitude  - 37.5665)  < 0.0001),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GET /health 테스트

    [Fact]
    public void Health_Returns200WithOkStatus()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var actionResult = controller.Health();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(200, okResult.StatusCode);
        Assert.NotNull(okResult.Value);
    }

    #endregion
}

#endregion
