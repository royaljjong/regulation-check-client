// =============================================================================
// AddressCheckRequestValidator.cs
// POST /api/regulation-check/address 요청 FluentValidation 검증기
// =============================================================================

using AutomationRawCheck.Api.Dtos;
using FluentValidation;

namespace AutomationRawCheck.Application.Validation;

#region AddressCheckRequestValidator 클래스

/// <summary>
/// AddressCheckRequestDto에 대한 FluentValidation 검증기입니다.
/// </summary>
public sealed class AddressCheckRequestValidator : AbstractValidator<AddressCheckRequestDto>
{
    /// <summary>AddressCheckRequestValidator를 초기화합니다.</summary>
    public AddressCheckRequestValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("query는 필수입니다.")
            .MinimumLength(2).WithMessage("주소는 2자 이상 입력하세요.")
            .MaximumLength(300).WithMessage("주소는 300자 이하로 입력하세요.");
    }
}

#endregion
