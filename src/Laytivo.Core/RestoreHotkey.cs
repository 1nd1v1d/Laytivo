namespace Laytivo.Core;

public sealed record HotkeyChoice(int Value, string Label);

public static class RestoreHotkey
{
    public static IReadOnlyList<HotkeyChoice> Modifiers { get; } = Array.AsReadOnly<HotkeyChoice>(
        [new(3, "Strg + Alt"), new(6, "Strg + Umschalt"), new(5, "Alt + Umschalt"), new(7, "Strg + Alt + Umschalt")]);
    public static IReadOnlyList<HotkeyChoice> Keys { get; } = Array.AsReadOnly(
        Enumerable.Range(0x41, 26).Select(k => new HotkeyChoice(k, ((char)k).ToString()))
            .Concat(Enumerable.Range(0x70, 11).Select(k => new HotkeyChoice(k, $"F{k - 0x6f}"))).ToArray());

    public static bool IsValid(int modifiers, int key) => modifiers is 3 or 5 or 6 or 7
        && (key is >= 0x41 and <= 0x5a or >= 0x70 and <= 0x7a);

    public static string Describe(AppSettings settings) =>
        $"{Modifiers.First(m => m.Value == settings.RestoreHotkeyModifiers).Label} + {Keys.First(k => k.Value == settings.RestoreHotkeyKey).Label}";
}
