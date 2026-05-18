using System.ComponentModel.DataAnnotations;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Auth;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Security;
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
    private readonly IPasswordHasher _hasher;

    public CreateModel(AppDbContext db, IPasswordHasher hasher, ICurrentUserAccessor currentUser) : base(currentUser)
    {
        _db = db;
        _hasher = hasher;
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

        var email = (Input.Email ?? string.Empty).Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Login == email, ct))
        {
            ModelState.AddModelError("Input.Email", "Этот email уже используется другим пользователем.");
            return Page();
        }

        // Создание персоны
        var persona = new Persona
        {
            LastName = Input.LastName.Trim(),
            FirstName = Input.FirstName.Trim(),
            MiddleName = string.IsNullOrWhiteSpace(Input.MiddleName) ? null : Input.MiddleName.Trim(),
            Phone = Input.Phone.Trim(),
            Email = email,
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

        // Создание учётной записи
        var masterRoleId = await _db.Roles
            .Where(r => r.Code == RoleCode.Master)
            .Select(r => r.RoleId)
            .SingleAsync(ct);

        var hash = _hasher.Hash(Input.Password);

        var user = new User
        {
            PersonaId = persona.PersonaId,
            RoleId = masterRoleId,
            BranchId = Input.BranchId,
            Login = email,
            PasswordHash = hash.HashBase64,
            PasswordSalt = hash.SaltBase64,
            PasswordIterations = hash.Iterations,
            IsEmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.Now,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

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

        [Required(ErrorMessage = "Введите email для входа.")]
        [EmailAddress(ErrorMessage = "Некорректный email.")]
        [StringLength(120, ErrorMessage = "Email не должен превышать 120 символов.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите пароль.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Пароль должен быть от 6 до 100 символов.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Подтвердите пароль.")]
        [Compare(nameof(Password), ErrorMessage = "Пароли не совпадают.")]
        [DataType(DataType.Password)]
        public string PasswordConfirm { get; set; } = string.Empty;

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
