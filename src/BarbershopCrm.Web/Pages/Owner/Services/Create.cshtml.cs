using System.ComponentModel.DataAnnotations;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Auth;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Web.Auth;
using BarbershopCrm.Web.Pages;
using BarbershopCrm.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BarbershopCrm.Web.Pages.Owner.Services;

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
    public ServiceCreateInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Page();

        var service = new Service
        {
            Name = Input.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim(),
            DurationMinutes = Input.DurationMinutes,
            Price = Input.Price,
            IsActive = Input.IsActive
        };

        if (Input.ImageFile is { Length: > 0 })
        {
            try
            {
                service.ImageUrl = await _images.SaveAsync(Input.ImageFile, "services", ct);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("Input.ImageFile", ex.Message);
                return Page();
            }
        }

        _db.Services.Add(service);
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = $"Услуга «{service.Name}» успешно создана.";
        return RedirectToPage("Index");
    }

    public class ServiceCreateInput
    {
        [Required(ErrorMessage = "Введите название услуги.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Название должно быть от 2 до 120 символов.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Описание слишком длинное (макс. 500 символов).")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Укажите длительность услуги.")]
        [Range(5, 480, ErrorMessage = "Длительность должна быть от 5 до 480 минут.")]
        public int DurationMinutes { get; set; } = 30;

        [Required(ErrorMessage = "Укажите цену услуги.")]
        [Range(0.01, 999999.99, ErrorMessage = "Цена должна быть от 0.01 до 999999.99.")]
        public decimal Price { get; set; }

        [Display(Name = "Фото услуги")]
        public IFormFile? ImageFile { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
