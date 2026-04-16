// =============================================================================
// CoordinateRequestValidator.cs
// POST /api/regulation-check/coordinate 요청 DTO 유효성 검사기
// FluentValidation 기반 — Program.cs에서 AddFluentValidationAutoValidation()으로 자동 연동됩니다.
// =============================================================================

using AutomationRawCheck.Api.Dtos;
using FluentValidation;

namespace AutomationRawCheck.Application.Validation;

#region CoordinateRequestValidator 클래스

/// <summary>
/// <see cref="CoordinateRequestDto"/> 유효성 검사기입니다.
/// <para>
/// FluentValidation 자동 연동이 활성화되면
/// 컨트롤러 액션 실행 전 자동으로 검사되며,
/// 실패 시 400 Bad Request + ValidationProblemDetails가 반환됩니다.
/// </para>
/// </summary>
public sealed class CoordinateRequestValidator : AbstractValidator<CoordinateRequestDto>
{
    #region 생성자 (규칙 정의)

    /// <summary>CoordinateRequestValidator 규칙을 정의합니다.</summary>
    public CoordinateRequestValidator()
    {
        #region 경도(Longitude) 규칙

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180.0, 180.0)
            .WithMessage("경도(longitude)는 -180 ~ 180 범위의 WGS84 값이어야 합니다.")
            .Must(lon => lon != 0.0 || true)  // 0.0 허용 (본초자오선)
            .Must(IsFiniteDouble)
            .WithMessage("경도(longitude)는 유한한 숫자여야 합니다 (NaN, Infinity 불가).");

        #endregion

        #region 위도(Latitude) 규칙

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90.0, 90.0)
            .WithMessage("위도(latitude)는 -90 ~ 90 범위의 WGS84 값이어야 합니다.")
            .Must(IsFiniteDouble)
            .WithMessage("위도(latitude)는 유한한 숫자여야 합니다 (NaN, Infinity 불가).");

        #endregion

        #region 한국 좌표 범위 경고 규칙 (warn only — 실제 거부 아님)
        // 한국 본토 대략 범위: 경도 124~132, 위도 33~39
        // 이 범위를 벗어나도 에러는 아니지만 경고 메시지를 포함합니다.
        // TODO: 실제 서비스 범위를 한국으로 제한하려면 아래 주석을 해제하세요.
        // RuleFor(x => x.Longitude)
        //     .InclusiveBetween(124.0, 132.0)
        //     .WithMessage("경고: 한국 본토 경도 범위(124~132)를 벗어났습니다.");
        // RuleFor(x => x.Latitude)
        //     .InclusiveBetween(33.0, 39.0)
        //     .WithMessage("경고: 한국 본토 위도 범위(33~39)를 벗어났습니다.");
        #endregion
    }

    #endregion

    #region 유틸리티

    /// <summary>double이 유한한 값(NaN, Infinity 제외)인지 확인합니다.</summary>
    private static bool IsFiniteDouble(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    #endregion
}

#endregion
