using NTDLS.Localization;
using NTDLS.WinFormsHelpers;
using System.Globalization;

namespace NetProxy.Client.Forms
{
    /// <summary>
    /// A list of the languages to choose from.
    /// </summary>
    public partial class FormLanguage : Form
    {
        public FormLanguage(string? currentLanguage)
        {
            InitializeComponent();
            FormLocalizer.Apply(this);
            Theming.InitializeForm(this);

            var choices = LanguageChoices.Build(Localizer.Default);

            //Items rather than DataSource: a data source is not bound until the list is on a form, so the list would still be empty here.
            listBoxLanguages.Items.AddRange(choices.ToArray<object>());
            listBoxLanguages.SelectedIndex = LanguageChoices.IndexOf(choices, currentLanguage);
        }

        private void ListBoxLanguages_DoubleClick(object? sender, EventArgs e)
        {
            if (listBoxLanguages.IndexFromPoint(listBoxLanguages.PointToClient(MousePosition)) >= 0)
            {
                DialogResult = DialogResult.OK;
            }
        }

        /// <summary>The chosen language: a culture code, or an empty string for Automatic.</summary>
        public string SelectedLanguage => (listBoxLanguages.SelectedItem as LanguageChoice)?.Code ?? string.Empty;
    }

    /// <summary>
    /// One language in the list the user chooses from.
    /// </summary>
    public sealed class LanguageChoice
    {
        /// <summary>The culture code to save ("de", "pt", "zh-Hans"), or an empty string for Automatic (follow Windows).</summary>
        public string Code { get; }
        public string DisplayName { get; }

        public LanguageChoice(string code, string displayName)
        {
            Code = code;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// The list of languages to choose from: "Automatic" (the Windows language), then every supported language, each shown in its
    /// own language so it can be found by someone who cannot read the current one ("Deutsch", "日本語").
    /// </summary>
    public static class LanguageChoices
    {
        public static List<LanguageChoice> Build(Localizer localizer)
        {
            var choices = new List<LanguageChoice>
            {
                new(string.Empty, localizer.Localize("Automatic: {0}", Capitalize(CultureInfo.CurrentUICulture.NativeName)))
            };

            var languages = new SupportedCultures().Collection
                .Select(o => new { o.Code, Native = Capitalize(o.Culture.NativeName), Name = localizer.Localize(o.Name) })
                .OrderBy(o => o.Native, StringComparer.CurrentCultureIgnoreCase);

            foreach (var language in languages)
            {
                var display = language.Name.Equals(language.Native, StringComparison.CurrentCultureIgnoreCase)
                    ? language.Native
                    : $"{language.Native} ({language.Name})";

                choices.Add(new LanguageChoice(language.Code, display));
            }

            return choices;
        }

        /// <summary>
        /// The position of a language setting in the list. A regional setting ("pt-BR") finds its language ("pt"); anything unknown finds Automatic.
        /// </summary>
        public static int IndexOf(IList<LanguageChoice> choices, string? code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo(code.Trim(), predefinedOnly: true);
            }
            catch (CultureNotFoundException)
            {
                return 0;
            }

            for (var current = culture; !string.IsNullOrEmpty(current.Name); current = current.Parent)
            {
                for (int index = 1; index < choices.Count; index++)
                {
                    if (choices[index].Code.Equals(current.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return index;
                    }
                }
            }

            return 0;
        }

        private static string Capitalize(string name)
            => string.IsNullOrEmpty(name) ? name : char.ToUpper(name[0], CultureInfo.CurrentCulture) + name[1..];
    }

}
