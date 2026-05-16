using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Auth;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Web.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace BarbershopCrm.Web.Pages.Profile;

[AuthorizePage]
public sealed class EditModel : AppPageModel
{
    private readonly AppDbContext _db;

    public EditModel(ICurrentUserAccessor currentUser, AppDbContext db) : base(currentUser)
    {
        _db = db;
    }

    [BindProperty]
    public EditProfileInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (Current == null || Current.PersonaId <= 0)
            return RedirectToPage("/Profile/Index");

        var persona = await _db.Persona.FindAsync(Current.PersonaId);
        if (persona == null)
            return RedirectToPage("/Profile/Index");

        Input = new EditProfileInput
        {
            LastName = persona.LastName,
            FirstName = persona.FirstName,
            MiddleName = persona.MiddleName,
            Phone = persona.Phone,
            Email = persona.Email,
            BirthDate = persona.BirthDate
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        if (Current == null || Current.PersonaId <= 0)
            return RedirectToPage("/Profile/Index");

        var persona = await _db.Persona.FindAsync(Current.PersonaId);
        if (persona == null)
            return RedirectToPage("/Profile/Index");

        persona.LastName = Input.LastName.Trim();
        persona.FirstName = Input.FirstName.Trim();
        persona.MiddleName = string.IsNullOrWhiteSpace(Input.MiddleName) ? null : Input.MiddleName.Trim();
        persona.Phone = Input.Phone.Trim();
        persona.Email = string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim();
        persona.BirthDate = Input.BirthDate;

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Профиль успешно обновлён";
        return RedirectToPage("/Profile/Index");
    }

    public sealed class EditProfileInput
    {
        [Required(ErrorMessage = "Фамилия обязательна")]
        [StringLength(100, ErrorMessage = "Максимум 100 символов")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Имя обязательно")]
        [StringLength(100, ErrorMessage = "Максимум 100 символов")]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Максимум 100 символов")]
        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Телефон обязателен")]
        [Phone(ErrorMessage = "Некорректный формат телефона")]
        [StringLength(20, ErrorMessage = "Максимум 20 символов")]
        public string Phone { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Некорректный формат email")]
        [StringLength(255, ErrorMessage = "Максимум 255 символов")]
        public string? Email { get; set; }

        [Display(Name = "Дата рождения")]
        public DateOnly? BirthDate { get; set; }
    }
}

// Made with Bob
