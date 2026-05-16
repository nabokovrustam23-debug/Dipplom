using System.ComponentModel.DataAnnotations;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Auth;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Web.Auth;
using BarbershopCrm.Web.Pages;
using BarbershopCrm.Web.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Masters;

[AuthorizePage(RoleCode.Owner)]
public class CreateModel : AppPageModel
{
    private const string DefaultPosition = "Барбер";

    private readonly AppDbContext _db;

    public CreateModel(AppDbContext db, ICurrentUserAccessor currentUser) : base(currentUser)
    {
        _db = db;
    }

    [BindProperty]
    public MasterCreateInput Input { get; set; } = new();

    public IList<Branch> Branches { get; private set; } = Array.Empty<Branch>();
    public IList<Service> AllServices { get; private set; } = Array.Empty<Service>();

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadLookups(ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LoadLookups(ct);

        if (!ModelState.IsValid)
            return Page();

        // Создание персоны
        var persona = new Persona
        {
            LastName = Input.LastName.Trim(),
            FirstName = Input.FirstName.Trim(),
            MiddleName = string.IsNullOrWhiteSpace(Input.MiddleName) ? null : Input.MiddleName.Trim(),
            Phone = Input.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim(),
            Gender = Input.Gender
        };

        _db.Persona.Add(persona);
        await _db.SaveChangesAsync(ct);

        // Создание мастера
        var master = new Master
        {
            PersonaId = persona.PersonaId,
            BranchId = Input.BranchId,
            Position = DefaultPosition,
            HireDate = DateOnly.FromDateTime(DateTime.Today),
            Bio = string.IsNullOrWhiteSpace(Input.Bio) ? null : Input.Bio.Trim(),
            IsActive = Input.IsActive
        };

        _db.Masters.Add(master);
        await _db.SaveChangesAsync(ct);

        // Добавление услуг мастера
        if (Input.SelectedServiceIds?.Length > 0)
        {
            foreach (var serviceId in Input.SelectedServiceIds)
            {
                _db.MasterServices.Add(new MasterService
                {
                    MasterId = master.MasterId,
                    ServiceId = serviceId
                });
            }
            await _db.SaveChangesAsync(ct);
        }

        TempData["Success"] = $"Мастер «{persona.FullName}» успешно создан.";
        return RedirectToPage("Manage");
    }

    private async Task LoadLookups(CancellationToken ct)
    {
        AllServices = await _db.Services
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .AsNoTracking()
            .ToListAsync(ct);

        Branches = await _db.Branches.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync(ct);
    }

    public class MasterCreateInput
    {
        [Required(ErrorMessage = "Введите фамилию.")]
        [StringLength(60, MinimumLength = 1, ErrorMessage = "Фамилия должна быть от 1 до 60 символов.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите имя.")]
        [StringLength(60, MinimumLength = 1, ErrorMessage = "Имя должно быть от 1 до 60 символов.")]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(60, ErrorMessage = "Отчество не должно превышать 60 символов.")]
        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Введите телефон.")]
        [RegularExpression(PhoneValidation.RussianPhonePattern, ErrorMessage = PhoneValidation.ErrorMessage)]
        public string Phone { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Некорректный email.")]
        [StringLength(120, ErrorMessage = "Email не должен превышать 120 символов.")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Укажите пол.")]
        [StringLength(1, MinimumLength = 1, ErrorMessage = "Укажите пол.")]
        public string Gender { get; set; } = BarbershopCrm.Domain.Enums.Gender.Male;

        [StringLength(500, ErrorMessage = "Биография не должна превышать 500 символов.")]
        public string? Bio { get; set; }

        [Required(ErrorMessage = "Выберите филиал.")]
        public int BranchId { get; set; }

        public int[] SelectedServiceIds { get; set; } = Array.Empty<int>();

        public bool IsActive { get; set; } = true;
    }
}
