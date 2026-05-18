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
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Owner.Branches;

[AuthorizePage(RoleCode.Owner)]
public class EditModel : AppPageModel
{
    private readonly AppDbContext _db;
    private readonly IImageUploadService _images;

    public EditModel(AppDbContext db, ICurrentUserAccessor currentUser, IImageUploadService images) : base(currentUser)
    {
        _db = db;
        _images = images;
    }

    [BindProperty]
    public BranchEditInput Input { get; set; } = new();

    public int BranchId { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.BranchId == id, ct);
        if (branch is null) return NotFound();

        BranchId = id;
        Input = new BranchEditInput
        {
            Name = branch.Name,
            Address = branch.Address,
            Latitude = branch.Latitude,
            Longitude = branch.Longitude,
            Phone = branch.Phone,
            ExistingImageUrl = branch.ImageUrl,
            OpeningTime = branch.OpeningTime.ToString("HH:mm"),
            ClosingTime = branch.ClosingTime.ToString("HH:mm"),
            IsActive = branch.IsActive,
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken ct)
    {
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.BranchId == id, ct);
        if (branch is null) return NotFound();

        BranchId = id;
        Input.ExistingImageUrl = branch.ImageUrl;

        if (!ModelState.IsValid)
            return Page();

        try
        {
            // Сначала сохраняем новое изображение, только потом удаляем старое
            string? oldImageUrl = branch.ImageUrl;

            if (Input.RemoveImage)
            {
                branch.ImageUrl = null;
            }

            if (Input.ImageFile is { Length: > 0 })
            {
                var newUrl = await _images.SaveAsync(Input.ImageFile, "branches", ct);
                branch.ImageUrl = newUrl;
            }

            branch.Name = Input.Name.Trim();
            branch.Address = Input.Address.Trim();
            branch.Latitude = Input.Latitude;
            branch.Longitude = Input.Longitude;
            branch.Phone = string.IsNullOrWhiteSpace(Input.Phone) ? null : Input.Phone.Trim();
            if (!TimeOnly.TryParse(Input.OpeningTime, out var openTime) || !TimeOnly.TryParse(Input.ClosingTime, out var closeTime))
            {
                ModelState.AddModelError(string.Empty, "Некорректный формат времени.");
                return Page();
            }
            branch.OpeningTime = openTime;
            branch.ClosingTime = closeTime;
            branch.IsActive = Input.IsActive;

            await _db.SaveChangesAsync(ct);

            // Удаляем старое изображение только после успешного сохранения в БД
            if (branch.ImageUrl != oldImageUrl && !string.IsNullOrEmpty(oldImageUrl))
            {
                _images.Delete(oldImageUrl);
            }

            TempData["Success"] = $"Филиал «{branch.Name}» обновлён.";
            return RedirectToPage("Index");
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            ModelState.AddModelError(string.Empty, $"Ошибка: {msg}");
            return Page();
        }
    }

    public class BranchEditInput
    {
        [Required(ErrorMessage = "Введите название филиала.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Название должно быть от 2 до 120 символов.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите адрес.")]
        [StringLength(300, MinimumLength = 5, ErrorMessage = "Адрес должен быть от 5 до 300 символов.")]
        public string Address { get; set; } = string.Empty;

        [RegularExpression(PhoneValidation.RussianPhonePattern, ErrorMessage = PhoneValidation.ErrorMessage)]
        public string? Phone { get; set; }

        [Range(-90, 90, ErrorMessage = "Широта должна быть от -90 до 90.")] public double? Latitude { get; set; }
        [Range(-180, 180, ErrorMessage = "Долгота должна быть от -180 до 180.")] public double? Longitude { get; set; }

        [Display(Name = "Фото филиала")]
        public IFormFile? ImageFile { get; set; }

        public string? ExistingImageUrl { get; set; }

        public bool RemoveImage { get; set; }

        [Required(ErrorMessage = "Укажите время открытия."), RegularExpression(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "Время в формате ЧЧ:ММ (00:00–23:59).")]
        public string OpeningTime { get; set; } = "10:00";

        [Required(ErrorMessage = "Укажите время закрытия."), RegularExpression(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "Время в формате ЧЧ:ММ (00:00–23:59).")]
        public string ClosingTime { get; set; } = "22:00";

        public bool IsActive { get; set; } = true;
    }
}
