using FluentValidation;

namespace TestJob.Api;

// Валидация входящего объекта через FluentValidation.
public sealed class ProcessRequestValidator : AbstractValidator<ProcessRequest>
{
    public ProcessRequestValidator()
    {
        RuleFor(x => x.Selector)
            .NotEmpty().WithErrorCode(ErrorCodes.EmptySelector)
            .WithMessage("Пустой селектор во входящем объекте.");

        RuleFor(x => x.Attribute)
            .NotEmpty().WithErrorCode(ErrorCodes.EmptyAttribute)
            .WithMessage("Пустой атрибут во входящем объекте.");

        RuleFor(x => x.UrlB64)
            .NotEmpty().WithErrorCode(ErrorCodes.MissingParam)
            .WithMessage("Отсутствует параметр url_b64.");

        RuleFor(x => x.EncryptedTextBytesB64)
            .NotEmpty().WithErrorCode(ErrorCodes.MissingParam)
            .WithMessage("Отсутствует параметр encrypted_text_bytes_b64.");

        RuleFor(x => x.KeyBytesB64)
            .NotEmpty().WithErrorCode(ErrorCodes.MissingParam)
            .WithMessage("Отсутствует параметр key_bytes_b64.");

        RuleFor(x => x.PageB64)
            .NotEmpty().WithErrorCode(ErrorCodes.MissingParam)
            .WithMessage("Отсутствует параметр page_b64.");
    }
}
