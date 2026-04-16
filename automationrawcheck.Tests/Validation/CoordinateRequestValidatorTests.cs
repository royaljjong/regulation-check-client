// =============================================================================
// CoordinateRequestValidatorTests.cs
// CoordinateRequestValidator 단위 테스트
// =============================================================================

using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Validation;

namespace AutomationRawCheck.Tests.Validation;

#region CoordinateRequestValidatorTests 클래스

/// <summary>
/// <see cref="CoordinateRequestValidator"/> 단위 테스트입니다.
/// </summary>
public sealed class CoordinateRequestValidatorTests
{
    private readonly CoordinateRequestValidator _validator = new();

    #region 유효한 입력 테스트

    [Theory]
    [InlineData(127.0276, 37.5796)]    // 서울 종로구
    [InlineData(126.9784, 37.5665)]    // 서울 시청
    [InlineData(129.0756, 35.1796)]    // 부산
    [InlineData(0.0, 0.0)]             // 본초자오선 교차점 (유효한 WGS84)
    [InlineData(-180.0, -90.0)]        // 경계 최솟값
    [InlineData(180.0, 90.0)]          // 경계 최댓값
    public async Task Validate_WithValidCoordinates_ShouldPass(double lon, double lat)
    {
        // Arrange
        var dto = new CoordinateRequestDto { Longitude = lon, Latitude = lat };

        // Act
        var result = await _validator.ValidateAsync(dto);

        // Assert
        Assert.True(result.IsValid,
            $"Lon={lon}, Lat={lat}는 유효해야 합니다. 오류: {string.Join(", ", result.Errors.Select(e => e.ErrorMessage))}");
    }

    #endregion

    #region 유효하지 않은 입력 테스트

    [Theory]
    [InlineData(181.0, 37.0)]     // 경도 초과
    [InlineData(-181.0, 37.0)]    // 경도 미만
    [InlineData(127.0, 91.0)]     // 위도 초과
    [InlineData(127.0, -91.0)]    // 위도 미만
    [InlineData(200.0, 100.0)]    // 둘 다 범위 초과
    public async Task Validate_WithOutOfRangeCoordinates_ShouldFail(double lon, double lat)
    {
        // Arrange
        var dto = new CoordinateRequestDto { Longitude = lon, Latitude = lat };

        // Act
        var result = await _validator.ValidateAsync(dto);

        // Assert
        Assert.False(result.IsValid,
            $"Lon={lon}, Lat={lat}는 유효하지 않아야 합니다.");
    }

    [Fact]
    public async Task Validate_WithNaN_ShouldFail()
    {
        // Arrange
        var dto = new CoordinateRequestDto { Longitude = double.NaN, Latitude = 37.0 };

        // Act
        var result = await _validator.ValidateAsync(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Longitude");
    }

    [Fact]
    public async Task Validate_WithInfinity_ShouldFail()
    {
        // Arrange
        var dto = new CoordinateRequestDto { Longitude = 127.0, Latitude = double.PositiveInfinity };

        // Act
        var result = await _validator.ValidateAsync(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Latitude");
    }

    #endregion
}

#endregion
