using System.ComponentModel.DataAnnotations;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Auth;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Web.Auth;
using BarbershopCrm.Web.Pages;
using BarbershopCrm.Web.Services;
using BarbershopCrm.Web.Validation;
using Microsoft.AspNetCore.Mvc;

namespace BarbershopCrm.Web.Pages.Owner.Branches;

[AuthorizePage(RoleCode.Owner)]
public class CreateModel : AppPageModel
{
    private readonly AppDbContext _db;
    private readonly IImageUploadService _images;

    public CreateModel(AppDbContext db, ICurrentUserAccessor currentUser, IImageUploadService images) : base(currentUser)
    {
        _db = db;
        _images = images;
    }

    [BindProperty]
    public BranchCreateInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Page();

        try
        {
            var branch = new Branch
            {
                Name = Input.Name.Trim(),
                Address = Input.Address.Trim(),
            Phone = string.IsNullOrWhiteSpace(Input.Phone) ? null : Input.Phone.Trim(),
                OpeningTime = TimeOnly.TryParse(Input.OpeningTime, out var openTime) ? openTime : TimeOnly.MinValue,
                ClosingTime = TimeOnly.TryParse(Input.ClosingTime, out var closeTime) ? closeTime : TimeOnly.MinValue,
                IsActive = Input.IsActive
            };

            if (Input.ImageFile is { Length: > 0 })
                branch.ImageUrl = await _images.SaveAsync(Input.ImageFile, "branches", ct);

            _db.Branches.Add(branch);
            await _db.SaveChangesAsync(ct);

            TempData["Success"] = $"Филиал «{branch.Name}» успешно создан.";
            return RedirectToPage("Index");
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            ModelState.AddModelError(string.Empty, $"Ошибка: {msg}");
            return Page();
        }
    }

    public class BranchCreateInput
    {
        [Required(ErrorMessage = "Введите название филиала.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Название должно быть от 2 до 120 символов.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите адрес.")]
        [StringLength(300, MinimumLength = 5, ErrorMessage = "Адрес должен быть от 5 до 300 символов.")]
        public string Address { get; set; } = string.Empty;

        [RegularExpression(PhoneValidation.RussianPhonePattern, ErrorMessage = PhoneValidation.ErrorMessage)]
        public string? Phone { get; set; }

        [Display(Name = "Фото филиала")]
        public IFormFile? ImageFile { get; set; }

        [Required(ErrorMessage = "Укажите время открытия.")]
        [RegularExpression(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "Формат времени: ЧЧ:ММ")]
        public string OpeningTime { get; set; } = "09:00";

        [Required(ErrorMessage = "Укажите время закрытия.")]
        [RegularExpression(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "Формат времени: ЧЧ:ММ")]
        public string ClosingTime { get; set; } = "21:00";

        public bool IsActive { get; set; } = true;
    }
}
