using System.ComponentModel.DataAnnotations;
using BarbershopCrm.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BarbershopCrm.Web.Pages.Account;

public sealed class RegisterModel : PageModel
{
    private readonly IUserAuthService _auth;

    public RegisterModel(IUserAuthService auth)
    {
        _auth = auth;
    }

    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Page();

        if (Input.Password != Input.PasswordRepeat)
        {
            ModelState.AddModelError($"Input.{nameof(Input.PasswordRepeat)}", "Пароли не совпадают.");
            return Page();
        }

        var result = await _auth.RegisterClientAsync(new RegisterClientCommand(
            Input.Email,
            Input.Password,
            Input.LastName,
            Input.FirstName,
            Input.MiddleName,
            Input.Phone), ct);

        switch (result)
        {
            case RegistrationResult.Success:
                TempData["Success"] = "Аккаунт успешно создан. Теперь вы можете войти.";
                return RedirectToPage("/Account/Login");

            case RegistrationResult.Failure { Reason: RegistrationFailureReason.EmailAlreadyUsed }:
                ModelState.AddModelError($"Input.{nameof(Input.Email)}",
                    "Этот email уже зарегистрирован. Можно войти или восстановить пароль.");
                return Page();

            case RegistrationResult.Failure { Reason: RegistrationFailureReason.PhoneAlreadyUsed }:
                ModelState.AddModelError($"Input.{nameof(Input.Phone)}",
                    "Этот телефон уже привязан к другому аккаунту.");
                return Page();
        }

        ErrorMessage = "Не удалось создать аккаунт. Попробуйте позже.";
        return Page();
    }

    public sealed class RegisterInput
    {
        [Required(ErrorMessage = "Введите фамилию.")]
        [StringLength(100, ErrorMessage = "Фамилия слишком длинная (макс. 100 символов).")]
        [Display(Name = "Фамилия")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите имя.")]
        [StringLength(100, ErrorMessage = "Имя слишком длинное (макс. 100 символов).")]
        [Display(Name = "Имя")]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Отчество слишком длинное (макс. 100 символов).")]
        [Display(Name = "Отчество")]
        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Введите телефон.")]
        [Phone(ErrorMessage = "Некорректный телефон.")]
        [Display(Name = "Телефон")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите email.")]
        [EmailAddress(ErrorMessage = "Некорректный email.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите пароль.")]
        [StringLength(200, MinimumLength = 8, ErrorMessage = "Пароль должен быть не короче 8 символов.")]
        [DataType(DataType.Password)]
        [Display(Name = "Пароль")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Повторите пароль.")]
        [DataType(DataType.Password)]
        [Display(Name = "Повторите пароль")]
        public string PasswordRepeat { get; set; } = string.Empty;

        [Range(typeof(bool), "true", "true", ErrorMessage = "Без согласия мы не можем создать аккаунт.")]
        [Display(Name = "Согласие на обработку персональных данных")]
        public bool AcceptTerms { get; set; }
    }
}
