using ClaimTrack.Api.Common;
using ClaimTrack.Api.Dtos.Documents;
using ClaimTrack.Api.Services;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace ClaimTrack.Api.Validators;

// Everything about the file itself (400): present, not empty, not too big, and really a
// PDF/PNG/JPEG judging by its own bytes. Rules that need the claim (final? too many files?)
// are in DocumentService (404/409).
public class UploadDocumentRequestValidator : AbstractValidator<UploadDocumentRequest>
{
    public UploadDocumentRequestValidator(IOptions<DocumentOptions> options)
    {
        var maxBytes = options.Value.MaxSizeBytes;

        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("Attach a file in the \"file\" field.")
            .DependentRules(() =>
            {
                RuleFor(x => x.File!.Length)
                    .GreaterThan(0)
                    .WithMessage("The file is empty.")
                    .LessThanOrEqualTo(maxBytes)
                    .WithMessage($"The file is larger than the {maxBytes / (1024 * 1024)} MB limit.")
                    .OverridePropertyName("file");

                RuleFor(x => x.File!)
                    .MustAsync(async (file, cancellationToken) =>
                    {
                        await using var content = file.OpenReadStream();
                        return await FileSignatures.DetectAsync(content, cancellationToken) is not null;
                    })
                    .When(x => x.File!.Length > 0)
                    .WithMessage($"Only {FileSignatures.AllowedDescription} files are allowed.")
                    .OverridePropertyName("file");
            });
    }
}
