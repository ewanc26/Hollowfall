using Godot;
using Hollowfall.Core.Localization;

namespace Hollowfall.Game;

/// <summary>Smoke checks for Gàidhlig: the fonts draw the grave accents, and choosing a language rebuilds the screens at once.</summary>
public partial class SmokeTest
{
    private const string GraveLetters = "àèìòùÀÈÌÒÙ";

    private static List<string> ButtonTexts(Node root)
    {
        var found = new List<string>();
        void Walk(Node n)
        {
            if (n is Button b && !b.IsQueuedForDeletion()) found.Add(b.Text);
            foreach (var child in n.GetChildren()) Walk(child);
        }
        Walk(root);
        return found;
    }

    private static List<string> LabelTexts(Node root)
    {
        var found = new List<string>();
        void Walk(Node n)
        {
            if (n is Label l && !l.IsQueuedForDeletion()) found.Add(l.Text);
            foreach (var child in n.GetChildren()) Walk(child);
        }
        Walk(root);
        return found;
    }

    /// <summary>The menu fonts draw à è ì ò ù in both cases, and the Language chooser changes the text of every screen straight away.</summary>
    private async Task CheckLanguageAsync(Main main)
    {
        Check("every UI font draws the grave-accented vowels Gàidhlig needs", () =>
        {
            foreach (var (name, font) in new[] { ("Display", UiFonts.Display), ("DisplayMedium", UiFonts.DisplayMedium), ("Body", UiFonts.Body), ("BodyBold", UiFonts.BodyBold) })
                foreach (char c in GraveLetters)
                    if (!font.HasChar(c))
                    {
                        GD.PrintErr($"    {name} cannot draw {c}");
                        return false;
                    }
            return true;
        });

        const string name = "the Language chooser rebuilds the menus in Gàidhlig, Bilingual and back";
        try
        {
            var menus = main.Menus;
            var settings = main.Settings;
            menus.Open(MenuScreen.Settings);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var box = menus.LanguageBox ?? throw new InvalidOperationException("Settings has no Language chooser");
            bool named = box.ItemCount == 3 && box.GetItemText(0) == "English" && box.GetItemText(1) == "Gàidhlig" && box.GetItemText(2) == "Gàidhlig agus Beurla";

            async Task<List<string>> Choose(Language language)
            {
                var chooser = menus.LanguageBox!;
                chooser.Select((int)language);
                chooser.EmitSignal(OptionButton.SignalName.ItemSelected, (int)language);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                return ButtonTexts(menus);
            }

            var gaelic = await Choose(Language.Gaelic);
            bool inGaelic = settings.Language == Language.Gaelic && gaelic.Contains("Cluich") && gaelic.Contains("Lean air") && gaelic.Contains("Roghainnean");
            bool stillSettings = menus.Current == MenuScreen.Settings && menus.LanguageBox!.Selected == (int)Language.Gaelic;
            bool englishKept = gaelic.Contains("Controls"); // no verified Gàidhlig, so the English stays

            var bilingual = await Choose(Language.Bilingual);
            bool inBoth = bilingual.Contains("Cluich (Play)") && bilingual.Contains("Roghainnean (Settings)");

            var back = await Choose(Language.English);
            bool english = settings.Language == Language.English && back.Contains("Play") && back.Contains("Resume") && !back.Contains("Cluich");

            // The Gàidhlig dungeon name shows under the title.
            await Choose(Language.Gaelic);
            menus.Open(MenuScreen.Title);
            var labels = LabelTexts(menus);
            bool dungeonName = labels.Any(t => t.Length > 0 && t == GaelicNames.For(Hollowfall.Core.Terrain.TerrainGenerator.SeedFromString(settings.Seed)));
            await Choose(Language.English);
            menus.Close();

            bool ok = named && inGaelic && stillSettings && englishKept && inBoth && english && dungeonName && settings.Language == Language.English;
            if (ok) { _passed++; GD.Print($"  ok   {name}"); }
            else { _failed++; GD.PrintErr($"  FAIL {name}: named={named} gaelic={inGaelic} still={stillSettings} englishKept={englishKept} both={inBoth} english={english} dungeonName={dungeonName}"); }
        }
        catch (Exception e)
        {
            _failed++;
            GD.PrintErr($"  FAIL {name}: {e.Message}");
        }
    }
}
