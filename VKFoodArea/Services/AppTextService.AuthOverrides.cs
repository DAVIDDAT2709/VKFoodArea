namespace VKFoodArea.Services;

public sealed partial class AppTextService
{
    private static IReadOnlyDictionary<string, string> CreateVietnameseAuthOverrides()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Login.Subtitle"] = "Đăng nhập để tiếp tục khám phá khu ẩm thực và lịch sử nghe.",
            ["Login.UsernameLabel"] = "Tài khoản hoặc email",
            ["Login.UsernamePlaceholder"] = "Nhập tài khoản hoặc email",
            ["Login.DisabledError"] = "Tài khoản hiện đang bị khóa.",
            ["Login.RegisterPrompt"] = "Chưa có tài khoản?",
            ["Login.RegisterDescription"] = "Tạo tài khoản mới rồi quay lại đăng nhập ngay trên app.",
            ["Login.RegisterButton"] = "Đăng ký",
            ["Register.PageTitle"] = "Đăng ký",
            ["Register.BackShort"] = "Quay lại",
            ["Register.Hero"] = "Tạo tài khoản mới trong vài bước ngắn gọn",
            ["Register.Title"] = "Tạo tài khoản",
            ["Register.Subtitle"] = "Nhập thông tin cơ bản để bắt đầu sử dụng app.",
            ["Register.FullNameLabel"] = "Họ và tên",
            ["Register.FullNamePlaceholder"] = "Nhập họ và tên",
            ["Register.EmailLabel"] = "Email",
            ["Register.EmailPlaceholder"] = "Nhập email",
            ["Register.PasswordLabel"] = "Mật khẩu",
            ["Register.PasswordPlaceholder"] = "Tạo mật khẩu",
            ["Register.ConfirmPasswordLabel"] = "Xác nhận mật khẩu",
            ["Register.ConfirmPasswordPlaceholder"] = "Nhập lại mật khẩu",
            ["Register.Submit"] = "Tạo tài khoản",
            ["Register.BackToLogin"] = "Tôi đã có tài khoản",
            ["Register.RequiredError"] = "Vui lòng nhập đầy đủ thông tin đăng ký.",
            ["Register.InvalidEmailError"] = "Email chưa đúng định dạng.",
            ["Register.PasswordMismatchError"] = "Mật khẩu xác nhận chưa khớp.",
            ["Register.PasswordTooShortError"] = "Mật khẩu cần ít nhất 6 ký tự.",
            ["Register.DuplicateEmailError"] = "Email này đã được dùng cho tài khoản khác.",
            ["Register.FailedError"] = "Không thể tạo tài khoản lúc này.",
            ["Register.SuccessTitle"] = "Đăng ký thành công",
            ["Register.SuccessMessage"] = "Tài khoản đã được tạo. Bạn có thể đăng nhập ngay bằng email hoặc tên đăng nhập."
        };
    }

    private static IReadOnlyDictionary<string, string> CreateEnglishAuthOverrides()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Login.Subtitle"] = "Sign in to continue exploring food spots and listening history.",
            ["Login.UsernameLabel"] = "Username or email",
            ["Login.UsernamePlaceholder"] = "Enter username or email",
            ["Login.DisabledError"] = "This account is currently disabled.",
            ["Login.RegisterPrompt"] = "Need a new account?",
            ["Login.RegisterDescription"] = "Create one in the app, then come back to sign in.",
            ["Login.RegisterButton"] = "Register",
            ["Register.PageTitle"] = "Register",
            ["Register.BackShort"] = "Back",
            ["Register.Hero"] = "Create a new account in a few quick steps",
            ["Register.Title"] = "Create account",
            ["Register.Subtitle"] = "Enter your basic details to start using the app.",
            ["Register.FullNameLabel"] = "Full name",
            ["Register.FullNamePlaceholder"] = "Enter full name",
            ["Register.EmailLabel"] = "Email",
            ["Register.EmailPlaceholder"] = "Enter email",
            ["Register.PasswordLabel"] = "Password",
            ["Register.PasswordPlaceholder"] = "Create password",
            ["Register.ConfirmPasswordLabel"] = "Confirm password",
            ["Register.ConfirmPasswordPlaceholder"] = "Re-enter password",
            ["Register.Submit"] = "Create account",
            ["Register.BackToLogin"] = "I already have an account",
            ["Register.RequiredError"] = "Please complete all registration fields.",
            ["Register.InvalidEmailError"] = "Email format is invalid.",
            ["Register.PasswordMismatchError"] = "Password confirmation does not match.",
            ["Register.PasswordTooShortError"] = "Password must be at least 6 characters.",
            ["Register.DuplicateEmailError"] = "This email is already used by another account.",
            ["Register.FailedError"] = "Unable to create the account right now.",
            ["Register.SuccessTitle"] = "Registration complete",
            ["Register.SuccessMessage"] = "Your account has been created. You can now sign in with your email or username."
        };
    }

    private static IReadOnlyDictionary<string, string> CreateChineseAuthOverrides()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Login.Subtitle"] = "登录后即可继续浏览美食地点与收听记录。",
            ["Login.UsernameLabel"] = "账号或邮箱",
            ["Login.UsernamePlaceholder"] = "输入账号或邮箱",
            ["Login.DisabledError"] = "该账号当前已被停用。",
            ["Login.RegisterPrompt"] = "还没有账号？",
            ["Login.RegisterDescription"] = "先在应用内创建账号，再回来登录。",
            ["Login.RegisterButton"] = "注册",
            ["Register.PageTitle"] = "注册",
            ["Register.BackShort"] = "返回",
            ["Register.Hero"] = "几步即可创建新的应用账号",
            ["Register.Title"] = "创建账号",
            ["Register.Subtitle"] = "填写基础信息即可开始使用应用。",
            ["Register.FullNameLabel"] = "姓名",
            ["Register.FullNamePlaceholder"] = "输入姓名",
            ["Register.EmailLabel"] = "邮箱",
            ["Register.EmailPlaceholder"] = "输入邮箱",
            ["Register.PasswordLabel"] = "密码",
            ["Register.PasswordPlaceholder"] = "设置密码",
            ["Register.ConfirmPasswordLabel"] = "确认密码",
            ["Register.ConfirmPasswordPlaceholder"] = "再次输入密码",
            ["Register.Submit"] = "创建账号",
            ["Register.BackToLogin"] = "我已有账号",
            ["Register.RequiredError"] = "请完整填写注册信息。",
            ["Register.InvalidEmailError"] = "邮箱格式不正确。",
            ["Register.PasswordMismatchError"] = "两次输入的密码不一致。",
            ["Register.PasswordTooShortError"] = "密码至少需要 6 个字符。",
            ["Register.DuplicateEmailError"] = "该邮箱已被其他账号使用。",
            ["Register.FailedError"] = "暂时无法创建账号。",
            ["Register.SuccessTitle"] = "注册成功",
            ["Register.SuccessMessage"] = "账号已创建，现在可以用邮箱或用户名登录。"
        };
    }

    private static IReadOnlyDictionary<string, string> CreateJapaneseAuthOverrides()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Login.Subtitle"] = "ログインしてグルメスポットと再生履歴を続けて利用します。",
            ["Login.UsernameLabel"] = "ユーザー名またはメール",
            ["Login.UsernamePlaceholder"] = "ユーザー名またはメールを入力",
            ["Login.DisabledError"] = "このアカウントは現在利用できません。",
            ["Login.RegisterPrompt"] = "アカウントをお持ちではありませんか？",
            ["Login.RegisterDescription"] = "アプリ内で新規登録してからログインに戻れます。",
            ["Login.RegisterButton"] = "新規登録",
            ["Register.PageTitle"] = "新規登録",
            ["Register.BackShort"] = "戻る",
            ["Register.Hero"] = "数ステップで新しいアカウントを作成できます",
            ["Register.Title"] = "アカウント作成",
            ["Register.Subtitle"] = "基本情報を入力するとアプリを利用できます。",
            ["Register.FullNameLabel"] = "氏名",
            ["Register.FullNamePlaceholder"] = "氏名を入力",
            ["Register.EmailLabel"] = "メール",
            ["Register.EmailPlaceholder"] = "メールを入力",
            ["Register.PasswordLabel"] = "パスワード",
            ["Register.PasswordPlaceholder"] = "パスワードを作成",
            ["Register.ConfirmPasswordLabel"] = "パスワード確認",
            ["Register.ConfirmPasswordPlaceholder"] = "もう一度入力",
            ["Register.Submit"] = "アカウント作成",
            ["Register.BackToLogin"] = "すでにアカウントがあります",
            ["Register.RequiredError"] = "登録情報をすべて入力してください。",
            ["Register.InvalidEmailError"] = "メール形式が正しくありません。",
            ["Register.PasswordMismatchError"] = "確認用パスワードが一致しません。",
            ["Register.PasswordTooShortError"] = "パスワードは 6 文字以上必要です。",
            ["Register.DuplicateEmailError"] = "このメールは既に別のアカウントで使用されています。",
            ["Register.FailedError"] = "現在アカウントを作成できません。",
            ["Register.SuccessTitle"] = "登録完了",
            ["Register.SuccessMessage"] = "アカウントが作成されました。メールまたはユーザー名でログインできます。"
        };
    }

    private static IReadOnlyDictionary<string, string> CreateGermanAuthOverrides()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Login.Subtitle"] = "Melden Sie sich an, um Food-Spots und Hörverlauf weiter zu nutzen.",
            ["Login.UsernameLabel"] = "Benutzername oder E-Mail",
            ["Login.UsernamePlaceholder"] = "Benutzernamen oder E-Mail eingeben",
            ["Login.DisabledError"] = "Dieses Konto ist derzeit deaktiviert.",
            ["Login.RegisterPrompt"] = "Noch kein Konto?",
            ["Login.RegisterDescription"] = "Erstellen Sie es direkt in der App und melden Sie sich dann an.",
            ["Login.RegisterButton"] = "Registrieren",
            ["Register.PageTitle"] = "Registrierung",
            ["Register.BackShort"] = "Zurück",
            ["Register.Hero"] = "Ein neues Konto in wenigen Schritten erstellen",
            ["Register.Title"] = "Konto erstellen",
            ["Register.Subtitle"] = "Geben Sie Ihre Basisdaten ein, um die App zu nutzen.",
            ["Register.FullNameLabel"] = "Vollständiger Name",
            ["Register.FullNamePlaceholder"] = "Vollständigen Namen eingeben",
            ["Register.EmailLabel"] = "E-Mail",
            ["Register.EmailPlaceholder"] = "E-Mail eingeben",
            ["Register.PasswordLabel"] = "Passwort",
            ["Register.PasswordPlaceholder"] = "Passwort erstellen",
            ["Register.ConfirmPasswordLabel"] = "Passwort bestätigen",
            ["Register.ConfirmPasswordPlaceholder"] = "Passwort erneut eingeben",
            ["Register.Submit"] = "Konto erstellen",
            ["Register.BackToLogin"] = "Ich habe bereits ein Konto",
            ["Register.RequiredError"] = "Bitte füllen Sie alle Registrierungsfelder aus.",
            ["Register.InvalidEmailError"] = "Das E-Mail-Format ist ungültig.",
            ["Register.PasswordMismatchError"] = "Die Passwortbestätigung stimmt nicht überein.",
            ["Register.PasswordTooShortError"] = "Das Passwort muss mindestens 6 Zeichen lang sein.",
            ["Register.DuplicateEmailError"] = "Diese E-Mail wird bereits von einem anderen Konto verwendet.",
            ["Register.FailedError"] = "Das Konto kann derzeit nicht erstellt werden.",
            ["Register.SuccessTitle"] = "Registrierung abgeschlossen",
            ["Register.SuccessMessage"] = "Das Konto wurde erstellt. Sie können sich jetzt mit E-Mail oder Benutzername anmelden."
        };
    }
}
