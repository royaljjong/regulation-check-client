// =============================================================================
// ParcelSearchRequestValidator.cs
// POST /api/regulation-check/parcel 요청 DTO 유효성 검사기
// =============================================================================

using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Domain.Models;
using FluentValidation;

namespace AutomationRawCheck.Application.Validation;

#region ParcelSearchRequestValidator 클래스

/// <summary>
/// <see cref="ParcelSearchRequestDto"/> 유효성 검사기입니다.
/// searchType에 따라 다른 규칙을 적용합니다.
/// </summary>
public sealed class ParcelSearchRequestValidator : AbstractValidator<ParcelSearchRequestDto>
{
    #region 생성자 (규칙 정의)

    /// <summary>ParcelSearchRequestValidator 규칙을 정의합니다.</summary>
    public ParcelSearchRequestValidator()
    {
        #region searchType 규칙

        RuleFor(x => x.SearchType)
            .NotEmpty()
            .WithMessage("searchType은 필수입니다.")
            .Must(BeValidSearchType)
            .WithMessage($"searchType은 다음 중 하나여야 합니다: " +
                         $"{string.Join(", ", Enum.GetNames<ParcelSearchType>())}");

        #endregion

        #region Coordinate 타입 규칙

        // searchType이 "Coordinate"이면 longitude, latitude 필수
        When(x => x.SearchType.Equals("Coordinate", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.Longitude)
                .NotNull()
                .WithMessage("searchType이 Coordinate이면 longitude는 필수입니다.")
                .InclusiveBetween(-180.0, 180.0)
                .WithMessage("longitude는 -180 ~ 180 범위여야 합니다.")
                .When(x => x.Longitude.HasValue);

            RuleFor(x => x.Latitude)
                .NotNull()
                .WithMessage("searchType이 Coordinate이면 latitude는 필수입니다.")
                .InclusiveBetween(-90.0, 90.0)
                .WithMessage("latitude는 -90 ~ 90 범위여야 합니다.")
                .When(x => x.Latitude.HasValue);
        });

        #endregion

        #region JibunAddress / RoadAddress 타입 규칙

        // 주소 타입이면 addressText 필수
        When(x => !x.SearchType.Equals("Coordinate", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.AddressText)
                .NotEmpty()
                .WithMessage("JibunAddress 또는 RoadAddress 타입이면 addressText는 필수입니다.")
                .MaximumLength(200)
                .WithMessage("addressText는 200자 이내여야 합니다.");
        });

        #endregion
    }

    #endregion

    #region 유틸리티

    /// <summary>문자열이 유효한 <see cref="ParcelSearchType"/> 열거형 값인지 확인합니다.</summary>
    private static bool BeValidSearchType(string searchType) =>
        Enum.TryParse<ParcelSearchType>(searchType, ignoreCase: true, out _);

    #endregion
}

#endregion
