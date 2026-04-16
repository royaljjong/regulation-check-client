// =============================================================================
// RegulationCheckServiceTests.cs
// RegulationCheckService 단위 테스트
// - Moq를 사용해 모든 프로바이더를 Mock으로 교체합니다.
// - 실제 SHP/CSV 파일 없이 동작합니다.
// =============================================================================

using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Application.Services;
using AutomationRawCheck.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AutomationRawCheck.Tests.Services;

#region RegulationCheckServiceTests 클래스

/// <summary>
/// <see cref="RegulationCheckService"/> 단위 테스트입니다.
/// </summary>
public sealed class RegulationCheckServiceTests
{
    #region 픽스처 (공통 mock 설정)

    private readonly Mock<IZoningLayerProvider>            _zoningMock = new();
    private readonly Mock<ILawReferenceProvider>           _lawMock    = new();
    private readonly Mock<IDistrictUnitPlanProvider>       _dupMock    = new();
    private readonly Mock<IDevelopmentRestrictionProvider> _drpMock    = new();

    /// <summary>테스트용 서비스 인스턴스를 생성합니다.</summary>
    private RegulationCheckService CreateService() =>
        new RegulationCheckService(
            _zoningMock.Object,
            _lawMock.Object,
            _dupMock.Object,
            _drpMock.Object,
            NullLogger<RegulationCheckService>.Instance);

    /// <summary>테스트용 더미 ZoningFeature를 생성합니다.</summary>
    private static ZoningFeature MakeZoningFeature(string name = "제2종일반주거지역", string code = "UQ140") =>
        new ZoningFeature(
            name:        name,
            code:        code,
            sourceLayer: "test.shp",
            attributes:  new Dictionary<string, object?> { ["CODE"] = code, ["NM"] = name });

    /// <summary>데이터 미보유 OverlayZoneResult를 생성합니다.</summary>
    private static OverlayZoneResult NoDataOverlay(string source = "테스트 데이터 미보유") =>
        new OverlayZoneResult(IsInside: false, Name: null, Code: null, Source: source);

    #endregion

    #region 용도지역 발견 시 테스트

    [Fact]
    public async Task CheckAsync_WhenZoningFound_ReturnsPreliminaryResult()
    {
        // Arrange
        var query  = new CoordinateQuery(127.0276, 37.5796);
        var zoning = MakeZoningFeature();

        _zoningMock.Setup(x => x.GetZoningAsync(query, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(zoning);
        _lawMock.Setup(x => x.GetReferencesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<LawReference>());
        _dupMock.Setup(x => x.GetOverlayAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NoDataOverlay());
        _drpMock.Setup(x => x.GetOverlayAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NoDataOverlay());

        var service = CreateService();

        // Act
        var result = await service.CheckAsync(query);

        // Assert
        Assert.Equal(RegulationStatus.Preliminary, result.RegulationSummary.Status);
        Assert.NotNull(result.Zoning);
        Assert.Equal("제2종일반주거지역", result.Zoning.Name);
        Assert.Equal("UQ140", result.Zoning.Code);
        Assert.Equal(query.Longitude, result.Input.Longitude);
        Assert.Equal(query.Latitude,  result.Input.Latitude);
    }

    [Fact]
    public async Task CheckAsync_WhenZoningFound_IncludesLayerMeta()
    {
        // Arrange: ShapefileZoningLayerProvider가 아닌 mock이므로 NoData meta가 반환됨을 확인
        var query  = new CoordinateQuery(127.0276, 37.5796);
        var zoning = MakeZoningFeature();

        _zoningMock.Setup(x => x.GetZoningAsync(query, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(zoning);
        _lawMock.Setup(x => x.GetReferencesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<LawReference>());
        _dupMock.Setup(x => x.GetOverlayAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NoDataOverlay());
        _drpMock.Setup(x => x.GetOverlayAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NoDataOverlay());

        var service = CreateService();

        // Act
        var result = await service.CheckAsync(query);

        // Assert: mock provider는 ShapefileZoningLayerProvider가 아니므로 NoData 메타
        Assert.NotNull(result.LayerMeta);
        Assert.Equal("데이터 없음", result.LayerMeta.LayerName);
    }

    [Fact]
    public async Task CheckAsync_WhenZoningFound_CallsLawProviderWithZoningCode()
    {
        // Arrange
        var query  = new CoordinateQuery(127.0276, 37.5796);
        var zoning = MakeZoningFeature(code: "UQ140");

        _zoningMock.Setup(x => x.GetZoningAsync(query, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(zoning);
        _lawMock.Setup(x => x.GetReferencesAsync("UQ140", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<LawReference>())
                .Verifiable("법령 조회 시 용도지역 코드가 전달되어야 합니다.");
        _dupMock.Setup(x => x.GetOverlayAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NoDataOverlay());
        _drpMock.Setup(x => x.GetOverlayAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NoDataOverlay());

        var service = CreateService();

        // Act
        await service.CheckAsync(query);

        // Assert
        _lawMock.Verify(x => x.GetReferencesAsync("UQ140", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckAsync_WhenZoningFound_PopulatesExtraLayers()
    {
        // Arrange
        var query  = new CoordinateQuery(127.0276, 37.5796);
        var zoning = MakeZoningFeature();
        var drpResult = new OverlayZoneResult(
            IsInside: true,
            Name:     "개발제한구역",
            Code:     "UDV100",
            Source:   "UQ141");

        _zoningMock.Setup(x => x.GetZoningAsync(query, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(zoning);
        _lawMock.Setup(x => x.GetReferencesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<LawReference>());
        _dupMock.Setup(x => x.GetOverlayAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NoDataOverlay());
        _drpMock.Setup(x => x.GetOverlayAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(drpResult);

        var service = CreateService();

        // Act
        var result = await service.CheckAsync(query);

        // Assert
        Assert.NotNull(result.ExtraLayers.DevelopmentRestriction);
        Assert.True(result.ExtraLayers.DevelopmentRestriction!.IsInside);
        Assert.Equal("UDV100", result.ExtraLayers.DevelopmentRestriction.Code);
    }

    #endregion

    #region 용도지역 미발견 시 테스트

    [Fact]
    public async Task CheckAsync_WhenZoningNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var query = new CoordinateQuery(0.0, 0.0);  // 용도지역 없는 좌표

        _zoningMock.Setup(x => x.GetZoningAsync(query, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((ZoningFeature?)null);
        _dupMock.Setup(x => x.GetOverlayAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NoDataOverlay());
        _drpMock.Setup(x => x.GetOverlayAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NoDataOverlay());

        var service = CreateService();

        // Act
        var result = await service.CheckAsync(query);

        // Assert
        Assert.Equal(RegulationStatus.NotFound, result.RegulationSummary.Status);
        Assert.Null(result.Zoning);
        Assert.Empty(result.LawReferences);
    }

    [Fact]
    public async Task CheckAsync_WhenZoningNotFound_DoesNotCallLawProvider()
    {
        // Arrange
        var query = new CoordinateQuery(0.0, 0.0);

        _zoningMock.Setup(x => x.GetZoningAsync(query, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((ZoningFeature?)null);
        _dupMock.Setup(x => x.GetOverlayAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NoDataOverlay());
        _drpMock.Setup(x => x.GetOverlayAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NoDataOverlay());

        var service = CreateService();

        // Act
        await service.CheckAsync(query);

        // Assert: 용도지역 없으면 법령 조회 호출 안 함
        _lawMock.Verify(
            x => x.GetReferencesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region CancellationToken 전달 테스트

    [Fact]
    public async Task CheckAsync_PassesCancellationTokenToProviders()
    {
        // Arrange
        var query  = new CoordinateQuery(127.0276, 37.5796);
        var zoning = MakeZoningFeature();
        var cts    = new CancellationTokenSource();
        var ct     = cts.Token;

        _zoningMock.Setup(x => x.GetZoningAsync(query, ct)).ReturnsAsync(zoning);
        _lawMock.Setup(x => x.GetReferencesAsync(It.IsAny<string>(), ct))
                .ReturnsAsync(Array.Empty<LawReference>());
        _dupMock.Setup(x => x.GetOverlayAsync(query, ct)).ReturnsAsync(NoDataOverlay());
        _drpMock.Setup(x => x.GetOverlayAsync(query, ct)).ReturnsAsync(NoDataOverlay());

        var service = CreateService();

        // Act
        await service.CheckAsync(query, ct);

        // Assert: 각 provider에 CancellationToken이 전달되었는지 확인
        _zoningMock.Verify(x => x.GetZoningAsync(query, ct), Times.Once);
        _lawMock.Verify(x => x.GetReferencesAsync(zoning.Code, ct), Times.Once);
        _dupMock.Verify(x => x.GetOverlayAsync(query, ct), Times.Once);
        _drpMock.Verify(x => x.GetOverlayAsync(query, ct), Times.Once);
    }

    #endregion
}

#endregion
