namespace SplitWireTurkey.Linux;

/// <summary>Implemented by views that refresh their labels from LanguageManager.</summary>
public interface PagesLocalized
{
    void ApplyTranslations();
}
