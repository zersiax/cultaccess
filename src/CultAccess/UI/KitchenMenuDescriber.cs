using System.Text;
using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using Lamb.UI;
using Lamb.UI.KitchenMenu;
using UnityEngine.UI;

namespace CultAccess.UI
{
    /// <summary>
    /// Describes the Cooking Fire's recipe buttons.
    ///
    /// These are icon-only with a bare quantity label, so the generic focus reader announced
    /// just "2" — the number of ingredients owned, with no idea which recipe it belonged to.
    /// The only way to learn what a button was had been to press it, by which point the
    /// ingredients were already spent.
    ///
    /// Hooked through the focus path rather than <c>RecipeItem.OnSelect</c>: menu navigation
    /// goes through <c>UINavigatorNew</c>, and the EventSystem select handler fired only
    /// occasionally, so most moves fell through to the generic reader.
    ///
    /// Everything is read from <c>CookingData</c> and the button's own final gate, never from
    /// the icon.
    /// </summary>
    internal static class KitchenMenuDescriber
    {
        /// <summary>
        /// Replaces the generic description for a recipe button. False for anything else, so
        /// the ordinary focus reader keeps handling the rest of the menu.
        /// </summary>
        internal static bool TryDescribeItem(Selectable selectable, out string description)
        {
            description = string.Empty;
            if (selectable == null) return false;

            var item = selectable.GetComponentInParent<RecipeItem>();
            if (item == null) return false;

            description = Describe(item);
            return description.Length > 0;
        }

        /// <summary>
        /// The controller's own choose handler, so the confirmation reflects a recipe the game
        /// actually accepted rather than a keypress that may have been refused.
        /// </summary>
        [HarmonyPatch(typeof(UICookingFireMenuController), "OnRecipeChosen")]
        private static class RecipeChosenPatch
        {
            [HarmonyPostfix]
            private static void AfterChosen(InventoryItem.ITEM_TYPE recipe)
            {
                if (!Plugin.SpeechEnabled.Value) return;

                var name = MealName(recipe);
                Plugin.Log.LogInfo($"[kitchen] chosen recipe={recipe} spoken=\"{name}\"");
                Speaker.Say($"Added {name}", SpeechPriority.Now);
            }
        }

        private static string Describe(RecipeItem item)
        {
            try
            {
                var type = item.Type;

                // A recipe the player has not discovered is deliberately concealed by the
                // game. Naming it here would hand over content the artwork hides.
                if (!CookingData.HasRecipeDiscovered(type))
                    return $"Undiscovered recipe, {Position(item)}";

                var parts = new System.Collections.Generic.List<string> { MealName(type) };

                var confirmable = item.Button != null && item.Button.Confirmable;
                parts.Add(confirmable
                    ? "available"
                    : CookingData.CanMakeMeal(type)
                        ? "unavailable"
                        : "not enough ingredients");

                var ingredients = Ingredients(type);
                if (ingredients.Length > 0) parts.Add(ingredients);

                var satiation = CookingData.GetSatationLevel(type);
                if (satiation > 0)
                    parts.Add(satiation == 1 ? "1 hunger star" : $"{satiation} hunger stars");

                parts.Add(Position(item));

                var spoken = string.Join(", ", parts.ToArray());
                Plugin.Log.LogInfo(
                    $"[kitchen] focus recipe={type} confirmable={confirmable} " +
                    $"spoken=\"{spoken}\"");
                return spoken;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not describe recipe item: {e.Message}");
                return "Recipe, details unavailable";
            }
        }

        /// <summary>
        /// Meals localize under <c>CookingData/{type}/Name</c>. The ordinary inventory term
        /// is untranslated for them, which produced spoken names like "MEAL MEAT".
        /// </summary>
        private static string MealName(InventoryItem.ITEM_TYPE type)
        {
            try
            {
                var cooking = RichText.Clean(CookingData.GetLocalizedName(type));
                if (RichText.IsUsableLocalization(cooking, $"CookingData/{type}/Name"))
                    return cooking;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not localize meal name: {e.Message}");
            }

            return ItemName(type);
        }

        /// <summary>
        /// The first entry of the recipe's cost table, which is what
        /// <c>UICookingFireMenuController.OnRecipeChosen</c> actually deducts. Owned amounts
        /// come with it so the player does not have to leave and check their bags.
        /// </summary>
        private static string Ingredients(InventoryItem.ITEM_TYPE type)
        {
            try
            {
                var recipe = CookingData.GetRecipe(type);
                if (recipe == null || recipe.Count == 0 || recipe[0] == null)
                    return string.Empty;

                var text = new StringBuilder();
                foreach (var ingredient in recipe[0])
                {
                    if (ingredient == null) continue;
                    if (text.Length > 0) text.Append(", ");

                    var owned = Inventory.GetItemQuantity(ingredient.type);
                    text.Append(
                        $"{ingredient.quantity} {ItemName((InventoryItem.ITEM_TYPE)ingredient.type)}, " +
                        $"have {owned}");
                }

                return text.Length == 0 ? string.Empty : $"needs {text}";
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not read recipe ingredients: {e.Message}");
                return string.Empty;
            }
        }

        private static string Position(RecipeItem item)
        {
            var transform = item.transform;
            var parent = transform.parent;
            if (parent == null) return string.Empty;

            return ProgressionText.Position(
                "recipe", transform.GetSiblingIndex(), parent.childCount);
        }

        private static string ItemName(InventoryItem.ITEM_TYPE type)
        {
            try
            {
                var localized = RichText.Clean(InventoryItem.LocalizedName(type));
                return RichText.IsUsableLocalization(localized, $"Inventory/{type}")
                    ? localized
                    : RichText.Humanise(type.ToString());
            }
            catch
            {
                return RichText.Humanise(type.ToString());
            }
        }
    }
}
