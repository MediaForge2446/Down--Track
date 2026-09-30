using System;
using System.Collections.Generic;
using System.Globalization;

namespace DownTrack.Core.Localization;

public class LocalizationService
{
    public static LocalizationService Instance { get; } = new();

    public string CurrentLanguage { get; private set; } = "en";
    public bool IsRightToLeft => CurrentLanguage is "he" or "ar";

    public event Action? LanguageChanged;

    private readonly Dictionary<string, Dictionary<string, string>> _translations = new()
    {
        ["en"] = new()
        {
            ["AppTitle"] = "DownTrack",
            ["Library"] = "Library",
            ["AddMedia"] = "Add Media",
            ["Settings"] = "Settings",
            ["UrlPlaceholder"] = "Paste YouTube song or playlist link here...",
            ["Search"] = "Search media...",
            ["Format"] = "Format",
            ["Quality"] = "Quality",
            ["Download"] = "Download",
            ["Cancel"] = "Cancel",
            ["ApplyToAll"] = "Apply to all",
            ["Retry"] = "Try again",
            ["Installing"] = "Installing DownTrack...",
            ["InstallSuccess"] = "DownTrack installed successfully!",
            ["InstallError"] = "Installation encountered an issue."
        },
        ["he"] = new()
        {
            ["AppTitle"] = "DownTrack",
            ["Library"] = "ספרייה",
            ["AddMedia"] = "הוסף מדיה",
            ["Settings"] = "הגדרות",
            ["UrlPlaceholder"] = "הדבק כאן קישור לשיר או פלייליסט מיוטיוב...",
            ["Search"] = "חיפוש קבצים...",
            ["Format"] = "פורמט",
            ["Quality"] = "איכות",
            ["Download"] = "הורדה",
            ["Cancel"] = "ביטול",
            ["ApplyToAll"] = "החל על כולם",
            ["Retry"] = "נסה שוב",
            ["Installing"] = "מתקין את DownTrack...",
            ["InstallSuccess"] = "DownTrack הותקנה בהצלחה!",
            ["InstallError"] = "ההתקנה נתקלה בבעיה."
        }
    };

    public string Get(string key)
    {
        if (_translations.TryGetValue(CurrentLanguage, out var dict) && dict.TryGetValue(key, out var val))
        {
            return val;
        }

        // Fallback to English
        if (_translations["en"].TryGetValue(key, out var enVal))
        {
            return enVal;
        }

        return key;
    }

    public void SetLanguage(string langCode)
    {
        if (!_translations.ContainsKey(langCode)) langCode = "en";
        CurrentLanguage = langCode;
        LanguageChanged?.Invoke();
    }
}
